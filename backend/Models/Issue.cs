namespace Backend.Models;

/// <summary>
/// A tracked failure or incident linked to a subprocess run.
/// </summary>
public class Issue
{
    public int Id { get; set; }

    public int SubprocessRunId { get; set; }
    public SubprocessRun SubprocessRun { get; set; } = null!;

    /// <summary>Category: Data Quality | Infrastructure | Configuration | Manual Error | Unknown</summary>
    public string RootCauseCategory { get; set; } = "Unknown";

    /// <summary>Critical | High | Medium | Low</summary>
    public string Severity { get; set; } = "Medium";

    public string Description { get; set; } = string.Empty;

    /// <summary>ServiceNow incident number, e.g. "INC14485851".</summary>
    public string? IncidentNumber { get; set; }

    /// <summary>Free-text note from operator.</summary>
    public string? OperatorComment { get; set; }

    /// <summary>Open | Investigating | Resolved | Closed</summary>
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
