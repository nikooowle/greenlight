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

---

## UX/UI refinements (designed 2026-04-15, to apply in future pass)

After reviewing the dashboard against the compressed golden mapping (27 subprocesses, highly sparse Location Specific columns), the following design direction was locked in via interactive prototypes under `docs/seed-data/matrix-view/`:

**Dense-by-default matrix:**
- Matrix shows only **Data Ingestion + Main Processes** (the dense, comparison-relevant columns) — ~20 columns
- **Location Specific** + sparse data collapsed to a single narrow **"Extras" column** with a status badge (e.g. green `●3`)
- Badge click → right-side **drawer** slides in showing the location's location-specific + quarterly runs with full step-level detail
- Location name click → drawer opens with **full focus view** (Data Ingestion + Main + Extras + Quarterly all in one pane)
- Matrix stays visible during drawer use (padding-right shift, not modal)

**Month-aware rendering:**
- At **quarter-end** months (Mar/Jun/Sep/Dec), Quarterly subprocesses (PDCE, EVE Optionality, Fair Value) render **inline** with Main Processes marked with a purple `Q` badge + column border
- At **regular** months, Quarterly columns are hidden entirely — dashboard shrinks visibly
- The Extras column only ever contains Location Specific items, never Quarterly

**Supporting patterns:**
- Top KPI cards (Active Locations, In-Scope Completed, Running, At SLA Risk, Failed, Manual Overrides) — clickable filters
- Pill toggles for each phase group (show/hide)
- Density switcher: Compact / Comfortable / Spacious
- Sticky first column (Location) and sticky header row
- Running status cells pulse visually
- Hover cell → subprocess name tooltip

**Progress indicator inside cells:** show `N/M` (e.g. `3/4`) when a subprocess is Running/Pending so operators see at-a-glance where the blocker is. Derived from `LocationStepRegistry` completion count.

**Role-aware defaults:**
| Role | Default view |
|---|---|
| Operator (country-level) | Their own location's drawer pre-opened |
| Coordinator / Lead | Full matrix |
| Management | KPI summary cards emphasized |
| Read-only audit | Full matrix, all actions disabled |

All roles can see all locations — dependencies across countries (e.g., ICDB-like consolidated runs) make global visibility valuable even for operators.

**Reference prototype:** `docs/seed-data/matrix-view/compressed-v3.html` (month-aware) — live at `http://localhost:4100/compressed-v3.html` when the preview server runs
