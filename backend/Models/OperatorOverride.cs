namespace Backend.Models;

/// <summary>
/// Operator-recorded override for a mandatory step that didn't get logged by Q3
/// (e.g., operator manually ran the export via QRM UI because Q3 had a bug).
/// The production log stays read-only; overrides live here as a separate layer.
/// Dashboard aggregation: if a step isn't in ProcessLogEntry but has an Override, treat as Completed.
/// </summary>
public class OperatorOverride
{
    public int Id { get; set; }

    public int McpRunId { get; set; }
    public McpRun McpRun { get; set; } = null!;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SubprocessId { get; set; }
    public Subprocess Subprocess { get; set; } = null!;

    /// <summary>The step that was manually completed.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>complete | skip | fail — what the override asserts about the step.</summary>
    public string Action { get; set; } = "complete";

    /// <summary>Why the override was needed (e.g. "Manual QRM export — Q3 bug INC14598723").</summary>
    public string Reason { get; set; } = string.Empty;

    public string? TicketRef { get; set; }
    public string? EvidenceUrl { get; set; }
    public string Operator { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
