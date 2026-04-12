namespace Backend.Models;

/// <summary>
/// Maps dynamic/manual raw script names to the standardized subprocess name.
/// Factors in Location because the same script base name may map differently by country.
/// Raw Script Name + Location → Standardized Subprocess Name.
/// </summary>
public class ScriptMapping
{
    public int Id { get; set; }

    /// <summary>Pattern to match against raw script names (prefix match, ignoring rerun suffixes).</summary>
    public string RawScriptPattern { get; set; } = string.Empty;

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SubprocessId { get; set; }
    public Subprocess Subprocess { get; set; } = null!;
}
