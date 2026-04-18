using System.Text.Json;

namespace Backend.Services;

/// <summary>
/// Loads `historical-stats.json` at startup and exposes lookups for the simulator.
/// Stats are derived by build-final-mapping.js from 4 months of production data,
/// using the canonical-iteration rule for efficient durations.
/// </summary>
public class HistoricalStatsService
{
    private Dictionary<(string loc, string sub), SubprocessStats> _stats = new();
    private List<string> _allErrors = new();
    private readonly ILogger<HistoricalStatsService> _log;

    public HistoricalStatsService(ILogger<HistoricalStatsService> log)
    {
        _log = log;
    }

    public void Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            _log.LogWarning("historical-stats.json not found at {Path}. Simulator will use fallback values.", jsonPath);
            return;
        }
        var json = File.ReadAllText(jsonPath);
        var doc = JsonSerializer.Deserialize<StatsFile>(json);
        if (doc is null)
        {
            _log.LogError("historical-stats.json failed to parse");
            return;
        }
        if (doc.version != "v6")
        {
            _log.LogWarning("historical-stats.json version is {Version}, expected v6", doc.version);
        }
        var dict = new Dictionary<(string, string), SubprocessStats>();
        var errSet = new HashSet<string>();
        foreach (var s in doc.stats)
        {
            var steps = s.stepDurations.Select(kv => new StepStat(kv.Key, kv.Value.avgMinutes)).ToList();
            dict[(s.location, s.subprocess)] = new SubprocessStats(
                Location: s.location,
                Subprocess: s.subprocess,
                Steps: steps,
                FailureRate: s.failureRate,
                RerunCountAvg: s.rerunCountAvg,
                SampleErrors: s.sampleErrors
            );
            foreach (var e in s.sampleErrors) errSet.Add(e);
        }
        _stats = dict;
        _allErrors = errSet.ToList();
        _log.LogInformation("HistoricalStatsService loaded {Count} (location, subprocess) stat entries from {Path}", _stats.Count, jsonPath);
    }

    public SubprocessStats? Get(string location, string subprocess)
        => _stats.TryGetValue((location, subprocess), out var s) ? s : null;

    /// <summary>Fallback error pool when a specific (loc, sub) has none.</summary>
    public IReadOnlyList<string> AllErrors => _allErrors;

    // ---------- Records ----------
    public record StepStat(string StepName, double AvgMinutes);

    public record SubprocessStats(
        string Location,
        string Subprocess,
        IReadOnlyList<StepStat> Steps,
        double FailureRate,
        double RerunCountAvg,
        IReadOnlyList<string> SampleErrors
    );

    // ---------- JSON DTOs ----------
    private class StatsFile
    {
        public string version { get; set; } = "";
        public string generatedAt { get; set; } = "";
        public List<StatsEntry> stats { get; set; } = [];
    }

    private class StatsEntry
    {
        public string location { get; set; } = "";
        public string subprocess { get; set; } = "";
        public Dictionary<string, StepDur> stepDurations { get; set; } = new();
        public double failureRate { get; set; }
        public double rerunCountAvg { get; set; }
        public List<string> sampleErrors { get; set; } = [];
    }

    private class StepDur
    {
        public double avgMinutes { get; set; }
    }
}
