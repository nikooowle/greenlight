# Milestone 2: Database Schema & Seed Data

## Plan

**Delivers:** Fully defined SQLite database with 1 month of real production data loaded.

**Core tables (9 total):**
- `McpRun` — monthly run (id, reportMonth, year, month, status, startDate, endDate, EOM date)
- `Location` — 16 ING locations (code, name, region, inScope flag)
- `Subprocess` — 39 standardized process definitions grouped by phase (name, phase: DataIngestion/Processing/Reporting, displayOrder, description)
- `SubprocessRun` — 183 matrix cells: aggregated status per subprocess per location per MCP run (locationId, subprocessId, mcpRunId, status, startedAt, completedAt, elapsed time, completedSteps, totalRequiredSteps)
- `ProcessLogEntry` — 979 raw log entries from SQL extract (scriptName, stepName, stateName, timing, error messages, runtime metrics)
- `ScriptMapping` — maps dynamic script names to standardized subprocess names (rawScriptPattern + location -> subprocess)
- `SlaTarget` — 267 SLA deadlines per subprocess per location (frequency, workday, deadline, slaDate)
- `LocationStepRegistry` — golden source: mandatory completion steps per location per subprocess
- `Issue` — failures/incidents (subprocessRunId, rootCauseCategory, severity, description, incidentNumber, operatorComment, status)

**Status values:** Not in Scope, Not Started, Running, Completed, Failed, Stopped, For Rerun

**Seed data:**
- 1 month of real production data (Jan 2026 / reportMonth 2601)
- Excel data exported to JSON, loaded by C# SeedData class on startup
- Script name → subprocess mapping via prefix matching (268 unique scripts → 39 subprocesses)
- Auto-derived subprocess run matrix from log entry aggregation

**API Endpoints created:**
- `GET /api/mcp-runs/current` — latest MCP run with stats
- `GET /api/locations` — all locations
- `GET /api/subprocesses` — all subprocesses by phase
- `GET /api/mcp-runs/{reportMonth}/matrix` — subprocess run matrix
- `GET /api/mcp-runs/{reportMonth}/logs?location=` — process log entries
- `GET /api/sla-targets` — SLA deadlines

**Tech stack:**
- Entity Framework Core 8 (Code First) with SQLite
- Auto-migration + seed on startup
- JSON seed files under `backend/Data/SeedData/`

## Status: ✅ Done — 2026-04-12
