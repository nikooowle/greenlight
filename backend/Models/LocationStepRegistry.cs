namespace Backend.Models;

/// <summary>
/// Golden source: defines the mandatory steps that must complete
/// for a subprocess to be considered "Complete" at a given location.
/// [Location] + [Subprocess] = [List of Required Step Names]
/// </summary>
public class LocationStepRegistry
{
    public int Id { get; set; }

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SubprocessId { get; set; }
    public Subprocess Subprocess { get; set; } = null!;

    /// <summary>The step name that must appear as Completed in the logs.</summary>
    public string RequiredStepName { get; set; } = string.Empty;

    /// <summary>Display order of this step within the subprocess.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Whether this step is mandatory for completion determination.</summary>
    public bool IsMandatory { get; set; } = true;
}
