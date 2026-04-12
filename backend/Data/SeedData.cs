using System.Text.Json;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public static class SeedData
{
    // Location code → friendly name
    private static readonly Dictionary<string, (string Name, string Region)> LocationInfo = new()
    {
        ["AUDB"] = ("Australia", "APAC"),
        ["BEDB"] = ("Belgium Direct", "Europe"),
        ["BEGT"] = ("Belgium Group Treasury", "Europe"),
        ["DEDB"] = ("Germany Direct", "Europe"),
        ["DEGT"] = ("Germany Group Treasury", "Europe"),
        ["ESDB"] = ("Spain", "Europe"),
        ["FRDB"] = ("France", "Europe"),
        ["ICDB"] = ("ICG (International)", "Global"),
        ["ITDB"] = ("Italy", "Europe"),
        ["LUDB"] = ("Luxembourg", "Europe"),
        ["NLBTR"] = ("Netherlands BTR", "Europe"),
        ["NLRB"] = ("Netherlands Retail", "Europe"),
        ["PLDB"] = ("Poland", "Europe"),
        ["RODB"] = ("Romania", "Europe"),
        ["TRDB"] = ("Turkey", "Europe"),
        ["WBUS"] = ("Wholesale Banking US", "Americas"),
    };

    // Standardized subprocesses with phase and display order
    // Phase: DataIngestion (1-10), Processing (11-30), Reporting (31+)
    private static readonly (string Name, string Phase, int Order)[] SubprocessDefs =
    [
        // Data Ingestion
        ("Model Parameters",        "DataIngestion", 1),
        ("Spreads",                  "DataIngestion", 2),
        ("AIC Spreads",              "DataIngestion", 3),
        ("CSRBB Strategies",         "DataIngestion", 4),
        ("Load Position",            "DataIngestion", 5),
        ("Import Strategies",        "DataIngestion", 6),
        // Processing — Main Processes
        ("Valuation",                "Processing", 10),
        ("CSRBB (Val)",              "Processing", 11),
        ("CSRBB (Plan)",             "Processing", 12),
        ("EC",                       "Processing", 13),
        ("KR",                       "Processing", 14),
        ("Gap + Liquidity IR",       "Processing", 15),
        ("NII / Planning risk",      "Processing", 16),
        ("AIC Valuation",            "Processing", 17),
        ("ITS planning",             "Processing", 18),
        ("FTP EVE SOT",              "Processing", 19),
        ("NII SOT",                  "Processing", 20),
        // Processing — Location Specific
        ("Load Pos (RAS)",           "Processing", 25),
        ("Valuation (NPV) (RAS)",    "Processing", 26),
        ("Planning (NII) (RAS)",     "Processing", 27),
        ("EVE (Monthly)",            "Processing", 28),
        ("Planning Finance",         "Processing", 29),
        ("Cost Pricing",             "Processing", 30),
        ("Solo",                     "Processing", 31),
        ("SOT",                      "Processing", 32),
        ("SLM",                      "Processing", 33),
        // Quarterly
        ("CSRBB (Val) Quarterly",    "Processing", 40),
        ("CSRBB (Plan) Quarterly",   "Processing", 41),
        ("PDCE planning",            "Processing", 42),
        ("GAP ITS Planning",         "Processing", 43),
        ("EVE Optionality",          "Processing", 44),
        ("Quarterly valuation",      "Processing", 45),
        // Reporting / Other
        ("TRISS export",             "Reporting", 50),
        ("ACPR/ cashflow extraction","Reporting", 51),
        ("Custom reports",           "Reporting", 52),
        ("Regulatory Valuation",     "Reporting", 53),
        ("Regulatory Stress Test",   "Reporting", 54),
        ("Import CSRBB Strategies",  "Reporting", 55),
    ];

    public static async Task InitializeAsync(GreenlightContext db)
    {
        // Skip if already seeded
        if (await db.Locations.AnyAsync()) return;

        Console.WriteLine("Seeding database...");

        // 1. Locations
        var locations = new Dictionary<string, Location>();
        foreach (var (code, (name, region)) in LocationInfo)
        {
            var loc = new Location { Code = code, Name = name, Region = region, InScope = true };
            locations[code] = loc;
            db.Locations.Add(loc);
        }
        await db.SaveChangesAsync();

        // 2. Subprocesses
        var subprocesses = new Dictionary<string, Subprocess>();
        foreach (var (name, phase, order) in SubprocessDefs)
        {
            var sub = new Subprocess { Name = name, Phase = phase, DisplayOrder = order };
            subprocesses[name] = sub;
            db.Subprocesses.Add(sub);
        }
        await db.SaveChangesAsync();

        // 3. Load JSON seed files
        var basePath = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData");

        // 3a. Log entries
        var logJson = await File.ReadAllTextAsync(Path.Combine(basePath, "log-entries.json"));
        var logRecords = JsonSerializer.Deserialize<List<LogEntryJson>>(logJson) ?? [];
        Console.WriteLine($"  Loading {logRecords.Count} log entries...");

        // Create MCP runs from distinct report months
        var mcpRuns = new Dictionary<string, McpRun>();
        foreach (var rm in logRecords.Select(r => r.reportMonth).Distinct())
        {
            var year = 2000 + int.Parse(rm[..2]);
            var month = int.Parse(rm[2..]);
            var run = new McpRun
            {
                ReportMonth = rm,
                Year = year,
                Month = month,
                Status = "Completed",
                EomDate = new DateTime(year, month, DateTime.DaysInMonth(year, month))
            };
            mcpRuns[rm] = run;
            db.McpRuns.Add(run);
        }
        await db.SaveChangesAsync();

        // Set run start/end dates from log data
        foreach (var rm in mcpRuns.Keys)
        {
            var entries = logRecords.Where(r => r.reportMonth == rm).ToList();
            var startDates = entries.Where(e => e.startedAt != null).Select(e => DateTime.Parse(e.startedAt!));
            var endDates = entries.Where(e => e.endedAt != null).Select(e => DateTime.Parse(e.endedAt!));
            if (startDates.Any()) mcpRuns[rm].StartDate = startDates.Min();
            if (endDates.Any()) mcpRuns[rm].EndDate = endDates.Max();
        }
        await db.SaveChangesAsync();

        // 3b. Insert ProcessLogEntries
        var batch = new List<ProcessLogEntry>();
        foreach (var r in logRecords)
        {
            if (!locations.ContainsKey(r.location) || !mcpRuns.ContainsKey(r.reportMonth)) continue;

            batch.Add(new ProcessLogEntry
            {
                McpRunId = mcpRuns[r.reportMonth].Id,
                LocationId = locations[r.location].Id,
                Process = r.process,
                ScriptName = r.scriptName,
                StepName = r.stepName,
                StateName = r.stateName,
                StartedAt = ParseNullableDate(r.startedAt),
                EndedAt = ParseNullableDate(r.endedAt),
                NextStarted = ParseNullableDate(r.nextStarted),
                StartMarker = r.startMarker,
                EndMarker = r.endMarker,
                Iteration = r.iteration,
                TotalRuntimeHours = r.totalRuntimeHours,
                FailedRuntimeHours = r.failedRuntimeHours,
                EfficientRuntimeHours = r.efficientRuntimeHours,
                OpportunityCostHours = r.opportunityCostHours,
                InefficientRuntimeHours = r.inefficientRuntimeHours,
                E2ERuntimeHours = r.e2eRuntimeHours,
                ErrorMessage = r.errorMessage,
            });

            if (batch.Count >= 100)
            {
                db.ProcessLogEntries.AddRange(batch);
                await db.SaveChangesAsync();
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            db.ProcessLogEntries.AddRange(batch);
            await db.SaveChangesAsync();
        }

        // 4. SLA targets
        var slaJson = await File.ReadAllTextAsync(Path.Combine(basePath, "sla-targets.json"));
        var slaRecords = JsonSerializer.Deserialize<List<SlaEntryJson>>(slaJson) ?? [];
        Console.WriteLine($"  Loading {slaRecords.Count} SLA targets...");

        foreach (var s in slaRecords)
        {
            if (!locations.ContainsKey(s.locationCode)) continue;
            if (!subprocesses.ContainsKey(s.subprocessName))
            {
                // Create subprocess if it doesn't exist yet
                var newSub = new Subprocess { Name = s.subprocessName, Phase = "Processing", DisplayOrder = 99 };
                subprocesses[s.subprocessName] = newSub;
                db.Subprocesses.Add(newSub);
                await db.SaveChangesAsync();
            }

            db.SlaTargets.Add(new SlaTarget
            {
                LocationId = locations[s.locationCode].Id,
                SubprocessId = subprocesses[s.subprocessName].Id,
                Frequency = s.frequency,
                Deadline = s.deadline ?? "",
                Workday = s.GetWorkday(),
                SlaDate = ParseNullableDate(s.slaDate),
            });
        }
        await db.SaveChangesAsync();

        // 5. Build SubprocessRun matrix from log data
        // Derive the aggregate status per location+subprocess from log entries
        Console.WriteLine("  Building subprocess run matrix...");
        await BuildSubprocessRunMatrix(db, mcpRuns, locations, subprocesses, logRecords);

        Console.WriteLine("Seeding complete!");
    }

    private static async Task BuildSubprocessRunMatrix(
        GreenlightContext db,
        Dictionary<string, McpRun> mcpRuns,
        Dictionary<string, Location> locations,
        Dictionary<string, Subprocess> subprocesses,
        List<LogEntryJson> logRecords)
    {
        // Map ScriptName base patterns to standardized subprocess names
        var scriptToSubprocess = BuildScriptMapping(logRecords);

        foreach (var (rm, run) in mcpRuns)
        {
            var runEntries = logRecords.Where(r => r.reportMonth == rm).ToList();

            // Group by location + resolved subprocess
            var grouped = runEntries
                .Select(e => new
                {
                    Entry = e,
                    SubprocessName = ResolveSubprocessName(e.scriptName, e.location, scriptToSubprocess)
                })
                .Where(x => x.SubprocessName != null && subprocesses.ContainsKey(x.SubprocessName))
                .GroupBy(x => new { x.Entry.location, x.SubprocessName });

            foreach (var g in grouped)
            {
                if (!locations.ContainsKey(g.Key.location)) continue;

                var entries = g.Select(x => x.Entry).ToList();
                var latestIteration = entries.Max(e => e.iteration);
                var latestEntries = entries.Where(e => e.iteration == latestIteration).ToList();

                // Determine aggregate status
                var status = DeriveStatus(latestEntries);

                var starts = entries.Where(e => e.startedAt != null).Select(e => DateTime.Parse(e.startedAt!));
                var ends = entries.Where(e => e.endedAt != null).Select(e => DateTime.Parse(e.endedAt!));

                var startedAt = starts.Any() ? starts.Min() : (DateTime?)null;
                var completedAt = ends.Any() ? ends.Max() : (DateTime?)null;
                var elapsed = (startedAt.HasValue && completedAt.HasValue)
                    ? (completedAt.Value - startedAt.Value).TotalMinutes : (double?)null;

                db.SubprocessRuns.Add(new SubprocessRun
                {
                    McpRunId = run.Id,
                    LocationId = locations[g.Key.location].Id,
                    SubprocessId = subprocesses[g.Key.SubprocessName!].Id,
                    Status = status,
                    StartedAt = startedAt,
                    CompletedAt = completedAt,
                    ElapsedMinutes = elapsed,
                    CompletedSteps = latestEntries.Count(e => e.stateName == "Completed"),
                    TotalRequiredSteps = latestEntries.Count,
                });
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Maps script name patterns to standardized subprocess names.
    /// Uses prefix matching, stripping rerun suffixes.
    /// </summary>
    private static Dictionary<string, string> BuildScriptMapping(List<LogEntryJson> logRecords)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pattern: base script name prefix → subprocess name
        var prefixRules = new (string Prefix, string Subprocess)[]
        {
            ("Load_compare_MP", "Model Parameters"),
            ("Load_MP", "Model Parameters"),
            ("Load_compare_Spreads", "Spreads"),
            ("Load_Spreads", "Spreads"),
            ("Load_AIC", "AIC Spreads"),
            ("Import_CSRBB", "CSRBB Strategies"),
            ("Load_POS_", "Load Position"),
            ("LOAD_POS_", "Load Position"),
            ("Load_ELP", "Load Position"),
            ("Load_Index", "Load Position"),
            ("Import_Strategies", "Import Strategies"),
            ("Valuation_AIC", "AIC Valuation"),
            ("Valuation_NLBTR_MCP_RAS", "Valuation (NPV) (RAS)"),
            ("Valuation_", "Valuation"),
            ("CSRBB_Valuation_", "CSRBB (Val)"),
            ("CSRBB_Planning_", "CSRBB (Plan)"),
            ("EC_", "EC"),
            ("KR_", "KR"),
            ("GAP_ITS", "GAP ITS Planning"),
            ("GAP_", "Gap + Liquidity IR"),
            ("Planning_NLBTR_MCP_RAS", "Planning (NII) (RAS)"),
            ("Planning_DEGT_MCP_RAS", "Planning (NII) (RAS)"),
            ("Combined_Planning", "NII / Planning risk"),
            ("Planning_", "NII / Planning risk"),
            ("ITS_", "ITS planning"),
            ("FTP_EVE", "FTP EVE SOT"),
            ("NII_SOT", "NII SOT"),
            ("SOT_Create", "SOT"),
            ("SOT_", "SOT"),
            ("Delete_Portfolio", "Load Position"),
            ("Copy_Portfolio", "Load Position"),
            ("POS_MCP_Export", "Load Position"),
            ("Finance_Planning", "Planning Finance"),
            ("Finance_Strategies", "Planning Finance"),
            ("Cost_Pricing", "Cost Pricing"),
            ("SOLO_", "Solo"),
            ("SLM_", "SLM"),
            ("EVE_Monthly", "EVE (Monthly)"),
            ("EVE_", "EVE Optionality"),
            ("TRISS_", "TRISS export"),
            ("ICDB_MCP", "Valuation"), // ICDB runs are complex, default to Valuation
            ("EC_NPV_Export", "EC"),
            ("KR_BPV_Export", "KR"),
        };

        // For each script in the data, find the best matching prefix
        foreach (var scriptName in logRecords.Select(r => r.scriptName).Distinct())
        {
            foreach (var (prefix, subprocess) in prefixRules)
            {
                if (scriptName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    map[scriptName] = subprocess;
                    break;
                }
            }
        }

        return map;
    }

    private static string? ResolveSubprocessName(string scriptName, string location,
        Dictionary<string, string> mapping)
    {
        if (mapping.TryGetValue(scriptName, out var name)) return name;

        // Try stripping rerun suffixes and matching again
        var baseName = scriptName.Split(" rr")[0].Split(" RR")[0].Trim();
        if (mapping.TryGetValue(baseName, out name)) return name;

        return null;
    }

    private static string DeriveStatus(List<LogEntryJson> entries)
    {
        if (entries.Any(e => e.stateName == "Failed")) return "Failed";
        if (entries.Any(e => e.stateName == "Stopped")) return "Stopped";
        if (entries.Any(e => e.stateName == "Unfinished")) return "Failed";
        if (entries.All(e => e.stateName == "Completed")) return "Completed";
        return "Running";
    }

    private static DateTime? ParseNullableDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return DateTime.TryParse(s, out var d) ? d : null;
    }

    // JSON DTOs
    private class LogEntryJson
    {
        public string reportMonth { get; set; } = "";
        public string location { get; set; } = "";
        public string process { get; set; } = "";
        public string? startMarker { get; set; }
        public string? endMarker { get; set; }
        public string? startedAt { get; set; }
        public string? endedAt { get; set; }
        public string? nextStarted { get; set; }
        public string? errorMessage { get; set; }
        public string stateName { get; set; } = "";
        public string scriptName { get; set; } = "";
        public int iteration { get; set; } = 1;
        public string stepName { get; set; } = "";
        public double totalRuntimeHours { get; set; }
        public double failedRuntimeHours { get; set; }
        public double efficientRuntimeHours { get; set; }
        public double opportunityCostHours { get; set; }
        public double inefficientRuntimeHours { get; set; }
        public double e2eRuntimeHours { get; set; }
    }

    private class SlaEntryJson
    {
        public string locationShort { get; set; } = "";
        public string locationCode { get; set; } = "";
        public string subprocessName { get; set; } = "";
        public string frequency { get; set; } = "";
        public string deadline { get; set; } = "";
        public object? workday { get; set; }
        public string? slaDate { get; set; }

        public int GetWorkday()
        {
            if (workday is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number) return je.GetInt32();
                if (je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out var v)) return v;
            }
            if (workday is int i) return i;
            if (workday is string s && int.TryParse(s, out var w)) return w;
            return 0;
        }
    }
}
