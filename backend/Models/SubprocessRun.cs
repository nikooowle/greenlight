namespace Backend.Models;

/// <summary>
/// The matrix cell: status of a subprocess for a location in a specific MCP run.
/// This is the derived/aggregated status used by the dashboard grid.
/// </summary>
public class SubprocessRun
{
    public int Id { get; set; }

    public int McpRunId { get; set; }
    public McpRun McpRun { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SubprocessId { get; set; }
    public Subprocess Subprocess { get; set; } = null!;

    /// <summary>Not in Scope | Not Started | Running | Completed | Failed | Stopped | For Rerun</summary>
    public string Status { get; set; } = "Not Started";

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>Total elapsed time in minutes.</summary>
    public double? ElapsedMinutes { get; set; }

    /// <summary>Count of completed mandatory steps vs total required.</summary>
    public int CompletedSteps { get; set; }
    public int TotalRequiredSteps { get; set; }
}
