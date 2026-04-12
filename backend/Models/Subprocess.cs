namespace Backend.Models;

/// <summary>
/// A standardized process definition grouped by phase.
/// Examples: "Valuation", "Planning", "Load Position", "EC", "KR".
/// </summary>
public class Subprocess
{
    public int Id { get; set; }

    /// <summary>Canonical name, e.g. "Valuation", "CSRBB (Plan)".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>DataIngestion | Processing | Reporting</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Display order within its phase.</summary>
    public int DisplayOrder { get; set; }

    public string? Description { get; set; }

    // Navigation
    public ICollection<SubprocessRun> SubprocessRuns { get; set; } = [];
    public ICollection<SlaTarget> SlaTargets { get; set; } = [];
    public ICollection<LocationStepRegistry> StepRegistry { get; set; } = [];
    public ICollection<ScriptMapping> ScriptMappings { get; set; } = [];
}
