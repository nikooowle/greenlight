var builder = WebApplication.CreateBuilder(args);

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

app.UseCors();

// Health check endpoint
app.MapGet("/api/health", () => new
{
    status = "healthy",
    service = "ALM Greenlight API",
    timestamp = DateTime.UtcNow
});

// Placeholder: will be replaced with real endpoints in Milestone 4
app.MapGet("/api/mcp-runs/current", () => new
{
    id = 0,
    month = DateTime.Now.Month,
    year = DateTime.Now.Year,
    status = "No active run",
    message = "Connect database in Milestone 2"
});

app.Run();
