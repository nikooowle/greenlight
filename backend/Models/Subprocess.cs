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

    /// <summary>Display order within its phase (from v6 golden mapping globalSeq).</summary>
    public int DisplayOrder { get; set; }

    public string? Description { get; set; }

    /// <summary>Main Process | Location Specific (scope classification from v6 mapping).</summary>
    public string? Scope { get; set; }

    /// <summary>True if this subprocess only runs at quarter-end months (Mar/Jun/Sep/Dec).</summary>
    public bool IsQuarterly { get; set; }

    // Navigation
    public ICollection<SubprocessRun> SubprocessRuns { get; set; } = [];
    public ICollection<SlaTarget> SlaTargets { get; set; } = [];
    public ICollection<LocationStepRegistry> StepRegistry { get; set; } = [];
    public ICollection<ScriptMapping> ScriptMappings { get; set; } = [];
}
