namespace Backend.Models;

/// <summary>
/// A single row from the SQL extract — one step execution within a subprocess run.
/// This is the raw processed data from the production SQL query output.
/// </summary>
public class ProcessLogEntry
{
    public int Id { get; set; }

    public int McpRunId { get; set; }
    public McpRun McpRun { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    /// <summary>Normalized process name from SQL output (currently always "MCP").</summary>
    public string Process { get; set; } = string.Empty;

    /// <summary>Dynamic script name from production, e.g. "KR_AU_MCP rr due to tech issue".</summary>
    public string ScriptName { get; set; } = string.Empty;

    /// <summary>The canonical step name, e.g. "bpv-kr valuation".</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Completed | Failed | Stopped | Unfinished</summary>
    public string StateName { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>START marker from logs.</summary>
    public string? StartMarker { get; set; }

    /// <summary>END marker from logs (null if step didn't complete).</summary>
    public string? EndMarker { get; set; }

    /// <summary>Iteration number (1 = first run, 2+ = rerun).</summary>
    public int Iteration { get; set; } = 1;

    /// <summary>Total runtime in hours (including failed reruns).</summary>
    public double TotalRuntimeHours { get; set; }

    /// <summary>Hours spent on failed executions.</summary>
    public double FailedRuntimeHours { get; set; }

    /// <summary>Productive runtime hours (Total - Failed).</summary>
    public double EfficientRuntimeHours { get; set; }

    /// <summary>Business-hours idle time between failure and restart.</summary>
    public double OpportunityCostHours { get; set; }

    /// <summary>Failed + Opportunity cost.</summary>
    public double InefficientRuntimeHours { get; set; }

    /// <summary>Total end-to-end hours (runtime + opportunity cost).</summary>
    public double E2ERuntimeHours { get; set; }

    /// <summary>Error message captured from logs (null if no error).</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Next step's start time (for gap analysis).</summary>
    public DateTime? NextStarted { get; set; }
}
