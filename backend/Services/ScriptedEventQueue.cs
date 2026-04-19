namespace Backend.Services;

/// <summary>
/// In-memory queue of operator-scripted events. Consumed by the SimulatorService in Stressed mode
/// to force specific scenarios (slow steps, forced failures, start delays, mid-run holds) at
/// operator-chosen (location, subprocess, step) targets.
///
/// Events live for the lifetime of the process. No persistence.
/// </summary>
public class ScriptedEventQueue
{
    private readonly object _lock = new();
    private readonly List<ScriptedEvent> _events = new();
    private int _nextId = 1;

    public int Add(ScriptedEvent evt)
    {
        lock (_lock)
        {
            evt.Id = _nextId++;
            evt.Status = "Pending";
            _events.Add(evt);
            return evt.Id;
        }
    }

    public List<ScriptedEvent> List()
    {
        lock (_lock) return _events.Select(Clone).ToList();
    }

    public bool Remove(int id)
    {
        lock (_lock) return _events.RemoveAll(e => e.Id == id) > 0;
    }

    public bool Release(int id)
    {
        lock (_lock)
        {
            var e = _events.FirstOrDefault(x => x.Id == id);
            if (e is null || e.Action != "hold") return false;
            e.IsReleased = true;
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock) _events.Clear();
    }

    /// <summary>Find a pending event that matches the given target + action. Marks it as firing.</summary>
    public ScriptedEvent? Consume(string action, string location, string? subprocess, string? step)
    {
        lock (_lock)
        {
            var match = _events.FirstOrDefault(e =>
                e.Status == "Pending" &&
                e.Action == action &&
                e.Location == location &&
                (e.Subprocess is null || e.Subprocess == subprocess) &&
                (e.Step is null || e.Step == step));
            if (match is null) return null;
            match.Status = "Firing";
            return Clone(match);
        }
    }

    public void MarkDone(int id)
    {
        lock (_lock)
        {
            var e = _events.FirstOrDefault(x => x.Id == id);
            if (e is not null) e.Status = "Done";
        }
    }

    /// <summary>Peek (without consuming) for a hold event on this subprocess. Holds are subprocess-level.</summary>
    public ScriptedEvent? FindHold(string location, string subprocess)
    {
        lock (_lock)
        {
            return _events.FirstOrDefault(e =>
                e.Action == "hold" &&
                e.Status != "Done" &&
                e.Location == location &&
                e.Subprocess == subprocess);
        }
    }

    public bool IsReleased(int id)
    {
        lock (_lock) return _events.FirstOrDefault(e => e.Id == id)?.IsReleased ?? true;
    }

    /// <summary>List non-terminal critical events. Flips Pending → Firing on first peek so the
    /// UI shows them as actively in-flight during the rerun pass; MarkDone finalizes them later.</summary>
    public List<ScriptedEvent> PeekCriticalEvents()
    {
        lock (_lock)
        {
            var result = new List<ScriptedEvent>();
            foreach (var e in _events.Where(e => e.Action == "critical" && e.Status != "Done" && e.Status != "Skipped"))
            {
                if (e.Status == "Pending") e.Status = "Firing";
                result.Add(Clone(e));
            }
            return result;
        }
    }

    /// <summary>Terminal-mark any still-unfired events at end of run. Handles Clean mode (where
    /// scripted events are bypassed entirely) and events whose (loc, sub) target the sim never reached.
    /// Returns the count of events transitioned.</summary>
    public int MarkAllUnfiredSkipped()
    {
        lock (_lock)
        {
            var count = 0;
            foreach (var e in _events.Where(e => e.Status == "Pending" || e.Status == "Firing"))
            {
                e.Status = "Skipped";
                count++;
            }
            return count;
        }
    }

    /// <summary>Remove all terminal (Done / Skipped) events. Called at the start of each new run
    /// so the queue only contains the currently-staged scenario — prior runs' events don't linger
    /// as visual clutter in the history panel, and don't pollute the run's fingerprint.</summary>
    public int PurgeTerminal()
    {
        lock (_lock)
        {
            return _events.RemoveAll(e => e.Status == "Done" || e.Status == "Skipped");
        }
    }

    private static ScriptedEvent Clone(ScriptedEvent e) => new()
    {
        Id = e.Id,
        Action = e.Action,
        Location = e.Location,
        Subprocess = e.Subprocess,
        Step = e.Step,
        SlowMultiplier = e.SlowMultiplier,
        FailAfterPercent = e.FailAfterPercent,
        ExtraIterations = e.ExtraIterations,
        OpportunityCostHours = e.OpportunityCostHours,
        WorkingDays = e.WorkingDays,
        DiscoveryWd = e.DiscoveryWd,
        Reason = e.Reason,
        ErrorMessage = e.ErrorMessage,
        IsReleased = e.IsReleased,
        Status = e.Status,
    };
}

public class ScriptedEvent
{
    public int Id { get; set; }
    /// <summary>"slow" | "fail" | "critical" | "delay" | "hold"</summary>
    public string Action { get; set; } = "";
    public string Location { get; set; } = "";
    public string? Subprocess { get; set; }
    public string? Step { get; set; }

    // Action-specific params (only the relevant ones are set for a given action)
    public double? SlowMultiplier { get; set; }
    public double? FailAfterPercent { get; set; }  // 0.0–1.0 — fraction of normal duration before step fails
    public int? ExtraIterations { get; set; }       // extra failed/rerun iterations before the canonical success
    public double? OpportunityCostHours { get; set; } // business-hour gap between retries / fix duration
    public double? WorkingDays { get; set; }        // for "delay"
    public int? DiscoveryWd { get; set; }           // for "critical": working day when issue is discovered
    public string? Reason { get; set; }             // for "critical": cosmetic category (data quality / config / human error / tech)
    public string? ErrorMessage { get; set; }       // custom error text; if null, auto-generate from step name
    public bool IsReleased { get; set; }            // "hold" only

    /// <summary>"Pending" | "Firing" | "Done" | "Skipped"</summary>
    public string Status { get; set; } = "Pending";
}
