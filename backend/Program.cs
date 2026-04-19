using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<GreenlightContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Greenlight")));

// Simulator + historical stats
builder.Services.AddSingleton<SimulatorState>();
builder.Services.AddSingleton<ScriptedEventQueue>();
builder.Services.AddSingleton<HistoricalStatsService>(sp =>
{
    var log = sp.GetRequiredService<ILogger<HistoricalStatsService>>();
    var svc = new HistoricalStatsService(log);
    svc.Load(Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "historical-stats.json"));
    return svc;
});
builder.Services.AddHostedService<SimulatorService>();

// Enable CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Auto-apply migrations + seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GreenlightContext>();
    db.Database.Migrate();
    await SeedData.InitializeAsync(db);
}

app.UseCors();

// Health check endpoint
app.MapGet("/api/health", () => new
{
    status = "healthy",
    service = "ALM Greenlight API",
    timestamp = DateTime.UtcNow
});

// Current MCP run — now reads from real database
app.MapGet("/api/mcp-runs/current", async (GreenlightContext db) =>
{
    var run = await db.McpRuns
        .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
        .FirstOrDefaultAsync();

    if (run is null)
        return Results.Ok(new { status = "No active run", message = "No MCP runs in database" });

    var logCount = await db.ProcessLogEntries.CountAsync(e => e.McpRunId == run.Id);
    var locationCount = await db.Locations.CountAsync(l => l.InScope);

    return Results.Ok(new
    {
        run.Id,
        run.ReportMonth,
        run.Year,
        run.Month,
        run.Status,
        run.StartDate,
        run.EndDate,
        run.EomDate,
        logEntries = logCount,
        locations = locationCount
    });
});

// List all locations
app.MapGet("/api/locations", async (GreenlightContext db) =>
    await db.Locations.OrderBy(l => l.Code).ToListAsync());

// List all subprocesses by phase
app.MapGet("/api/subprocesses", async (GreenlightContext db) =>
    await db.Subprocesses.OrderBy(s => s.Phase).ThenBy(s => s.DisplayOrder).ToListAsync());

// Subprocess runs matrix for a given MCP run
app.MapGet("/api/mcp-runs/{reportMonth}/matrix", async (string reportMonth, GreenlightContext db) =>
{
    var run = await db.McpRuns.FirstOrDefaultAsync(r => r.ReportMonth == reportMonth);
    if (run is null) return Results.NotFound();

    var matrix = await db.SubprocessRuns
        .Where(sr => sr.McpRunId == run.Id)
        .Include(sr => sr.Location)
        .Include(sr => sr.Subprocess)
        .OrderBy(sr => sr.Location.Code)
        .ThenBy(sr => sr.Subprocess.Phase)
        .ThenBy(sr => sr.Subprocess.DisplayOrder)
        .Select(sr => new
        {
            location = sr.Location.Code,
            subprocess = sr.Subprocess.Name,
            phase = sr.Subprocess.Phase,
            scope = sr.Subprocess.Scope,
            isQuarterly = sr.Subprocess.IsQuarterly,
            sr.Status,
            sr.StartedAt,
            sr.CompletedAt,
            sr.ElapsedMinutes,
            sr.CompletedSteps,
            sr.TotalRequiredSteps,
            sr.HasOverrides
        })
        .ToListAsync();

    return Results.Ok(matrix);
});

// Operator overrides for a given MCP run
app.MapGet("/api/mcp-runs/{reportMonth}/overrides", async (string reportMonth, GreenlightContext db) =>
{
    var run = await db.McpRuns.FirstOrDefaultAsync(r => r.ReportMonth == reportMonth);
    if (run is null) return Results.NotFound();
    var overrides = await db.OperatorOverrides
        .Where(o => o.McpRunId == run.Id && o.RevokedAt == null)
        .Include(o => o.Location)
        .Include(o => o.Subprocess)
        .OrderBy(o => o.Location.Code)
        .ThenBy(o => o.Subprocess.Name)
        .Select(o => new
        {
            location = o.Location.Code,
            subprocess = o.Subprocess.Name,
            stepName = o.StepName,
            action = o.Action,
            reason = o.Reason,
            ticketRef = o.TicketRef,
            evidenceUrl = o.EvidenceUrl,
            @operator = o.Operator,
            createdAt = o.CreatedAt,
        })
        .ToListAsync();
    return Results.Ok(overrides);
});

// Process log entries for a specific run + location
app.MapGet("/api/mcp-runs/{reportMonth}/logs", async (string reportMonth, string? location, GreenlightContext db) =>
{
    var run = await db.McpRuns.FirstOrDefaultAsync(r => r.ReportMonth == reportMonth);
    if (run is null) return Results.NotFound();

    var query = db.ProcessLogEntries
        .Where(e => e.McpRunId == run.Id)
        .Include(e => e.Location);

    if (!string.IsNullOrEmpty(location))
        query = query.Where(e => e.Location.Code == location).Include(e => e.Location);

    var entries = await query
        .OrderBy(e => e.Location.Code)
        .ThenBy(e => e.StartedAt)
        .Select(e => new
        {
            location = e.Location.Code,
            e.ScriptName,
            e.StepName,
            e.StateName,
            e.StartedAt,
            e.EndedAt,
            e.Iteration,
            e.TotalRuntimeHours,
            e.FailedRuntimeHours,
            e.EfficientRuntimeHours,
            e.OpportunityCostHours,
            e.E2ERuntimeHours,
            e.ErrorMessage
        })
        .ToListAsync();

    return Results.Ok(entries);
});

// SLA targets
app.MapGet("/api/sla-targets", async (GreenlightContext db) =>
    await db.SlaTargets
        .Include(s => s.Location)
        .Include(s => s.Subprocess)
        .OrderBy(s => s.Location.Code)
        .ThenBy(s => s.Subprocess.Name)
        .Select(s => new
        {
            location = s.Location.Code,
            subprocess = s.Subprocess.Name,
            s.Frequency,
            s.Deadline,
            s.Workday,
            s.SlaDate
        })
        .ToListAsync());

// ── Simulator control endpoints ──────────────────────────────────────

app.MapGet("/api/simulator/status", (SimulatorState sim) => sim.GetStatus());

app.MapPost("/api/simulator/start", (SimulatorState sim) =>
{
    if (sim.IsRunning)
        return Results.BadRequest(new { error = "Simulation already running" });
    sim.StartRequested = true;
    return Results.Ok(new { message = "Simulation starting..." });
});

app.MapPost("/api/simulator/pause", (SimulatorState sim) =>
{
    if (!sim.IsRunning)
        return Results.BadRequest(new { error = "No simulation running" });
    sim.IsPaused = true;
    return Results.Ok(new { message = "Simulation paused" });
});

app.MapPost("/api/simulator/resume", (SimulatorState sim) =>
{
    if (!sim.IsRunning)
        return Results.BadRequest(new { error = "No simulation running" });
    sim.IsPaused = false;
    return Results.Ok(new { message = "Simulation resumed" });
});

app.MapPost("/api/simulator/speed/{multiplier:double}", (double multiplier, SimulatorState sim) =>
{
    sim.SpeedMultiplier = multiplier;
    return Results.Ok(new { message = $"Speed set to {sim.SpeedMultiplier}x" });
});

app.MapPost("/api/simulator/reset", (SimulatorState sim) =>
{
    sim.IsRunning = false;
    sim.IsPaused = false;
    sim.ResetRequested = true;
    return Results.Ok(new { message = "Simulation reset requested" });
});

// Target month: 2604 (Apr 2026 regular) or 2606 (Jun 2026 quarter-end)
app.MapPost("/api/simulator/target-month/{month}", (string month, SimulatorState sim) =>
{
    if (sim.IsRunning)
        return Results.BadRequest(new { error = "Cannot change target month while simulation is running. Reset first." });
    if (month != "2604" && month != "2606")
        return Results.BadRequest(new { error = "Only '2604' (Apr 2026) and '2606' (Jun 2026) are supported in the prototype." });
    sim.TargetMonth = month;
    return Results.Ok(new { message = $"Target month set to {month}", isQuarterEnd = sim.IsQuarterEnd });
});

// ── Simulator mode + scripted-event queue ────────────────────────────

app.MapPost("/api/simulator/mode/{mode}", (string mode, SimulatorState sim) =>
{
    if (!Enum.TryParse<SimMode>(mode, ignoreCase: true, out var parsed))
        return Results.BadRequest(new { error = "Mode must be one of: clean, baseline, stressed." });
    if (sim.IsRunning)
        return Results.BadRequest(new { error = "Cannot change mode while simulation is running. Reset first." });
    sim.Mode = parsed;
    return Results.Ok(new { message = $"Mode set to {parsed.ToString().ToLowerInvariant()}" });
});

// Catalog used by the operator UI dropdowns — lives entirely in historical-stats.
app.MapGet("/api/simulator/catalog", (HistoricalStatsService stats, GreenlightContext db) =>
{
    var locs = db.Locations.Where(l => l.InScope).OrderBy(l => l.Code).Select(l => l.Code).ToList();
    var catalog = new List<object>();
    foreach (var loc in locs)
    {
        var subs = db.Subprocesses.OrderBy(s => s.DisplayOrder).ToList();
        var subList = new List<object>();
        foreach (var sub in subs)
        {
            var s = stats.Get(loc, sub.Name);
            if (s is null || s.Steps.Count == 0) continue;
            subList.Add(new
            {
                subprocess = sub.Name,
                phase = sub.Phase,
                isQuarterly = sub.IsQuarterly,
                steps = s.Steps.Select(st => new { step = st.StepName, avgMinutes = st.AvgMinutes }).ToList(),
                sampleErrors = s.SampleErrors,
            });
        }
        if (subList.Count > 0) catalog.Add(new { location = loc, subprocesses = subList });
    }
    return Results.Ok(catalog);
});

app.MapPost("/api/simulator/inject", (InjectRequest req, ScriptedEventQueue queue) =>
{
    var action = (req.Action ?? "").ToLowerInvariant();
    if (action is not ("slow" or "fail" or "critical" or "delay" or "hold"))
        return Results.BadRequest(new { error = "action must be slow, fail, critical, delay, or hold" });
    if (string.IsNullOrEmpty(req.Location))
        return Results.BadRequest(new { error = "location is required" });
    if (action == "slow" && req.Subprocess is null)
        return Results.BadRequest(new { error = "slow requires subprocess" });
    if (action == "slow" && (req.SlowMultiplier is null || req.SlowMultiplier <= 0))
        return Results.BadRequest(new { error = "slow requires slowMultiplier > 0" });
    if (action == "fail" && req.Subprocess is null)
        return Results.BadRequest(new { error = "fail requires subprocess" });
    if (action == "hold" && req.Subprocess is null)
        return Results.BadRequest(new { error = "hold requires subprocess" });
    if (action == "delay" && (req.WorkingDays is null || req.WorkingDays <= 0))
        return Results.BadRequest(new { error = "delay requires workingDays > 0" });
    // critical: location-wide rerun triggered by late-discovered incident at a specific working day
    if (action == "critical" && (req.DiscoveryWd is null || req.DiscoveryWd < 1 || req.DiscoveryWd > 22))
        return Results.BadRequest(new { error = "critical requires discoveryWd in 1..22" });

    var id = queue.Add(new ScriptedEvent
    {
        Action = action,
        Location = req.Location,
        Subprocess = req.Subprocess,
        Step = null, // scripted events are subprocess-level; step is picked by the simulator internally
        SlowMultiplier = req.SlowMultiplier,
        FailAfterPercent = req.FailAfterPercent ?? 0.8,
        ExtraIterations = req.ExtraIterations ?? 1,
        OpportunityCostHours = req.OpportunityCostHours ?? (action == "critical" ? 24 : 4),
        WorkingDays = req.WorkingDays,
        DiscoveryWd = req.DiscoveryWd,
        Reason = req.Reason,
        ErrorMessage = req.ErrorMessage,
    });
    return Results.Ok(new { id, message = $"{action} event queued" });
});

app.MapGet("/api/simulator/queue", (ScriptedEventQueue queue) => queue.List());

app.MapDelete("/api/simulator/queue/{id:int}", (int id, ScriptedEventQueue queue) =>
{
    return queue.Remove(id)
        ? Results.Ok(new { message = $"event {id} removed" })
        : Results.NotFound(new { error = $"no event with id {id}" });
});

app.MapPost("/api/simulator/queue/{id:int}/release", (int id, ScriptedEventQueue queue) =>
{
    return queue.Release(id)
        ? Results.Ok(new { message = $"hold {id} released" })
        : Results.BadRequest(new { error = "no matching hold event with that id" });
});

// Fast-replay cache control — clears the in-memory cache of prior simulated runs. Use this if
// seed data changed or you want to force a fresh sim even when the (month, mode, queue) matches.
app.MapPost("/api/simulator/cache/clear", (IEnumerable<IHostedService> hosted) =>
{
    var sim = hosted.OfType<Backend.Services.SimulatorService>().FirstOrDefault();
    if (sim is null) return Results.StatusCode(500);
    var before = sim.CacheEntryCount;
    sim.ClearCache();
    return Results.Ok(new { cleared = before, message = $"cleared {before} cache entr{(before == 1 ? "y" : "ies")}" });
});

app.MapGet("/api/simulator/cache", (IEnumerable<IHostedService> hosted) =>
{
    var sim = hosted.OfType<Backend.Services.SimulatorService>().FirstOrDefault();
    return Results.Ok(new { entries = sim?.CacheEntryCount ?? 0 });
});

app.Run();

// Payload for POST /api/simulator/inject — scripted events target (location, subprocess) only.
// Step is not exposed: the simulator picks which step fails / holds internally.
record InjectRequest(
    string Action,              // "slow" | "fail" | "critical" | "delay" | "hold"
    string Location,
    string? Subprocess,
    double? SlowMultiplier,
    double? FailAfterPercent,
    int? ExtraIterations,
    double? OpportunityCostHours,
    double? WorkingDays,
    int? DiscoveryWd,           // "critical" only: WD when the incident is discovered (1..22)
    string? Reason,             // "critical" only: cosmetic category (DQ / config / human / tech)
    string? ErrorMessage);
