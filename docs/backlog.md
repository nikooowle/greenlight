# ALM Greenlight — Backlog

| # | Milestone | Status | Completed |
|---|-----------|--------|-----------|
| 1 | Project Setup & Skeleton | ✅ Done | 2026-04-12 |
| 2 | Database Schema & Seed Data | ✅ Done | 2026-04-12 |
| 3 | Data Simulator | ⬚ Not Started | — |
| 4 | Processing Grid + Data Ingestion (Partial) Dashboard | ⬚ Not Started | — |
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
    App.tsx                          ← main app with layout
    components/Sidebar.tsx           ← collapsible dark sidebar
    components/MetricCards.tsx        ← 6 placeholder metric cards
    components/PhaseGrid.tsx         ← 3-phase overview cards
    lib/utils.ts                     ← cn() utility
  vite.config.ts                     ← port 3000, Tailwind, @ alias
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
