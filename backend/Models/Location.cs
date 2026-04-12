namespace Backend.Models;

/// <summary>
/// An ING location / country book (e.g., AUDB = Australia Direct Bank).
/// </summary>
public class Location
{
    public int Id { get; set; }

    /// <summary>Short code: AUDB, BEGT, NLBTR, etc.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable name, e.g. "Australia".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Region grouping (APAC, EMEA, etc.) — optional.</summary>
    public string? Region { get; set; }

    /// <summary>Whether this location is in-scope for current MCP runs.</summary>
    public bool InScope { get; set; } = true;

    // Navigation
    public ICollection<SubprocessRun> SubprocessRuns { get; set; } = [];
    public ICollection<ProcessLogEntry> LogEntries { get; set; } = [];
    public ICollection<SlaTarget> SlaTargets { get; set; } = [];
    public ICollection<LocationStepRegistry> StepRegistry { get; set; } = [];
    public ICollection<ScriptMapping> ScriptMappings { get; set; } = [];
}
