using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Background service that simulates a live MCP run using v6 golden mapping + historical stats.
/// Produces realistic fake data with per-(location, subprocess) timings, failure rates, and rerun
/// patterns sourced from 4 months of production history.
///
/// Canonical-iteration rule: of all iterations for a (location, subprocess), only the LAST iteration
/// where every step completes successfully counts as EfficientRuntimeHours. All prior iterations —
/// failed outright OR completed-but-superseded (rerun due to wrong data/config) — roll into
/// FailedRuntimeHours.
/// </summary>
public class SimulatorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly SimulatorState _state;
    private readonly HistoricalStatsService _stats;
    private readonly ILogger<SimulatorService> _log;

    // Configurable knobs
    private const double SupersedeReruns = 0.4; // 40% of reruns are operator-triggered after successful completion
    private const double MinStepMinutes = 0.25;
    private const double NoiseRatio = 0.3;       // ±30% noise on avg step duration
    private const double OverrideRate = 0.15;    // ~15% of subprocesses have a "manual QRM workaround"
    private const double DqBlockRate = 0.12;     // ~12% of locations get a data quality blocker
    private const double DqBlockMinHours = 8;    // business-hour delay range for DQ blocks
    private const double DqBlockMaxHours = 24;
    private const string DqBlockSubprocess = "Load Position"; // DQ blocks happen at data ingestion

    private static readonly string[] OverrideReasons =
    {
        "Manual QRM export via UI — Q3 timeout, INC pending",
        "Manually re-ran the step in QRM because Q3 failed to record markers",
        "Operator workaround: Q3 bug caused step to finish without log output",
        "Manual execution through QRM due to Q3 connectivity issue",
        "Exported via QRM reporter UI after Q3 timed out twice",
    };

    private static readonly string[] DqBlockErrors =
    {
        "Data Quality breach: Missing position data for FX portfolio. Upstream feed did not deliver by cutoff time. Escalated to DAS team (INC pending).",
        "Data Quality breach: Stale market data detected — source system returned data from previous business day. Waiting for upstream correction.",
        "Data Quality breach: Position count mismatch. Expected ~45,000 positions, received 38,211. Investigation pending with upstream data owner.",
        "Data Quality breach: NTG file validation failed — negative notional values detected in 312 records. Feed team notified, manual correction underway.",
        "Data Quality breach: FX rate feed incomplete — 4 currency pairs missing from daily extract. Operations contacted treasury for manual rates.",
    };

    public SimulatorService(
        IServiceProvider services,
        SimulatorState state,
        HistoricalStatsService stats,
        ILogger<SimulatorService> log)
    {
        _services = services;
        _state = state;
        _stats = stats;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Simulator service started. Waiting for start signal...");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.StartRequested)
            {
                _state.StartRequested = false;
                try
                {
                    await RunSimulationAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Simulator encountered an error");
                    _state.Phase = "Error: " + ex.Message;
                    _state.IsRunning = false;
                }
            }
            if (_state.ResetRequested)
            {
                _state.ResetRequested = false;
                await CleanupSimulatedRunAsync();
            }
            await Task.Delay(500, stoppingToken);
        }
    }

    private async Task RunSimulationAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenlightContext>();

        var targetMonth = _state.TargetMonth;
        var year = 2000 + int.Parse(targetMonth[..2]);
        var month = int.Parse(targetMonth[2..]);
        var isQuarterEnd = month is 3 or 6 or 9 or 12;
        var seed = targetMonth.GetHashCode();
        var rng = new Random(seed);

        // Clean up any prior simulated run for the same month
        var existing = await db.McpRuns.FirstOrDefaultAsync(r => r.ReportMonth == targetMonth, ct);
        if (existing != null)
        {
            db.SubprocessRuns.RemoveRange(db.SubprocessRuns.Where(sr => sr.McpRunId == existing.Id));
            db.ProcessLogEntries.RemoveRange(db.ProcessLogEntries.Where(p => p.McpRunId == existing.Id));
            db.McpRuns.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        // Create the new run
        var run = new McpRun
        {
            ReportMonth = targetMonth,
            Year = year,
            Month = month,
            Status = "Running",
            StartDate = DateTime.UtcNow,
            EomDate = new DateTime(year, month, DateTime.DaysInMonth(year, month)),
        };
        db.McpRuns.Add(run);
        await db.SaveChangesAsync(ct);
        _state.CurrentRunId = run.Id;
        _state.IsRunning = true;
        _state.Phase = "Initializing";
        _log.LogInformation("Simulator: started run {ReportMonth} (id {Id}), quarterly={Q}, speed={Spd}x",
            targetMonth, run.Id, isQuarterEnd, _state.SpeedMultiplier);

        // Load subprocess scope from DB — filter by IsQuarterly × isQuarterEnd
        var allLocs = await db.Locations.Where(l => l.InScope).OrderBy(l => l.Code).ToListAsync(ct);
        var allSubs = await db.Subprocesses.OrderBy(s => s.DisplayOrder).ToListAsync(ct);

        // Registry: (locId, subId) -> ordered step list
        var registryRows = await db.LocationStepRegistry.ToListAsync(ct);
        var registry = registryRows
            .GroupBy(r => (r.LocationId, r.SubprocessId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.DisplayOrder).Select(r => r.RequiredStepName).ToList()
            );

        // Determine eligible subprocesses for this month
        bool IsEligible(Subprocess s) => isQuarterEnd || !s.IsQuarterly;
        var eligibleSubs = allSubs.Where(IsEligible).ToList();

        // Pre-seed SubprocessRun rows with correct in-scope flag
        var runRows = new Dictionary<(int loc, int sub), SubprocessRun>();
        foreach (var loc in allLocs)
        {
            foreach (var sub in allSubs)
            {
                var isInScope = registry.ContainsKey((loc.Id, sub.Id)) && IsEligible(sub);
                var steps = isInScope ? registry[(loc.Id, sub.Id)].Count : 0;
                var sr = new SubprocessRun
                {
                    McpRunId = run.Id,
                    LocationId = loc.Id,
                    SubprocessId = sub.Id,
                    Status = isInScope ? "Not Started" : "Not in Scope",
                    TotalRequiredSteps = steps,
                    CompletedSteps = 0,
                };
                runRows[(loc.Id, sub.Id)] = sr;
                db.SubprocessRuns.Add(sr);
            }
        }
        await db.SaveChangesAsync(ct);
        var inScopeRuns = runRows.Values.Where(r => r.Status == "Not Started").ToList();
        _state.TotalSubprocesses = inScopeRuns.Count;
        _state.CompletedSubprocesses = 0;
        _log.LogInformation("Simulator: {InScope} in-scope (loc × sub) combos for {Month}", inScopeRuns.Count, targetMonth);

        // Group eligible subprocesses by phase in a fixed order
        var phaseOrder = new[] { "DataIngestion", "Processing", "Reporting" };
        var subsByPhase = eligibleSubs
            .GroupBy(s => NormalizePhase(s.Phase))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.DisplayOrder).ToList());

        // Use a simulated "current clock" per location that advances as each subprocess executes.
        // Initialize at the run start date (today's date anchor) to produce realistic wall-clock timestamps.
        var anchor = DateTime.UtcNow;
        var locClock = allLocs.ToDictionary(l => l.Id, _ => anchor);

        // Pick DQ-blocked locations (Load Position will fail → long delay → blocks all downstream)
        var dqBlockedLocs = new HashSet<int>();
        foreach (var loc in allLocs)
        {
            if (rng.NextDouble() < DqBlockRate)
                dqBlockedLocs.Add(loc.Id);
        }
        // Guarantee at least 1 DQ block for demo visibility
        if (dqBlockedLocs.Count == 0 && allLocs.Count > 0)
            dqBlockedLocs.Add(allLocs[rng.Next(allLocs.Count)].Id);
        _log.LogInformation("Simulator: DQ blocks at {Count} locations: {Locs}",
            dqBlockedLocs.Count, string.Join(", ", allLocs.Where(l => dqBlockedLocs.Contains(l.Id)).Select(l => l.Code)));

        // Main walk
        foreach (var phase in phaseOrder)
        {
            if (!subsByPhase.TryGetValue(phase, out var phaseSubs)) continue;
            _state.Phase = phase;
            foreach (var sub in phaseSubs)
            {
                ct.ThrowIfCancellationRequested();
                _state.CurrentSubprocess = sub.Name;

                foreach (var loc in allLocs)
                {
                    if (!runRows.TryGetValue((loc.Id, sub.Id), out var sr) || sr.Status != "Not Started") continue;
                    if (!registry.TryGetValue((loc.Id, sub.Id), out var steps) || steps.Count == 0) continue;

                    _state.CurrentLocation = loc.Code;
                    var stats = _stats.Get(loc.Code, sub.Name);
                    var iterCount = DecideIterationCount(stats, rng);
                    var locStart = locClock[loc.Id];

                    // DQ block: if this is Load Position at a DQ-blocked location,
                    // force at least 2 iterations with a LONG business-hour gap between them.
                    // The first iteration fails with a DQ-specific error. All downstream
                    // subprocesses for this location are delayed because locClock advances.
                    // DQ block: exactly 2 iterations — one DQ failure, one clean rerun after fix
                    // NOT many retries; just the one DQ issue with a long wait for upstream correction
                    var isDqBlock = dqBlockedLocs.Contains(loc.Id) && sub.Name == DqBlockSubprocess;
                    if (isDqBlock) iterCount = 2;

                    // Decide if this (loc, sub) has a manual-QRM-workaround scenario
                    // (one or two mandatory steps missing from logs; an OperatorOverride row will mark them complete)
                    var useOverride = rng.NextDouble() < OverrideRate && steps.Count >= 2;
                    var overrideStepIndices = new HashSet<int>();
                    if (useOverride)
                    {
                        // Pick 1 or 2 random steps to skip only on the LAST (canonical) iteration
                        var skipCount = rng.Next(1, Math.Min(3, steps.Count));
                        while (overrideStepIndices.Count < skipCount)
                            overrideStepIndices.Add(rng.Next(steps.Count));
                    }

                    // Pass 1: generate all entries (iterations × steps)
                    var entries = new List<ProcessLogEntry>();
                    var iterOutcomes = new List<bool>(); // true = iteration succeeded
                    DateTime iterStart = locStart;
                    for (int iter = 1; iter <= iterCount; iter++)
                    {
                        bool isLast = iter == iterCount;
                        // Decide why this iteration would NOT be canonical:
                        //   - Not last iteration → failed-or-superseded
                        //   - Last iteration → canonical (all steps succeed)
                        bool shouldSucceed = isLast;
                        // For non-last iterations, decide: failure-triggered or supersede-triggered
                        bool isSupersede = !shouldSucceed && rng.NextDouble() < SupersedeReruns;
                        int? failStepIndex = null;
                        if (!shouldSucceed && !isSupersede)
                            failStepIndex = rng.Next(steps.Count); // which step fails
                        // DQ block: force first iteration to fail at the load step with a DQ-specific error
                        bool useDqError = isDqBlock && iter == 1;
                        if (useDqError) { failStepIndex = 0; isSupersede = false; }

                        // Wait until the simulated iteration start — advance the clock
                        await SetSimulatedNow(iterStart, ct);

                        DateTime stepCursor = iterStart;
                        for (int i = 0; i < steps.Count; i++)
                        {
                            var stepName = steps[i];

                            // Override scenario: on the LAST iteration, skip overridden steps entirely
                            // (operator did them manually via QRM, no Q3 log entry produced)
                            if (isLast && useOverride && overrideStepIndices.Contains(i))
                            {
                                // Advance clock slightly to represent the manual work taking some time,
                                // even though no log row is created
                                var manualDurMin = Math.Max(MinStepMinutes, SampleStepDuration(stats, stepName, sub.Phase, rng) * 0.7);
                                await WaitSimulatedMinutesAsync(manualDurMin, ct);
                                stepCursor = stepCursor.AddMinutes(manualDurMin);
                                continue; // no ProcessLogEntry row
                            }

                            var durMin = SampleStepDuration(stats, stepName, sub.Phase, rng);
                            var stepStart = stepCursor;
                            await WaitSimulatedMinutesAsync(durMin, ct);
                            var stepEnd = stepCursor.AddMinutes(durMin);
                            stepCursor = stepEnd;

                            string state;
                            string? err = null;
                            if (!shouldSucceed && failStepIndex.HasValue && i == failStepIndex.Value)
                            {
                                state = "Failed";
                                err = useDqError ? DqBlockErrors[rng.Next(DqBlockErrors.Length)] : PickError(stats, rng);
                            }
                            else
                            {
                                state = "Completed";
                            }

                            entries.Add(new ProcessLogEntry
                            {
                                McpRunId = run.Id,
                                LocationId = loc.Id,
                                Process = "MCP",
                                ScriptName = BuildScriptName(loc.Code, sub.Name, iter),
                                StepName = stepName,
                                StateName = state,
                                StartMarker = $"START {stepName}",
                                EndMarker = state == "Completed" ? $"END {stepName}" : null,
                                StartedAt = stepStart,
                                EndedAt = state == "Completed" ? stepEnd : stepEnd,
                                Iteration = iter,
                                ErrorMessage = err,
                            });

                            // If this step failed, skip remaining steps in this iteration
                            if (state == "Failed")
                            {
                                stepCursor = stepEnd;
                                break;
                            }

                            // Update SubprocessRun progress only on the last (canonical) iteration's completions
                            if (isLast)
                            {
                                sr.CompletedSteps = i + 1;
                                if (i == 0 && sr.StartedAt == null) sr.StartedAt = stepStart;
                                sr.Status = "Running";
                                await db.SaveChangesAsync(ct);
                            }
                        }

                        iterOutcomes.Add(shouldSucceed);

                        // If another iteration follows, advance the clock by business-hour opportunity cost
                        if (iter < iterCount)
                        {
                            double opCostHours;
                            if (isDqBlock && iter == 1)
                            {
                                // DQ block: long delay while data quality team investigates + fixes upstream feed
                                opCostHours = DqBlockMinHours + rng.NextDouble() * (DqBlockMaxHours - DqBlockMinHours);
                            }
                            else
                            {
                                opCostHours = 0.25 + rng.NextDouble() * 3.0; // ~15 min to 3h normal delay
                            }
                            iterStart = BusinessHoursCalculator.AddBusinessHours(stepCursor, opCostHours);
                        }
                        else
                        {
                            iterStart = stepCursor;
                        }
                    }

                    // Pass 2: compute per-entry runtime fields using canonical-iteration rule
                    // Canonical iteration = highest iter where no step has Failed/Stopped/Unfinished
                    // Note: with overrides, the last iteration has fewer rows (some steps skipped) but still counts as canonical
                    int? canonIter = DetermineCanonicalIteration(entries);
                    ApplyRuntimeFields(entries, canonIter);

                    // Pass 3: set NextStarted (within (loc, sub, iteration))
                    SetNextStarted(entries);

                    // Persist the log entries in a batch
                    db.ProcessLogEntries.AddRange(entries);

                    // Create OperatorOverride rows for any skipped steps on the canonical iteration
                    if (useOverride && canonIter.HasValue)
                    {
                        foreach (var idx in overrideStepIndices)
                        {
                            db.OperatorOverrides.Add(new OperatorOverride
                            {
                                McpRunId = run.Id,
                                LocationId = loc.Id,
                                SubprocessId = sub.Id,
                                StepName = steps[idx],
                                Action = "complete",
                                Reason = OverrideReasons[rng.Next(OverrideReasons.Length)],
                                TicketRef = rng.NextDouble() < 0.6 ? $"INC{14500000 + rng.Next(100000)}" : null,
                                Operator = "sim.operator@ing.com",
                                CreatedAt = iterStart,
                            });
                        }
                    }

                    // Finalize SubprocessRun — for the demo, ALWAYS end Completed:
                    //   - If canonical iteration exists → Completed (standard path)
                    //   - If overrides filled the gap → Completed (HasOverrides=true)
                    //   - Even if no canonical and no overrides (shouldn't happen given last iter always succeeds) → Completed with HasOverrides=true
                    sr.Status = "Completed";
                    sr.HasOverrides = useOverride;
                    sr.CompletedAt = iterStart;
                    sr.CompletedSteps = steps.Count;
                    if (sr.StartedAt.HasValue)
                        sr.ElapsedMinutes = (sr.CompletedAt.Value - sr.StartedAt.Value).TotalMinutes;

                    _state.CompletedSubprocesses++;
                    locClock[loc.Id] = iterStart;
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        // Finalize run — all subprocesses end Completed (some via overrides, some with earlier failed iterations in their history)
        var anyOverrides = await db.SubprocessRuns.AnyAsync(sr => sr.McpRunId == run.Id && sr.HasOverrides, ct);
        run.Status = anyOverrides ? "Completed with Overrides" : "Completed";
        run.EndDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _state.Phase = "Complete";
        _state.IsRunning = false;
        _state.CurrentSubprocess = null;
        _state.CurrentLocation = null;
        _log.LogInformation("Simulator: finished run {ReportMonth} — {Status}", targetMonth, run.Status);
    }

    private static string NormalizePhase(string phase)
        => phase.Replace(" ", "");

    private static string BuildScriptName(string location, string subprocess, int iter)
    {
        var baseName = $"{subprocess.Replace(" ", "_")}_{location}_MCP";
        return iter == 1 ? baseName : $"{baseName} rr{iter - 1}";
    }

    private int DecideIterationCount(HistoricalStatsService.SubprocessStats? stats, Random rng)
    {
        if (stats == null) return 1;
        // Use the rerunCountAvg as the center; add noise so not every run has exactly the same count
        var mean = Math.Max(1.0, stats.RerunCountAvg);
        // Simple Poisson-ish sampling: round(mean) with ±1 jitter based on failure rate
        var p = rng.NextDouble();
        if (stats.FailureRate <= 0.05) return 1; // very reliable → usually single iteration
        if (p < stats.FailureRate) return (int)Math.Round(mean + (rng.NextDouble() - 0.5));
        return 1;
    }

    private static double SampleStepDuration(HistoricalStatsService.SubprocessStats? stats, string stepName, string phase, Random rng)
    {
        double avg;
        if (stats != null)
        {
            var s = stats.Steps.FirstOrDefault(x => x.StepName == stepName);
            avg = s?.AvgMinutes ?? PhaseFallbackMinutes(phase);
        }
        else
        {
            avg = PhaseFallbackMinutes(phase);
        }
        // Apply ±NoiseRatio multiplicative noise
        var noise = 1.0 + (rng.NextDouble() * 2 - 1) * NoiseRatio;
        return Math.Max(MinStepMinutes, avg * noise);
    }

    private static double PhaseFallbackMinutes(string phase)
        => phase.Replace(" ", "") switch
        {
            "DataIngestion" => 8,
            "Processing" => 25,
            "Reporting" => 10,
            _ => 15,
        };

    private string PickError(HistoricalStatsService.SubprocessStats? stats, Random rng)
    {
        if (stats != null && stats.SampleErrors.Count > 0)
            return stats.SampleErrors[rng.Next(stats.SampleErrors.Count)];
        var pool = _stats.AllErrors;
        return pool.Count > 0 ? pool[rng.Next(pool.Count)] : "Exception Info: Workflow finished with errors.";
    }

    /// <summary>
    /// Canonical iteration = highest iteration where NO step ended in a failure state.
    /// </summary>
    private static int? DetermineCanonicalIteration(List<ProcessLogEntry> entries)
    {
        var failStates = new HashSet<string> { "Failed", "Stopped", "Unfinished" };
        var byIter = entries.GroupBy(e => e.Iteration).OrderByDescending(g => g.Key);
        foreach (var g in byIter)
        {
            if (g.Any(e => failStates.Contains(e.StateName))) continue;
            if (g.Any(e => e.StateName == "Completed")) return g.Key;
        }
        return null;
    }

    /// <summary>
    /// Canonical-iteration rule:
    ///   Efficient = sum of canonical iteration's step durations
    ///   Failed    = sum of all prior iterations' step durations (whether Failed or superseded-Completed)
    ///   OpportunityCost on boundary rows = business-hour gap between this iter's end and next iter's start
    /// </summary>
    private static void ApplyRuntimeFields(List<ProcessLogEntry> entries, int? canonIter)
    {
        // Step durations
        foreach (var e in entries)
        {
            if (e.StartedAt.HasValue && e.EndedAt.HasValue)
                e.TotalRuntimeHours = Math.Max(0, (e.EndedAt.Value - e.StartedAt.Value).TotalHours);
            else
                e.TotalRuntimeHours = 0;

            var isCanon = canonIter.HasValue && e.Iteration == canonIter.Value;
            e.EfficientRuntimeHours = isCanon ? e.TotalRuntimeHours : 0;
            e.FailedRuntimeHours = isCanon ? 0 : e.TotalRuntimeHours;
        }

        // Opportunity cost on iteration boundaries
        var iterEnds = entries.GroupBy(e => e.Iteration)
            .ToDictionary(g => g.Key,
                g => (Last: g.OrderByDescending(e => e.EndedAt ?? DateTime.MinValue).First(),
                      First: g.OrderBy(e => e.StartedAt ?? DateTime.MaxValue).First()));

        foreach (var kv in iterEnds)
        {
            var iter = kv.Key;
            var last = kv.Value.Last;
            // Find the first entry of the next iteration
            if (iterEnds.TryGetValue(iter + 1, out var nextSet))
            {
                var next = nextSet.First;
                var opHours = BusinessHoursCalculator.BusinessHoursBetween(last.EndedAt, next.StartedAt);
                last.OpportunityCostHours = opHours;
            }
        }

        // Inefficient + E2E
        foreach (var e in entries)
        {
            e.InefficientRuntimeHours = e.FailedRuntimeHours + e.OpportunityCostHours;
            e.E2ERuntimeHours = e.TotalRuntimeHours + e.OpportunityCostHours;
        }
    }

    private static void SetNextStarted(List<ProcessLogEntry> entries)
    {
        // Sort by iteration, then by StartedAt
        var ordered = entries
            .OrderBy(e => e.Iteration)
            .ThenBy(e => e.StartedAt ?? DateTime.MinValue)
            .ToList();
        for (int i = 0; i < ordered.Count - 1; i++)
            ordered[i].NextStarted = ordered[i + 1].StartedAt;
    }

    // ----- Timing helpers (simulated clock) -----

    /// <summary>Wait real time corresponding to simMinutes at the current speed multiplier.</summary>
    private async Task WaitSimulatedMinutesAsync(double simMinutes, CancellationToken ct)
    {
        var realMs = simMinutes * 60_000 / _state.SpeedMultiplier;
        var remaining = realMs;
        const int tickMs = 200;
        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();
            while (_state.IsPaused && !ct.IsCancellationRequested)
                await Task.Delay(200, ct);
            var wait = Math.Min(remaining, tickMs);
            await Task.Delay((int)wait, ct);
            remaining -= wait;
            realMs = simMinutes * 60_000 / _state.SpeedMultiplier;
        }
    }

    /// <summary>No-op placeholder for future "set simulated clock to X" if we decouple from real time.</summary>
    private Task SetSimulatedNow(DateTime simTime, CancellationToken ct) => Task.CompletedTask;

    private async Task CleanupSimulatedRunAsync()
    {
        var runId = _state.CurrentRunId;
        if (runId == null) return;
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GreenlightContext>();
        var run = await db.McpRuns.FindAsync(runId.Value);
        if (run != null)
        {
            db.SubprocessRuns.RemoveRange(db.SubprocessRuns.Where(sr => sr.McpRunId == run.Id));
            db.ProcessLogEntries.RemoveRange(db.ProcessLogEntries.Where(p => p.McpRunId == run.Id));
            db.McpRuns.Remove(run);
            await db.SaveChangesAsync();
        }
        _state.CurrentRunId = null;
        _state.IsRunning = false;
        _state.Phase = "Idle";
        _state.CompletedSubprocesses = 0;
        _state.TotalSubprocesses = 0;
        _state.CurrentSubprocess = null;
        _state.CurrentLocation = null;
        _log.LogInformation("Simulator: cleaned up simulated run {Id}", runId);
    }
}
