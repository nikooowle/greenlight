# Milestone 2: Database Schema & Seed Data

## Plan

**Delivers:** Fully defined SQL Server database with 3 months of real production data loaded.

**Core tables:**
- `McpRun` — monthly run (id, month, year, status, startDate, endDate, EOM date)
- `Location` — ING locations (code, name, region, inScope flag)
- `Subprocess` — process definitions grouped by phase (name, phase: DataIngestion/Processing/Reporting, displayOrder, description)
- `SubprocessRun` — the matrix cells: status per subprocess per location per MCP run (locationId, subprocessId, mcpRunId, status, startedAt, completedAt, elapsed time)
- `Issue` — failures/incidents (subprocessRunId, rootCauseCategory, severity, description, incidentNumber, operatorComment, status)
- `SlaTarget` — deadline per subprocess (workday + time)
- `DataQualityCheck` — DQ rule results (for future DAS integration)

**Status values:** Not in Scope, Not Started, Running, Completed, Failed, Stopped, For Rerun

**Seed data:**
- Rose shares SQL query + 3 months of real production query results
- Schema created with Entity Framework Core (Code First)
- Seed script loads real data into local SQL Server Express/Developer (free)

## Status: ⬚ Not Started
