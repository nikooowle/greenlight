using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<GreenlightContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Greenlight")));

// Simulator + historical stats
builder.Services.AddSingleton<SimulatorState>();
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

app.Run();
