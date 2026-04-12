namespace Backend.Services;

/// <summary>
/// Thread-safe singleton holding simulator state.
/// Shared between the BackgroundService and API control endpoints.
/// </summary>
public class SimulatorState
{
    private readonly object _lock = new();

    private bool _isRunning;
    private bool _isPaused;
    private double _speedMultiplier = 60; // 1 sim minute = 1 real second
    private int? _currentRunId;
    private string _phase = "Idle";
    private int _completedSubprocesses;
    private int _totalSubprocesses;
    private string? _currentSubprocess;
    private string? _currentLocation;

    public bool IsRunning { get { lock (_lock) return _isRunning; } set { lock (_lock) _isRunning = value; } }
    public bool IsPaused { get { lock (_lock) return _isPaused; } set { lock (_lock) _isPaused = value; } }
    public double SpeedMultiplier { get { lock (_lock) return _speedMultiplier; } set { lock (_lock) _speedMultiplier = Math.Clamp(value, 1, 1000); } }
    public int? CurrentRunId { get { lock (_lock) return _currentRunId; } set { lock (_lock) _currentRunId = value; } }
    public string Phase { get { lock (_lock) return _phase; } set { lock (_lock) _phase = value; } }
    public int CompletedSubprocesses { get { lock (_lock) return _completedSubprocesses; } set { lock (_lock) _completedSubprocesses = value; } }
    public int TotalSubprocesses { get { lock (_lock) return _totalSubprocesses; } set { lock (_lock) _totalSubprocesses = value; } }
    public string? CurrentSubprocess { get { lock (_lock) return _currentSubprocess; } set { lock (_lock) _currentSubprocess = value; } }
    public string? CurrentLocation { get { lock (_lock) return _currentLocation; } set { lock (_lock) _currentLocation = value; } }

    /// <summary>Signal the service to start a new run.</summary>
    public bool StartRequested { get; set; }

    /// <summary>Signal the service to stop and clean up.</summary>
    public bool ResetRequested { get; set; }

    public object GetStatus() => new
    {
        isRunning = IsRunning,
        isPaused = IsPaused,
        speedMultiplier = SpeedMultiplier,
        currentRunId = CurrentRunId,
        phase = Phase,
        completedSubprocesses = CompletedSubprocesses,
        totalSubprocesses = TotalSubprocesses,
        progress = TotalSubprocesses > 0
            ? Math.Round(100.0 * CompletedSubprocesses / TotalSubprocesses, 1)
            : 0,
        currentSubprocess = CurrentSubprocess,
        currentLocation = CurrentLocation,
    };
}
