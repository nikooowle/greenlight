namespace Backend.Models;

/// <summary>
/// A monthly MCP run cycle (e.g., Jan 2026 = "2601").
/// </summary>
public class McpRun
{
    public int Id { get; set; }

    /// <summary>Raw report-month code from production, e.g. "2601".</summary>
    public string ReportMonth { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Not Started | Running | Completed | Failed</summary>
    public string Status { get; set; } = "Not Started";

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>End-of-month date this run covers.</summary>
    public DateTime? EomDate { get; set; }

    // Navigation
    public ICollection<ProcessLogEntry> LogEntries { get; set; } = [];
    public ICollection<SubprocessRun> SubprocessRuns { get; set; } = [];
}
