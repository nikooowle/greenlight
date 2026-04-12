using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Background service that simulates a live MCP run progressing through subprocesses.
/// Uses real production data patterns for realistic timing and failure rates.
/// </summary>
public class SimulatorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly SimulatorState _state;
    private readonly ILogger<SimulatorService> _log;
    private readonly Random _rng = new();

    // Realistic durations per phase (in simulated minutes)
    private static readonly Dictionary<string, (int Min, int Max)> PhaseDurations = new()
    {
        ["DataIngestion"] = (2, 15),
        ["Processing"] = (10, 120),
        ["Reporting"] = (5, 30),
    };

    // Realistic error messages sampled from production
    private static readonly string[] ErrorMessages =
    [
        "Exception Info: Workflow finished with errors. (line: 40)",
        "Exception Info: User has no access to database (line: 56)",
        "Exception from QRM: Cell contains no data. -- Details available: Y.",
        "Exception Info: Import failed - file in use by another process.",
        "Exception Info: Timeout expired waiting for database lock.",
        "Exception Info: Network connectivity lost during data transfer.",
        "Exception from QRM: Strategy file not found in expected path.",
        "Exception Info: Memory allocation failed during valuation run.",
    ];

    // Which subprocesses each location actually runs (from production data)
    // null = all standard subprocesses apply
    private static readonly Dictionary<string, HashSet<string>?> LocationScope = new()
    {
        ["AUDB"] = null, ["BEDB"] = null, ["BEGT"] = null, ["DEDB"] = null,
        ["DEGT"] = null, ["ESDB"] = null, ["FRDB"] = null, ["ICDB"] = null,
        ["ITDB"] = null, ["LUDB"] = null, ["NLBTR"] = null, ["NLRB"] = null,
        ["PLDB"] = null, ["RODB"] = null, ["TRDB"] = null, ["WBUS"] = null,
    };

    public SimulatorService(IServiceProvider services, SimulatorState state, ILogger<SimulatorService> log)
    {
        _services = services;
        _state = state;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Simulator service started. Waiting for start signal...");

        // Auto-start a simulation on launch
        _state.StartRequested = true;

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

        // 1. Create a new MCP run
        var latestRun = await db.McpRuns.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).FirstOrDefaultAsync(ct);
        int nextYear = latestRun?.Year ?? 2026;
        int nextMonth = (latestRun?.Month ?? 0) + 1;
        if (nextMonth > 12) { nextMonth = 1; nextYear++; }

        var reportMonth = $"{(nextYear % 100):D2}{nextMonth:D2}";

        // Check if simulated run already exists, clean it up
        var existing = await db.McpRuns.FirstOrDefaultAsync(r => r.ReportMonth == reportMonth, ct);
        if (existing != null)
        {
            db.SubprocessRuns.RemoveRange(db.SubprocessRuns.Where(sr => sr.McpRunId == existing.Id));
            db.ProcessLogEntries.RemoveRange(db.ProcessLogEntries.Where(p => p.McpRunId == existing.Id));
            db.McpRuns.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        var run = new McpRun
        {
            ReportMonth = reportMonth,
            Year = nextYear,
            Month = nextMonth,
            Status = "Running",
            StartDate = DateTime.UtcNow,
            EomDate = new DateTime(nextYear, nextMonth, DateTime.DaysInMonth(nextYear, nextMonth)),
        };
        db.McpRuns.Add(run);
        await db.SaveChangesAsync(ct);

        _state.CurrentRunId = run.Id;
        _state.IsRunning = true;
        _state.Phase = "Initializing";
        _log.LogInformation("Simulator: Created run {ReportMonth} (ID {Id})", reportMonth, run.Id);

        // 2. Get locations and subprocesses
        var locations = await db.Locations.Where(l => l.InScope).OrderBy(l => l.Code).ToListAsync(ct);
        var subprocesses = await db.Subprocesses.OrderBy(s => s.Phase).ThenBy(s => s.DisplayOrder).ToListAsync(ct);

        // Group subprocesses by phase for ordered execution
        var phases = new[] { "DataIngestion", "Processing", "Reporting" };
        var subsByPhase = subprocesses.GroupBy(s => s.Phase).ToDictionary(g => g.Key, g => g.ToList());

        // 3. Pre-create SubprocessRun rows as "Not in Scope" or "Not Started"
        // Use real seed data to determine which location+subprocess combos are in scope
        var seedRunId = latestRun?.Id ?? 0;
        var seedMatrix = await db.SubprocessRuns
            .Where(sr => sr.McpRunId == seedRunId)
            .Select(sr => new { sr.LocationId, sr.SubprocessId })
            .ToListAsync(ct);
        var inScopeSet = new HashSet<(int, int)>(seedMatrix.Select(s => (s.LocationId, s.SubprocessId)));

        var allRuns = new List<SubprocessRun>();
        foreach (var loc in locations)
        {
            foreach (var sub in subprocesses)
            {
                var isInScope = inScopeSet.Contains((loc.Id, sub.Id));
                var sr = new SubprocessRun
                {
                    McpRunId = run.Id,
                    LocationId = loc.Id,
                    SubprocessId = sub.Id,
                    Status = isInScope ? "Not Started" : "Not in Scope",
                    TotalRequiredSteps = isInScope ? _rng.Next(2, 6) : 0,
                    CompletedSteps = 0,
                };
                allRuns.Add(sr);
                db.SubprocessRuns.Add(sr);
            }
        }
        await db.SaveChangesAsync(ct);

        var inScopeRuns = allRuns.Where(r => r.Status == "Not Started").ToList();
        _state.TotalSubprocesses = inScopeRuns.Count;
        _state.CompletedSubprocesses = 0;

        _log.LogInformation("Simulator: {Total} subprocess runs to simulate ({InScope} in-scope)",
            allRuns.Count, inScopeRuns.Count);

        // 4. Simulate phase by phase
        foreach (var phase in phases)
        {
            if (!subsByPhase.ContainsKey(phase)) continue;
            _state.Phase = phase;

            var phaseSubprocesses = subsByPhase[phase];

            foreach (var sub in phaseSubprocesses)
            {
                ct.ThrowIfCancellationRequested();
                _state.CurrentSubprocess = sub.Name;

                // Get all location runs for this subprocess that are in scope
                var subRuns = inScopeRuns.Where(r => r.SubprocessId == sub.Id).ToList();
                if (subRuns.Count == 0) continue;

                // Transition all to "Running"
                foreach (var sr in subRuns)
                {
                    sr.Status = "Running";
                    sr.StartedAt = DateTime.UtcNow;
                    _state.CurrentLocation = locations.First(l => l.Id == sr.LocationId).Code;
                }
                await db.SaveChangesAsync(ct);

                // Simulate duration
                var (minDur, maxDur) = PhaseDurations.GetValueOrDefault(phase, (5, 30));
                var simMinutes = _rng.Next(minDur, maxDur + 1);

                // Simulate step-by-step progress
                for (int step = 1; step <= subRuns[0].TotalRequiredSteps; step++)
                {
                    await WaitSimulatedMinutesAsync(simMinutes / subRuns[0].TotalRequiredSteps, ct);

                    foreach (var sr in subRuns)
                    {
                        sr.CompletedSteps = step;
                    }
                    await db.SaveChangesAsync(ct);
                }

                // Determine outcome per location
                foreach (var sr in subRuns)
                {
                    var locCode = locations.First(l => l.Id == sr.LocationId).Code;

                    // ~8% failure rate
                    if (_rng.NextDouble() < 0.08)
                    {
                        sr.Status = "Failed";
                        sr.CompletedAt = DateTime.UtcNow;
                        sr.ElapsedMinutes = (sr.CompletedAt.Value - sr.StartedAt!.Value).TotalMinutes;
                        sr.CompletedSteps = Math.Max(0, sr.CompletedSteps - 1);

                        // Create a failed log entry
                        db.ProcessLogEntries.Add(new ProcessLogEntry
                        {
                            McpRunId = run.Id,
                            LocationId = sr.LocationId,
                            Process = "MCP",
                            ScriptName = $"{sub.Name.Replace(" ", "_")}_{locCode}_MCP",
                            StepName = $"step_{sr.CompletedSteps + 1}",
                            StateName = "Failed",
                            StartedAt = sr.StartedAt,
                            EndedAt = sr.CompletedAt,
                            Iteration = 1,
                            TotalRuntimeHours = sr.ElapsedMinutes.Value / 60.0,
                            FailedRuntimeHours = sr.ElapsedMinutes.Value / 60.0,
                            ErrorMessage = ErrorMessages[_rng.Next(ErrorMessages.Length)],
                        });
                    }
                    else
                    {
                        sr.Status = "Completed";
                        sr.CompletedAt = DateTime.UtcNow;
                        sr.ElapsedMinutes = (sr.CompletedAt.Value - sr.StartedAt!.Value).TotalMinutes;

                        db.ProcessLogEntries.Add(new ProcessLogEntry
                        {
                            McpRunId = run.Id,
                            LocationId = sr.LocationId,
                            Process = "MCP",
                            ScriptName = $"{sub.Name.Replace(" ", "_")}_{locCode}_MCP",
                            StepName = $"step_final",
                            StateName = "Completed",
                            StartedAt = sr.StartedAt,
                            EndedAt = sr.CompletedAt,
                            Iteration = 1,
                            TotalRuntimeHours = sr.ElapsedMinutes.Value / 60.0,
                            EfficientRuntimeHours = sr.ElapsedMinutes.Value / 60.0,
                        });
                    }

                    _state.CompletedSubprocesses++;
                }

                await db.SaveChangesAsync(ct);
            }
        }

        // 5. Finalize run
        run.Status = inScopeRuns.Any(r => r.Status == "Failed") ? "Completed with Failures" : "Completed";
        run.EndDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _state.Phase = "Complete";
        _state.IsRunning = false;
        _state.CurrentSubprocess = null;
        _state.CurrentLocation = null;

        _log.LogInformation("Simulator: Run {ReportMonth} finished — {Status}", reportMonth, run.Status);
    }

    /// <summary>
    /// Wait for a simulated number of minutes, respecting speed multiplier and pause state.
    /// </summary>
    private async Task WaitSimulatedMinutesAsync(double simMinutes, CancellationToken ct)
    {
        var realMs = (simMinutes * 60_000) / _state.SpeedMultiplier;
        var remaining = realMs;
        const int tickMs = 200;

        while (remaining > 0)
        {
            ct.ThrowIfCancellationRequested();

            // Pause support
            while (_state.IsPaused && !ct.IsCancellationRequested)
                await Task.Delay(200, ct);

            var wait = Math.Min(remaining, tickMs);
            await Task.Delay((int)wait, ct);
            remaining -= wait;

            // Re-check speed in case it changed mid-wait
            realMs = (simMinutes * 60_000) / _state.SpeedMultiplier;
        }
    }

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

        _log.LogInformation("Simulator: Cleaned up simulated run {Id}", runId);
    }
}
