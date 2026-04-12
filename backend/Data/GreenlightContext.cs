using Microsoft.EntityFrameworkCore;
using Backend.Models;

namespace Backend.Data;

public class GreenlightContext : DbContext
{
    public GreenlightContext(DbContextOptions<GreenlightContext> options) : base(options) { }

    public DbSet<McpRun> McpRuns => Set<McpRun>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Subprocess> Subprocesses => Set<Subprocess>();
    public DbSet<SubprocessRun> SubprocessRuns => Set<SubprocessRun>();
    public DbSet<ProcessLogEntry> ProcessLogEntries => Set<ProcessLogEntry>();
    public DbSet<ScriptMapping> ScriptMappings => Set<ScriptMapping>();
    public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
    public DbSet<LocationStepRegistry> LocationStepRegistry => Set<LocationStepRegistry>();
    public DbSet<Issue> Issues => Set<Issue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // McpRun
        modelBuilder.Entity<McpRun>(e =>
        {
            e.HasIndex(r => r.ReportMonth).IsUnique();
        });

        // Location
        modelBuilder.Entity<Location>(e =>
        {
            e.HasIndex(l => l.Code).IsUnique();
        });

        // Subprocess
        modelBuilder.Entity<Subprocess>(e =>
        {
            e.HasIndex(s => s.Name).IsUnique();
        });

        // SubprocessRun — composite uniqueness: one status per location+subprocess+run
        modelBuilder.Entity<SubprocessRun>(e =>
        {
            e.HasIndex(sr => new { sr.McpRunId, sr.LocationId, sr.SubprocessId }).IsUnique();

            e.HasOne(sr => sr.McpRun).WithMany(r => r.SubprocessRuns)
                .HasForeignKey(sr => sr.McpRunId);

            e.HasOne(sr => sr.Location).WithMany(l => l.SubprocessRuns)
                .HasForeignKey(sr => sr.LocationId);

            e.HasOne(sr => sr.Subprocess).WithMany(s => s.SubprocessRuns)
                .HasForeignKey(sr => sr.SubprocessId);
        });

        // ProcessLogEntry
        modelBuilder.Entity<ProcessLogEntry>(e =>
        {
            e.HasOne(p => p.McpRun).WithMany(r => r.LogEntries)
                .HasForeignKey(p => p.McpRunId);

            e.HasOne(p => p.Location).WithMany(l => l.LogEntries)
                .HasForeignKey(p => p.LocationId);

            e.HasIndex(p => new { p.McpRunId, p.LocationId, p.ScriptName, p.StepName, p.Iteration });
        });

        // ScriptMapping
        modelBuilder.Entity<ScriptMapping>(e =>
        {
            e.HasOne(sm => sm.Location).WithMany(l => l.ScriptMappings)
                .HasForeignKey(sm => sm.LocationId);

            e.HasOne(sm => sm.Subprocess).WithMany(s => s.ScriptMappings)
                .HasForeignKey(sm => sm.SubprocessId);

            e.HasIndex(sm => new { sm.LocationId, sm.RawScriptPattern }).IsUnique();
        });

        // SlaTarget
        modelBuilder.Entity<SlaTarget>(e =>
        {
            e.HasOne(st => st.Location).WithMany(l => l.SlaTargets)
                .HasForeignKey(st => st.LocationId);

            e.HasOne(st => st.Subprocess).WithMany(s => s.SlaTargets)
                .HasForeignKey(st => st.SubprocessId);

            e.HasIndex(st => new { st.LocationId, st.SubprocessId }).IsUnique();
        });

        // LocationStepRegistry
        modelBuilder.Entity<LocationStepRegistry>(e =>
        {
            e.HasOne(r => r.Location).WithMany(l => l.StepRegistry)
                .HasForeignKey(r => r.LocationId);

            e.HasOne(r => r.Subprocess).WithMany(s => s.StepRegistry)
                .HasForeignKey(r => r.SubprocessId);

            e.HasIndex(r => new { r.LocationId, r.SubprocessId, r.RequiredStepName }).IsUnique();
        });

        // Issue
        modelBuilder.Entity<Issue>(e =>
        {
            e.HasOne(i => i.SubprocessRun).WithMany()
                .HasForeignKey(i => i.SubprocessRunId);
        });
    }
}
