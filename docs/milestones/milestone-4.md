# Milestone 4: Processing Grid + Data Ingestion (Partial) Dashboard

## Plan

**Delivers:** The main matrix view — the Excel prototype brought to life.

**The matrix grid:**
- Rows = Locations (BEGT, DEST, NLBTR, AUDB, BEDB, etc.)
- Columns = Subprocesses grouped into sections:
  - **Data Ingestion (partial):** "Loaded to QRM" status per input per location only
  - **Processing:** Main Processes, Location Specific, Quarterly Runs
- Cells = Status icons (7 states) — color-coded matching the Excel prototype

**Header bar:**
- Current MCP run identifier (e.g., "March 2026 MCP"), EOM date, current workday
- Overall progress summary

**RAG status logic:**
- Green = Completed, Red = Failed, Amber = Running but approaching SLA, Gray = Not Started / Not in Scope

**Dynamic Run Status Indicator** legend (matching Excel prototype)

**API endpoints:** .NET Web API serving grid data from SQL Server, refreshed by simulator

**Open design decisions:**
- UI approach for handling column density across 3 phases — tabs vs collapsible groups vs summary+drill-down
- Exact subprocess groupings and column names — from Excel file to be shared

## Status: ✅ Done — 2026-04-13

### What was built

**Frontend-backend integration (8 new files, 3 modified):**

- **Vite proxy** — `/api` requests proxy to backend on port 5176
- **TypeScript types** — `McpRun`, `Location`, `Subprocess`, `MatrixCell`, `SimulatorStatus`
- **API client** — Thin fetch wrappers for all 12 endpoints (`lib/api.ts`)
- **Polling hooks** — `usePolling` (generic), `useSimulator` (status + controls), `useDashboard` (run + matrix + locations)
- **Processing Grid** — Tabbed matrix (Data Ingestion / Processing / Reporting), locations as rows, subprocesses as columns with rotated headers, 7-color status cells with tooltips, sticky location column, tab badges showing completed/total counts
- **Simulator Controls** — Play/Pause/Resume/Reset buttons, speed selector (1x/10x/60x/200x/1000x), phase indicator
- **Status Legend** — All 7 statuses with color-coded icons
- **Live metric cards** — Completed, Running, Failed, Waiting, Issues, Locations — all computed from real matrix data
- **Header** — Shows current MCP run month, EOM date, run status badge
- **Auto-refresh** — Dashboard polls every 2s, simulator status every 1s

**UI approach:** Tabbed phases (user's choice) — one phase visible at a time with horizontal scroll for many columns
