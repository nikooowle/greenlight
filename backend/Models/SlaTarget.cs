namespace Backend.Models;

/// <summary>
/// SLA deadline per subprocess per location.
/// </summary>
public class SlaTarget
{
    public int Id { get; set; }

    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public int SubprocessId { get; set; }
    public Subprocess Subprocess { get; set; } = null!;

    /// <summary>M = Monthly, Q = Quarterly.</summary>
    public string Frequency { get; set; } = "M";

    /// <summary>Deadline label, e.g. "WD7" (workday 7).</summary>
    public string Deadline { get; set; } = string.Empty;

    /// <summary>Workday number (e.g. 7 = 7th business day after EOM).</summary>
    public int Workday { get; set; }

    /// <summary>Calculated SLA date for the current period.</summary>
    public DateTime? SlaDate { get; set; }
}
