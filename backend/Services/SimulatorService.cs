using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    private readonly ScriptedEventQueue _queue;
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

    // Fast-replay cache: stores serialized row payload for (month, mode, queue-fingerprint) combos
    // that have completed at least once. On a matching Start, ReplayCachedRunAsync skips the sim
    // loop entirely (no WaitSimulatedMinutesAsync), dumps rows to DB, and animates progress 0→100%
    // over ~4 seconds. Reset on backend restart.
    private sealed class CachedRun
    {
        public string SubRunsJson { get; set; } = "[]";
        public string EntriesJson { get; set; } = "[]";
        public string OverridesJson { get; set; } = "[]";
        /// <summary>Fingerprint-of-event-params → final Status ("Done" or "Skipped") from the original run.</summary>
        public Dictionary<string, string> EventFinalStatus { get; set; } = new();
        public int TotalSubprocesses { get; set; }
        public string FinalRunStatus { get; set; } = "";
    }
    private readonly Dictionary<string, CachedRun> _runCache = new();
    private static readonly JsonSerializerOptions CacheJsonOpts = new()
    {
        PropertyNamingPolicy = null, // preserve casing to match EF model properties
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public SimulatorService(
        IServiceProvider services,
        SimulatorState state,
        HistoricalStatsService stats,
        ScriptedEventQueue queue,
        ILogger<SimulatorService> log)
    {
        _services = services;
        _state = state;
        _stats = stats;
        _queue = queue;
        _log = log;
    }

    /// <summary>Stable fingerprint of a scripted event's parameters (action + target + option values).
    /// Excludes id and status so two events with identical params collide.</summary>
    private static string FingerprintEvent(ScriptedEvent e) =>
        $"{e.Action}|{e.Location}|{e.Subprocess ?? ""}|{e.Step ?? ""}|" +
        $"{e.SlowMultiplier}|{e.FailAfterPercent}|{e.ExtraIterations}|{e.OpportunityCostHours}|" +
        $"{e.WorkingDays}|{e.DiscoveryWd}|{e.Reason ?? ""}|{e.ErrorMessage ?? ""}";

    /// <summary>Cache key = SHA256(targetMonth + mode + sorted event fingerprints). Skipped events
    /// are excluded — they represent "never fired", and a fresh run with identical params would
    /// skip them the same way, so including them would cause identical inputs to miss the cache.</summary>
    private string ComputeCacheKey()
    {
        var events = _queue.List()
            .Where(e => e.Status != "Skipped")
            .Select(FingerprintEvent)
            .OrderBy(s => s, StringComparer.Ordinal);
        var combined = $"{_state.TargetMonth}|{_state.Mode}|{string.Join(";", events)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash);
    }

    /// <summary>Clears the in-memory run cache. Exposed via an API endpoint so operators can
    /// force a fresh sim after seed changes.</summary>
    public void ClearCache()
    {
        _runCache.Clear();
        _log.LogInformation("Simulator: fast-replay cache cleared");
    }

    public int CacheEntryCount => _runCache.Count;

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
                catch (OperationCanceledException)
                {
                    // Service shutdown → exit loop. Reset-triggered cancel → keep service running;
                    // cleanup will fire on the next iteration via the ResetRequested branch.
                    if (stoppingToken.IsCancellationRequested) break;
                    _log.LogInformation("Simulator: run cancelled by reset");
                    _state.IsRunning = false;
                    _state.IsPaused = false;
                    _state.Phase = "Resetting";
                }
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

        // Each Start = fresh scenario. Purge Done/Skipped events from the queue so the history
        // panel shows only this run's events at completion, and the cache fingerprint reflects
        // just the currently-staged Pending events.
        var purged = _queue.PurgeTerminal();
        if (purged > 0)
            _log.LogInformation("Simulator: purged {Count} terminal event(s) from the queue", purged);

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

        // Fast-replay cache lookup. If this exact (month, mode, queue) was simulated before,
        // dump the cached rows and animate progress 0→100% in a few seconds instead of re-running
        // the full sim loop (which is dominated by WaitSimulatedMinutesAsync real-time waits).
        var cacheKey = ComputeCacheKey();
        if (_runCache.TryGetValue(cacheKey, out var cached))
        {
            _log.LogInformation("Simulator: fast-replay HIT for key {Key} ({Entries} entries cached) — skipping sim loop",
                cacheKey[..8], cached.EventFinalStatus.Count);
            await ReplayCachedRunAsync(cached, run, db, ct);
            return;
        }
        _log.LogInformation("Simulator: fast-replay MISS for key {Key} — running fresh", cacheKey[..8]);

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

        // Pre-seed SubprocessRun rows. TotalRequiredSteps reflects the runnable subset —
        // only steps with reference-month data (regular=2602, quarterly=2512); steps
        // absent from historical-stats are dropped from the sim entirely.
        var runRows = new Dictionary<(int loc, int sub), SubprocessRun>();
        foreach (var loc in allLocs)
        {
            foreach (var sub in allSubs)
            {
                var hasRegistry = registry.ContainsKey((loc.Id, sub.Id)) && IsEligible(sub);
                int runnableCount = 0;
                if (hasRegistry)
                {
                    var stats = _stats.Get(loc.Code, sub.Name);
                    runnableCount = registry[(loc.Id, sub.Id)]
                        .Count(s => stats?.Steps?.Any(x => x.StepName == s) == true);
                }
                var isInScope = hasRegistry && runnableCount > 0;
                var sr = new SubprocessRun
                {
                    McpRunId = run.Id,
                    LocationId = loc.Id,
                    SubprocessId = sub.Id,
                    Status = isInScope ? "Not Started" : "Not in Scope",
                    TotalRequiredSteps = runnableCount,
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
        // ConcurrentDictionary so the parallel per-location walk can read/write its own loc's clock safely.
        var locClock = new ConcurrentDictionary<int, DateTime>(allLocs.ToDictionary(l => l.Id, _ => anchor));

        // Pick DQ-blocked locations (Load Position will fail → long delay → blocks all downstream).
        // DQ blocks are a Baseline-only phenomenon: Clean skips them, Stressed leaves it to operator-scripted events.
        var dqBlockedLocs = new HashSet<int>();
        if (_state.Mode == SimMode.Baseline)
        {
            foreach (var loc in allLocs)
            {
                if (rng.NextDouble() < DqBlockRate)
                    dqBlockedLocs.Add(loc.Id);
            }
            if (dqBlockedLocs.Count == 0 && allLocs.Count > 0)
                dqBlockedLocs.Add(allLocs[rng.Next(allLocs.Count)].Id);
        }
        _log.LogInformation("Simulator: mode={Mode}, DQ blocks at {Count} locations: {Locs}",
            _state.Mode, dqBlockedLocs.Count, string.Join(", ", allLocs.Where(l => dqBlockedLocs.Contains(l.Id)).Select(l => l.Code)));

        // Critical events: operator-scripted late-discovered incidents that cause location-wide rerun.
        // Applied in Baseline and Stressed. Each event has a discoveryWd that anchors "when we found out" —
        // subs that finish before that WD get a canonical iter 2 after the fix delay; subs starting after
        // run once on fresh data (no iter 2).
        var criticalByLoc = new Dictionary<string, (ScriptedEvent Evt, DateTime DiscoveryTs, double FixHours)>();
        if (_state.Mode != SimMode.Clean)
        {
            foreach (var evt in _queue.PeekCriticalEvents())
            {
                var wd = evt.DiscoveryWd ?? 17;
                var discoveryTs = BusinessHoursCalculator.AddWorkingDays(anchor, wd - 1);
                var fixHours = evt.OpportunityCostHours ?? 24;
                criticalByLoc[evt.Location] = (evt, discoveryTs, fixHours);
                _log.LogInformation("Critical event {Id} @ {Loc}: discoveryWd={Wd} ({Ts:o}), fix={Fix}h, reason={Reason}",
                    evt.Id, evt.Location, wd, discoveryTs, fixHours, evt.Reason ?? "(unspecified)");
            }
        }

        // Main walk. Outer phase × sub is sequential (phase ordering matters for UI + DQ blocks);
        // inner per-location work runs in parallel since each location's per-(phase, sub) step is
        // independent (each has its own DbContext, Random, and locClock entry). Delivers ~5-10×
        // wall-clock speedup since WaitSimulatedMinutesAsync real-time waits now overlap across locs.
        foreach (var phase in phaseOrder)
        {
            if (!subsByPhase.TryGetValue(phase, out var phaseSubs)) continue;
            _state.Phase = phase;
            foreach (var sub in phaseSubs)
            {
                ThrowIfResetOrCancelled(ct);
                _state.CurrentSubprocess = sub.Name;

                await Parallel.ForEachAsync(
                    allLocs,
                    new ParallelOptions { MaxDegreeOfParallelism = allLocs.Count, CancellationToken = ct },
                    async (loc, innerCt) =>
                {
                    ThrowIfResetOrCancelled(innerCt);
                    if (!registry.TryGetValue((loc.Id, sub.Id), out var registrySteps) || registrySteps.Count == 0) return;

                    // Per-location scope, DbContext, Random (deterministic seed including loc.Id + sub.Id)
                    using var locScope = _services.CreateScope();
                    var locDb = locScope.ServiceProvider.GetRequiredService<GreenlightContext>();
                    var locRng = new Random(seed * 31 + loc.Id * 17 + sub.Id);

                    // Re-fetch SubprocessRun via this task's own context so EF tracks updates here
                    var sr = await locDb.SubprocessRuns
                        .FirstOrDefaultAsync(r => r.McpRunId == run.Id && r.LocationId == loc.Id && r.SubprocessId == sub.Id, innerCt);
                    if (sr is null || sr.Status != "Not Started") return;

                    _state.CurrentLocation = loc.Code; // racy but harmless — last-writer-wins display field
                    var stats = _stats.Get(loc.Code, sub.Name);

                    // Filter to steps with reference-month data — others are dropped from the sim entirely.
                    var steps = registrySteps.Where(s => stats?.Steps?.Any(x => x.StepName == s) == true).ToList();
                    if (steps.Count == 0) return;

                    // iterCount base depends on mode:
                    //   Baseline → stochastic from historical stats
                    //   Clean / Stressed → 1 iter unless scripted-fail events bump it below
                    var iterCount = _state.Mode == SimMode.Baseline ? DecideIterationCount(stats, locRng) : 1;
                    var locStart = locClock[loc.Id];

                    // Consume scripted events for this (loc, sub) — subprocess-level, not step-level.
                    // Applies in Baseline and Stressed modes.
                    ScriptedEvent? scriptedDelay = null;
                    ScriptedEvent? scriptedSlow = null;
                    ScriptedEvent? scriptedFail = null;
                    if (_state.Mode != SimMode.Clean)
                    {
                        scriptedDelay = _queue.Consume("delay", loc.Code, sub.Name, null)
                                     ?? _queue.Consume("delay", loc.Code, null, null);
                        scriptedSlow = _queue.Consume("slow", loc.Code, sub.Name, null);
                        scriptedFail = _queue.Consume("fail", loc.Code, sub.Name, null);
                    }

                    // Apply scripted delay: advance locStart by the requested working days
                    if (scriptedDelay?.WorkingDays is not null && scriptedDelay.WorkingDays > 0)
                    {
                        var original = locStart;
                        locStart = BusinessHoursCalculator.AddWorkingDays(locStart, scriptedDelay.WorkingDays.Value);
                        _log.LogInformation("Scripted delay {Days} WD at {Loc}/{Sub}: {Orig:o} -> {New:o}",
                            scriptedDelay.WorkingDays.Value, loc.Code, sub.Name, original, locStart);
                        _queue.MarkDone(scriptedDelay.Id);
                    }

                    // Scripted slow multiplier applies uniformly to every step in the subprocess
                    var slowMultiplier = scriptedSlow?.SlowMultiplier ?? 1.0;
                    if (scriptedSlow is not null) _queue.MarkDone(scriptedSlow.Id);

                    // Compute scripted-fail step index based on cumulative runtime hitting failAfterPercent × sub total.
                    // After slow is applied, durations scale but the percentage threshold behaves the same, so compute
                    // from the raw avgMinutes (multiplier cancels out in ratio terms).
                    int? scriptedFailStepIndex = null;
                    if (scriptedFail is not null)
                    {
                        var stepAvgMins = steps.Select(s => stats!.Steps.First(x => x.StepName == s).AvgMinutes).ToList();
                        var totalMin = stepAvgMins.Sum();
                        var threshold = totalMin * (scriptedFail.FailAfterPercent ?? 0.8);
                        double cum = 0;
                        for (int i = 0; i < stepAvgMins.Count; i++)
                        {
                            cum += stepAvgMins[i];
                            if (cum >= threshold) { scriptedFailStepIndex = i; break; }
                        }
                        if (scriptedFailStepIndex is null) scriptedFailStepIndex = stepAvgMins.Count - 1;

                        var wantExtras = scriptedFail.ExtraIterations ?? 1;
                        iterCount = Math.Max(iterCount, wantExtras + 1);
                    }

                    // Critical event: main walk runs iter 1 normally. Iter 2 gets generated in a post-walk
                    // rerun pass (one 24h gap per affected location, then all subs re-run sequentially).
                    bool isCriticalAffected = criticalByLoc.ContainsKey(loc.Code);

                    // DQ block (Baseline-only — dqBlockedLocs was populated only if mode == Baseline).
                    // First iter fails with DQ-specific error; long business-hour gap before the clean rerun.
                    var isDqBlock = dqBlockedLocs.Contains(loc.Id) && sub.Name == DqBlockSubprocess;
                    if (isDqBlock) iterCount = Math.Max(iterCount, 2);

                    // Operator-override scenario (Baseline-only). Biases toward the shortest steps.
                    var useOverride = _state.Mode == SimMode.Baseline
                                      && locRng.NextDouble() < OverrideRate
                                      && steps.Count >= 2;
                    var overrideStepIndices = new HashSet<int>();
                    if (useOverride)
                    {
                        var skipCount = locRng.Next(1, Math.Min(3, steps.Count));
                        var eligiblePool = Enumerable.Range(0, steps.Count)
                            .OrderBy(i => stats!.Steps.First(s => s.StepName == steps[i]).AvgMinutes)
                            .Take(Math.Max(1, steps.Count / 2))
                            .ToList();
                        while (overrideStepIndices.Count < skipCount && overrideStepIndices.Count < eligiblePool.Count)
                            overrideStepIndices.Add(eligiblePool[locRng.Next(eligiblePool.Count)]);
                    }

                    // Hold event: check once per (loc, sub) before any iterations run
                    if (_state.Mode != SimMode.Clean)
                    {
                        var hold = _queue.FindHold(loc.Code, sub.Name);
                        if (hold is not null && !_queue.IsReleased(hold.Id))
                        {
                            _log.LogInformation("Holding subprocess {Loc}/{Sub} (event {Id}) — waiting for release",
                                loc.Code, sub.Name, hold.Id);
                            while (!_queue.IsReleased(hold.Id))
                            {
                                ThrowIfResetOrCancelled(innerCt);
                                await Task.Delay(500, innerCt);
                            }
                            _log.LogInformation("Hold released: {Loc}/{Sub}", loc.Code, sub.Name);
                            _queue.MarkDone(hold.Id);
                        }
                    }

                    // Pass 1: generate all entries (iterations × steps)
                    var entries = new List<ProcessLogEntry>();
                    var iterOutcomes = new List<bool>();
                    DateTime iterStart = locStart;
                    for (int iter = 1; iter <= iterCount; iter++)
                    {
                        bool isLast = iter == iterCount;
                        bool shouldSucceed = isLast;
                        bool isSupersede = false;
                        int? failStepIndex = null;
                        string? scriptedErrorOverride = null;
                        double? scriptedOppCostHours = null;

                        if (!shouldSucceed)
                        {
                            if (scriptedFail is not null && scriptedFailStepIndex.HasValue)
                            {
                                failStepIndex = scriptedFailStepIndex;
                                scriptedErrorOverride = scriptedFail.ErrorMessage
                                    ?? $"{steps[scriptedFailStepIndex.Value]} execution terminated unexpectedly";
                                scriptedOppCostHours = scriptedFail.OpportunityCostHours;
                            }
                            else if (_state.Mode == SimMode.Baseline)
                            {
                                // Stochastic: 40% supersede (all complete but rerun), 60% fail at a random step
                                isSupersede = locRng.NextDouble() < SupersedeReruns;
                                if (!isSupersede) failStepIndex = locRng.Next(steps.Count);
                            }
                        }

                        // DQ block: force first iteration to fail at the load step with a DQ-specific error
                        bool useDqError = isDqBlock && iter == 1;
                        if (useDqError) { failStepIndex = 0; isSupersede = false; scriptedErrorOverride = null; }

                        // Wait until the simulated iteration start — advance the clock
                        await SetSimulatedNow(iterStart, innerCt);

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
                                var manualDurMin = Math.Max(MinStepMinutes, SampleStepDuration(stats!, stepName, locRng) * 0.7);
                                await WaitSimulatedMinutesAsync(manualDurMin, innerCt);
                                stepCursor = stepCursor.AddMinutes(manualDurMin);
                                continue; // no ProcessLogEntry row
                            }

                            // Base step duration × scripted slow multiplier (uniform across all steps in the sub)
                            var durMin = SampleStepDuration(stats!, stepName, locRng);
                            if (slowMultiplier != 1.0)
                                durMin = Math.Max(MinStepMinutes, durMin * slowMultiplier);
                            bool thisStepFails = !shouldSucceed && failStepIndex.HasValue && i == failStepIndex.Value;

                            var stepStart = stepCursor;
                            await WaitSimulatedMinutesAsync(durMin, innerCt);
                            var stepEnd = stepCursor.AddMinutes(durMin);
                            stepCursor = stepEnd;

                            string state;
                            string? err = null;
                            if (thisStepFails)
                            {
                                state = "Failed";
                                err = useDqError ? DqBlockErrors[locRng.Next(DqBlockErrors.Length)]
                                      : scriptedErrorOverride ?? PickError(stats, locRng);
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
                                await locDb.SaveChangesAsync(innerCt);
                            }
                        }

                        iterOutcomes.Add(shouldSucceed);

                        // If another iteration follows, advance the clock by business-hour opportunity cost.
                        // Critical events compute the gap such that iter 2 starts at (discoveryTs + fixHours).
                        if (iter < iterCount)
                        {
                            double opCostHours;
                            if (isDqBlock && iter == 1)
                            {
                                opCostHours = DqBlockMinHours + locRng.NextDouble() * (DqBlockMaxHours - DqBlockMinHours);
                            }
                            else if (scriptedOppCostHours.HasValue)
                            {
                                opCostHours = scriptedOppCostHours.Value;
                            }
                            else
                            {
                                opCostHours = 0.25 + locRng.NextDouble() * 3.0; // ~15 min to 3h normal delay
                            }
                            iterStart = BusinessHoursCalculator.AddBusinessHours(stepCursor, opCostHours);
                        }
                        else
                        {
                            iterStart = stepCursor;
                        }
                    }

                    // Mark scripted fail event (if any) as Done now that the iteration has fired
                    if (scriptedFail is not null) _queue.MarkDone(scriptedFail.Id);

                    // Pass 2: compute per-entry runtime fields using canonical-iteration rule
                    // Canonical iteration = highest iter where no step has Failed/Stopped/Unfinished
                    // Note: with overrides, the last iteration has fewer rows (some steps skipped) but still counts as canonical
                    int? canonIter = DetermineCanonicalIteration(entries);
                    ApplyRuntimeFields(entries, canonIter);

                    // Pass 3: set NextStarted (within (loc, sub, iteration))
                    SetNextStarted(entries);

                    // Persist the log entries in a batch
                    locDb.ProcessLogEntries.AddRange(entries);

                    // Create OperatorOverride rows for any skipped steps on the canonical iteration
                    if (useOverride && canonIter.HasValue)
                    {
                        foreach (var idx in overrideStepIndices)
                        {
                            locDb.OperatorOverrides.Add(new OperatorOverride
                            {
                                McpRunId = run.Id,
                                LocationId = loc.Id,
                                SubprocessId = sub.Id,
                                StepName = steps[idx],
                                Action = "complete",
                                Reason = OverrideReasons[locRng.Next(OverrideReasons.Length)],
                                TicketRef = locRng.NextDouble() < 0.6 ? $"INC{14500000 + locRng.Next(100000)}" : null,
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

                    _state.IncrementCompletedSubprocesses();
                    locClock[loc.Id] = iterStart;
                    await locDb.SaveChangesAsync(innerCt);
                });
            }
        }

        // After the parallel main walk, runRows (built from the outer db during pre-seed) has stale
        // in-memory Status values — the per-loc DbContexts updated the DB but not these references.
        // Reload so the critical-event rerun pass below sees Status="Completed" correctly.
        var freshRuns = await db.SubprocessRuns.AsNoTracking()
            .Where(r => r.McpRunId == run.Id).ToListAsync(ct);
        foreach (var fr in freshRuns) runRows[(fr.LocationId, fr.SubprocessId)] = fr;

        // ── Critical-event second pass: after the main walk, each affected location gets ONE 24h gap,
        // then every sub at that loc re-runs sequentially (iter 2 becomes canonical). Locations are
        // independent here — parallelize across them with their own DbContext + RNG, same pattern as
        // the main walk above.
        await Parallel.ForEachAsync(
            criticalByLoc,
            new ParallelOptions { MaxDegreeOfParallelism = allLocs.Count, CancellationToken = ct },
            async (kvp, innerCt) =>
        {
            var (locCode, criticalEntry) = kvp;
            var loc = allLocs.FirstOrDefault(l => l.Code == locCode);
            if (loc is null) return;

            using var locScope = _services.CreateScope();
            var locDb = locScope.ServiceProvider.GetRequiredService<GreenlightContext>();
            var locRng = new Random(seed * 31 + loc.Id * 17 + 99991); // rerun-pass-distinct seed

            // One 24h gap at this location before the rerun pass begins.
            var gapStart = locClock[loc.Id];
            var rerunStart = BusinessHoursCalculator.AddBusinessHours(gapStart, criticalEntry.FixHours);
            locClock[loc.Id] = rerunStart;
            DateTime cursor = rerunStart;
            _log.LogInformation("Critical rerun at {Loc}: {GapStart:o} + {FixHours}h biz = {RerunStart:o}",
                loc.Code, gapStart, criticalEntry.FixHours, rerunStart);

            // Walk phases × subs in the same order as the main pass; emit iter 2 entries for this loc only
            foreach (var phase in phaseOrder)
            {
                if (!subsByPhase.TryGetValue(phase, out var phaseSubs)) continue;
                foreach (var sub in phaseSubs)
                {
                    if (!runRows.TryGetValue((loc.Id, sub.Id), out var sr) || sr.Status != "Completed") continue;
                    if (!registry.TryGetValue((loc.Id, sub.Id), out var registrySteps) || registrySteps.Count == 0) continue;
                    var stats = _stats.Get(loc.Code, sub.Name);
                    var steps = registrySteps.Where(s => stats?.Steps?.Any(x => x.StepName == s) == true).ToList();
                    if (steps.Count == 0) continue;

                    // Re-fetch SubprocessRun via locDb so we track updates here (not on stale outer-db instance)
                    var srLocal = await locDb.SubprocessRuns
                        .FirstOrDefaultAsync(r => r.McpRunId == run.Id && r.LocationId == loc.Id && r.SubprocessId == sub.Id, innerCt);
                    if (srLocal is null) continue;

                    // Determine the next iteration number for this (loc, sub) — appended after existing iter 1
                    var existingMaxIter = await locDb.ProcessLogEntries
                        .Where(e => e.McpRunId == run.Id && e.LocationId == loc.Id
                                    && (e.ScriptName == BuildScriptName(loc.Code, sub.Name, 1)
                                        || e.ScriptName.StartsWith(sub.Name.Replace(" ", "_") + "_" + loc.Code + "_MCP rr")))
                        .MaxAsync(e => (int?)e.Iteration, innerCt) ?? 0;
                    int nextIter = existingMaxIter + 1;

                    // Sample slow multiplier if a scripted slow for this (loc, sub) was applied during main walk
                    // (it was already consumed, so the rerun uses normal speed — matches "fresh data" semantics)
                    var rerunEntries = new List<ProcessLogEntry>();
                    foreach (var stepName in steps)
                    {
                        var durMin = SampleStepDuration(stats!, stepName, locRng);
                        var stepStart = cursor;
                        await WaitSimulatedMinutesAsync(durMin, innerCt);
                        var stepEnd = cursor.AddMinutes(durMin);
                        cursor = stepEnd;
                        rerunEntries.Add(new ProcessLogEntry
                        {
                            McpRunId = run.Id,
                            LocationId = loc.Id,
                            Process = "MCP",
                            ScriptName = BuildScriptName(loc.Code, sub.Name, nextIter),
                            StepName = stepName,
                            StateName = "Completed",
                            StartMarker = $"START {stepName}",
                            EndMarker = $"END {stepName}",
                            StartedAt = stepStart,
                            EndedAt = stepEnd,
                            Iteration = nextIter,
                            TotalRuntimeHours = (stepEnd - stepStart).TotalHours,
                            EfficientRuntimeHours = (stepEnd - stepStart).TotalHours, // iter 2 is canonical
                            FailedRuntimeHours = 0,
                            E2ERuntimeHours = (stepEnd - stepStart).TotalHours,
                        });
                    }

                    // Mark prior iter 1 entries as superseded: move TotalRuntimeHours from Efficient to Failed.
                    // The last entry of iter 1 gets the OpportunityCostHours for the 24h+ fix gap.
                    var iter1DbEntries = await locDb.ProcessLogEntries
                        .Where(e => e.McpRunId == run.Id && e.LocationId == loc.Id && e.Iteration == 1
                                    && (e.ScriptName == BuildScriptName(loc.Code, sub.Name, 1)
                                        || e.ScriptName.StartsWith(sub.Name.Replace(" ", "_") + "_" + loc.Code + "_MCP")))
                        .OrderBy(e => e.StartedAt)
                        .ToListAsync(innerCt);
                    foreach (var e in iter1DbEntries)
                    {
                        e.EfficientRuntimeHours = 0;
                        e.FailedRuntimeHours = e.TotalRuntimeHours;
                        e.InefficientRuntimeHours = e.FailedRuntimeHours;
                    }
                    locDb.ProcessLogEntries.AddRange(rerunEntries);

                    // Update SubprocessRun's CompletedAt to reflect the iter 2 end
                    srLocal.CompletedAt = cursor;
                    if (srLocal.StartedAt.HasValue)
                        srLocal.ElapsedMinutes = (srLocal.CompletedAt.Value - srLocal.StartedAt.Value).TotalMinutes;
                    await locDb.SaveChangesAsync(innerCt);
                }
            }
            locClock[loc.Id] = cursor;

            // Attribute the fix duration ONCE at the location level — put it on the latest iter 1 entry
            // so the aggregate "opportunity cost for this location" = fixHours, not fixHours × affected-sub count.
            var lastIter1AtLoc = await locDb.ProcessLogEntries
                .Where(e => e.McpRunId == run.Id && e.LocationId == loc.Id && e.Iteration == 1)
                .OrderByDescending(e => e.EndedAt)
                .FirstOrDefaultAsync(innerCt);
            if (lastIter1AtLoc is not null)
            {
                lastIter1AtLoc.OpportunityCostHours = criticalEntry.FixHours;
                lastIter1AtLoc.InefficientRuntimeHours = lastIter1AtLoc.FailedRuntimeHours + criticalEntry.FixHours;
                lastIter1AtLoc.E2ERuntimeHours = lastIter1AtLoc.TotalRuntimeHours + criticalEntry.FixHours;
                await locDb.SaveChangesAsync(innerCt);
            }
        });

        // Mark all critical events as Done now that the rerun has processed them
        foreach (var c in criticalByLoc.Values) _queue.MarkDone(c.Evt.Id);

        // Any scripted events still Pending/Firing at this point never fired — either mode=Clean
        // bypassed them, or their (loc, sub) target was not reached in this run. Mark Skipped so
        // the operator can see the terminal state instead of stale "pending" in the queue panel.
        var skippedCount = _queue.MarkAllUnfiredSkipped();
        if (skippedCount > 0)
            _log.LogInformation("Simulator: marked {Count} scripted event(s) as Skipped at end of run", skippedCount);

        // Finalize run — all subprocesses end Completed (some via overrides, some with earlier failed iterations in their history)
        var anyOverrides = await db.SubprocessRuns.AnyAsync(sr => sr.McpRunId == run.Id && sr.HasOverrides, ct);
        run.Status = anyOverrides ? "Completed with Overrides" : "Completed";
        run.EndDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Save this run to the fast-replay cache keyed on the (month, mode, queue) fingerprint
        // we computed at the top. Next Start with the same inputs replays instantly.
        await SaveRunToCacheAsync(cacheKey, run.Id, db, ct);

        _state.Phase = "Complete";
        _state.IsRunning = false;
        _state.CurrentSubprocess = null;
        _state.CurrentLocation = null;
        _log.LogInformation("Simulator: finished run {ReportMonth} — {Status}", targetMonth, run.Status);
    }

    /// <summary>Serialize the rows produced by this run into the in-memory cache.</summary>
    private async Task SaveRunToCacheAsync(string cacheKey, int runId, GreenlightContext db, CancellationToken ct)
    {
        var subRuns = await db.SubprocessRuns.AsNoTracking().Where(sr => sr.McpRunId == runId).ToListAsync(ct);
        var entries = await db.ProcessLogEntries.AsNoTracking().Where(e => e.McpRunId == runId).ToListAsync(ct);
        var overrides = await db.OperatorOverrides.AsNoTracking().Where(o => o.McpRunId == runId).ToListAsync(ct);

        // Record each event's final terminal status so replay can restore Done vs Skipped correctly
        // for events that were Pending at the start of this run.
        var eventStatus = _queue.List().ToDictionary(FingerprintEvent, e => e.Status);

        _runCache[cacheKey] = new CachedRun
        {
            SubRunsJson = JsonSerializer.Serialize(subRuns, CacheJsonOpts),
            EntriesJson = JsonSerializer.Serialize(entries, CacheJsonOpts),
            OverridesJson = JsonSerializer.Serialize(overrides, CacheJsonOpts),
            EventFinalStatus = eventStatus,
            TotalSubprocesses = _state.TotalSubprocesses,
            FinalRunStatus = await db.McpRuns.Where(r => r.Id == runId).Select(r => r.Status).FirstAsync(ct),
        };
        _log.LogInformation("Simulator: cached run under key {Key} — {SubRuns} subruns, {Entries} log entries, {Ovr} overrides",
            cacheKey[..8], subRuns.Count, entries.Count, overrides.Count);
    }

    /// <summary>Replay a cached run: dump rows to the new McpRun, animate progress 0→100% in a few
    /// seconds, then finalize. Skips all sim-loop work (sampling, business-hour math, step waits).</summary>
    private async Task ReplayCachedRunAsync(CachedRun cached, McpRun run, GreenlightContext db, CancellationToken ct)
    {
        _state.Phase = "Replaying (cached)";
        _state.TotalSubprocesses = cached.TotalSubprocesses;
        _state.CompletedSubprocesses = 0;

        var subRuns = JsonSerializer.Deserialize<List<SubprocessRun>>(cached.SubRunsJson, CacheJsonOpts) ?? new();
        var entries = JsonSerializer.Deserialize<List<ProcessLogEntry>>(cached.EntriesJson, CacheJsonOpts) ?? new();
        var overrides = JsonSerializer.Deserialize<List<OperatorOverride>>(cached.OverridesJson, CacheJsonOpts) ?? new();

        // Patch McpRunId + reset PK so EF inserts as new rows under the current run
        foreach (var sr in subRuns) { sr.Id = 0; sr.McpRunId = run.Id; }
        foreach (var e in entries) { e.Id = 0; e.McpRunId = run.Id; }
        foreach (var o in overrides) { o.Id = 0; o.McpRunId = run.Id; }

        db.SubprocessRuns.AddRange(subRuns);
        db.ProcessLogEntries.AddRange(entries);
        db.OperatorOverrides.AddRange(overrides);
        await db.SaveChangesAsync(ct);

        // Animate progress bar 0 → total over ~4 seconds. Respect pause/reset/cancel.
        const int AnimDurationMs = 4000;
        var total = Math.Max(1, cached.TotalSubprocesses);
        var stepMs = Math.Max(10, AnimDurationMs / total);
        for (int i = 1; i <= total; i++)
        {
            ThrowIfResetOrCancelled(ct);
            while (_state.IsPaused && !ct.IsCancellationRequested && !_state.ResetRequested)
                await Task.Delay(100, ct);
            _state.CompletedSubprocesses = i;
            await Task.Delay(stepMs, ct);
        }

        // Finalize run row
        run.Status = cached.FinalRunStatus;
        run.EndDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Restore scripted event terminal statuses from the cached run (Done events get MarkDone,
        // any leftovers become Skipped via MarkAllUnfiredSkipped).
        foreach (var evt in _queue.List().Where(e => e.Status == "Pending" || e.Status == "Firing"))
        {
            var fp = FingerprintEvent(evt);
            if (cached.EventFinalStatus.TryGetValue(fp, out var finalStatus) && finalStatus == "Done")
                _queue.MarkDone(evt.Id);
        }
        _queue.MarkAllUnfiredSkipped();

        _state.Phase = "Complete";
        _state.IsRunning = false;
        _state.CurrentSubprocess = null;
        _state.CurrentLocation = null;
        _log.LogInformation("Simulator: fast-replay finished — {Status}", run.Status);
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

    // Caller MUST pre-filter the step list to those present in stats.Steps.
    // No phase fallback — steps without reference-month data are skipped upstream, not simulated.
    private static double SampleStepDuration(HistoricalStatsService.SubprocessStats stats, string stepName, Random rng)
    {
        var s = stats.Steps.First(x => x.StepName == stepName);
        var noise = 1.0 + (rng.NextDouble() * 2 - 1) * NoiseRatio;
        return Math.Max(MinStepMinutes, s.AvgMinutes * noise);
    }

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

    /// <summary>Throws OperationCanceledException if the service is stopping OR the operator requested a reset.</summary>
    private void ThrowIfResetOrCancelled(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_state.ResetRequested) throw new OperationCanceledException("reset requested");
    }

    /// <summary>Wait real time corresponding to simMinutes at the current speed multiplier.</summary>
    private async Task WaitSimulatedMinutesAsync(double simMinutes, CancellationToken ct)
    {
        var realMs = simMinutes * 60_000 / _state.SpeedMultiplier;
        var remaining = realMs;
        const int tickMs = 200;
        while (remaining > 0)
        {
            ThrowIfResetOrCancelled(ct);
            while (_state.IsPaused && !ct.IsCancellationRequested && !_state.ResetRequested)
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
            db.OperatorOverrides.RemoveRange(db.OperatorOverrides.Where(o => o.McpRunId == run.Id));
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
