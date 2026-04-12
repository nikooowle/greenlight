# ALM Greenlight — Backlog

| # | Milestone | Status | Completed |
|---|-----------|--------|-----------|
| 1 | Project Setup & Skeleton | ✅ Done | 2026-04-12 |
| 2 | Database Schema & Seed Data | ✅ Done | 2026-04-12 |
| 3 | Data Simulator | ✅ Done | 2026-04-12 |
| 4 | Processing Grid + Data Ingestion (Partial) Dashboard | ✅ Done | 2026-04-13 |
| 5 | Subprocess Sidebar, SLA Predictions & Operator Tooling | ⬚ Not Started | — |
| 6 | Notifications & Alerts | ⬚ Not Started | — |
| 7 | Data Quality Integration with DAS | ⬚ Not Started | — |
| 8 | Analytics, Trending & Multi-Month Views | ⬚ Not Started | — |
| 9 | Reporting Phase — Power BI Publication Tracking | ⬚ Not Started | — |
| 10 | Process Dependency Visualization (nice-to-have) | ⬚ Not Started | — |
| 11 | Premium Design Polish, Auth & Deployment | ⬚ Not Started | — |

## Current App Structure

```
frontend/
  src/
    App.tsx                          ← main app, wires dashboard + simulator hooks
    types.ts                         ← TypeScript interfaces for API responses
    components/Sidebar.tsx           ← collapsible dark sidebar
    components/MetricCards.tsx        ← 6 live metric cards (from matrix data)
    components/ProcessingGrid.tsx    ← tabbed location×subprocess matrix grid
    components/StatusLegend.tsx      ← 7-color status legend bar
    components/SimulatorControls.tsx  ← play/pause/reset/speed controls
    components/PhaseGrid.tsx         ← (legacy) 3-phase overview cards
    hooks/usePolling.ts              ← generic polling hook
    hooks/useDashboard.ts            ← fetches run, matrix, locations, subprocesses
    hooks/useSimulator.ts            ← simulator status + control actions
    lib/api.ts                       ← fetch wrappers for all API endpoints
    lib/utils.ts                     ← cn() utility
  vite.config.ts                     ← port 3000, proxy /api → backend:5176
backend/
  Program.cs                         ← API: 6 endpoints (health, runs, matrix, logs, locations, SLA)
  Data/
    GreenlightContext.cs              ← EF Core DbContext (9 tables)
    SeedData.cs                       ← Auto-seeds from JSON on startup
    SeedData/
      log-entries.json                ← 979 production log entries (Jan 2026)
      sla-targets.json                ← 267 SLA deadline mappings
  Models/
    McpRun.cs                         ← Monthly run cycle
    Location.cs                       ← 16 ING locations
    Subprocess.cs                     ← 39 standardized processes
    SubprocessRun.cs                  ← Matrix cells (location × subprocess status)
    ProcessLogEntry.cs                ← Raw SQL extract rows
    ScriptMapping.cs                  ← Dynamic script name → subprocess mapping
    SlaTarget.cs                      ← SLA deadlines per location × subprocess
    LocationStepRegistry.cs           ← Golden source: mandatory completion steps
    Issue.cs                          ← Failure/incident tracking
  Migrations/                         ← EF Core migration (InitialCreate)
  greenlight.db                       ← SQLite database (auto-created on startup)
docs/
  plan.md                            ← full project plan
  backlog.md                         ← this file
  milestones/                        ← 11 milestone files
  seed-data/                         ← production Excel + exported JSON
```
