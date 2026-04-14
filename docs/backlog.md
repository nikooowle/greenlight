# ALM Greenlight — Backlog

| # | Milestone | Status | Completed |
|---|-----------|--------|-----------|
| 1 | Project Setup & Skeleton | ✅ Done | 2026-04-12 |
| 2 | Database Schema, Seed Data & Golden Mapping | ✅ Done | 2026-04-15 |
| 3 | Data Simulator | ✅ Done | 2026-04-12 |
| 4 | Processing Grid + Data Ingestion (Partial) Dashboard | ✅ Done | 2026-04-13 |
| 5 | Subprocess Sidebar, SLA Predictions & Operator Tooling | ⬚ Not Started | — |
| 6 | Notifications & Alerts | ⬚ Not Started | — |
| 7 | Data Quality Integration with DAS | ⬚ Not Started | — |
| 8 | Analytics, Trending & Multi-Month Views | ⬚ Not Started | — |
| 9 | Reporting Phase — Power BI Publication Tracking | ⬚ Not Started | — |
| 10 | Process Dependency Visualization (nice-to-have) | ⬚ Not Started | — |
| 11 | Premium Design Polish, Auth & Deployment | ⬚ Not Started | — |

## Current Status

**Locked foundations:**
- Backend API + DB schema (M2)
- Data simulator (M3)
- Dashboard skeleton with live polling (M4)
- **Golden Source Mapping** — finalized using 4 months of production data, 27 subprocesses across 15 locations, with Main/Location Specific/Quarterly categorization and month-aware rendering rules

**Follow-up before M5:**
- ⚠ Backend seed still uses the pre-refinement 2601-only mapping. Refresh `SeedData.cs` prefix rules + seed JSONs + populate `LocationStepRegistry` from `golden-source-final-v6.xlsx` before building SLA / completion logic

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
      log-entries.json                ← production log entries (needs refresh with 4-month data)
      sla-targets.json                ← SLA deadline mappings
  Models/
    McpRun.cs · Location.cs · Subprocess.cs · SubprocessRun.cs
    ProcessLogEntry.cs · ScriptMapping.cs · SlaTarget.cs
    LocationStepRegistry.cs · Issue.cs
  Migrations/                         ← EF Core migration (InitialCreate)
  greenlight.db                       ← SQLite database (auto-created on startup)
docs/
  plan.md                            ← full project plan
  backlog.md                         ← this file
  milestones/                        ← 11 milestone files
  seed-data/
    production data 1 month.xlsx       ← original 2601 source
    q3 logs.xlsx                       ← 3-month source (2512, 2602, 2603)
    golden-source-final-v6.xlsx        ← ★ authoritative golden mapping
    consistency-analysis.xlsx          ← 4-month step consistency check
    matrix-data.json                   ← scope matrix + groupings for prototypes
    build-final-mapping.js             ← script to regenerate golden source
    build-consistency-analysis.js      ← script to regenerate consistency report
    matrix-view/                       ← interactive HTML design prototypes
      compressed-v4.html                ← latest: month-aware + manual overrides
      compressed-v3.html                ← month-aware dashboard
      compressed-v2.html                ← badge + drawer pattern
      compressed.html                   ← initial compressed design
      index.html                        ← scope matrix view (v6)
```
