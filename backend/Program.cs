using Microsoft.EntityFrameworkCore;
using Backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<GreenlightContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Greenlight")));

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
            sr.Status,
            sr.StartedAt,
            sr.CompletedAt,
            sr.ElapsedMinutes,
            sr.CompletedSteps,
            sr.TotalRequiredSteps
        })
        .ToListAsync();

    return Results.Ok(matrix);
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

app.Run();
