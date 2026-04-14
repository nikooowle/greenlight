# Milestone 2: Database Schema, Seed Data & Golden Mapping Source

## Plan

**Delivers:** Fully defined SQLite database seeded with real production data + the **Golden Source Mapping** that drives completion logic, scope detection, and the dashboard view.

---

## Part A — Database (initial build, done 2026-04-12)

**Core tables (9 total):**
- `McpRun` — monthly run (id, reportMonth, year, month, status, startDate, endDate, EOM date)
- `Location` — ING locations (code, name, region, inScope flag)
- `Subprocess` — standardized process definitions grouped by phase (name, phase, displayOrder, description)
- `SubprocessRun` — the matrix cells: aggregated status per subprocess per location per MCP run
- `ProcessLogEntry` — raw log entries from SQL extract (scriptName, stepName, stateName, timing, error messages, runtime metrics)
- `ScriptMapping` — maps dynamic script names to standardized subprocess names
- `SlaTarget` — SLA deadlines per subprocess per location (frequency, workday, deadline, slaDate)
- `LocationStepRegistry` — golden source: mandatory completion steps per location per subprocess
- `Issue` — failures/incidents

**Status values:** Not in Scope, Not Started, Running, Completed, Failed, Stopped, For Rerun

**API Endpoints:**
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

---

## Part B — Golden Mapping Source (refined 2026-04-14/15)

**Problem this solves:** Production log scripts use dynamic names (e.g. `KR_AU_MCP rr due to tech issue`, `EC_BE_MCP rr`) that change across reruns and locations. The dashboard needs a stable mapping of **raw script → standardized subprocess** per location, plus the list of mandatory steps that define "completion" per cell.

**Data used:** 4 months of real production logs (Dec 2025 → Mar 2026):
- `2512` — Dec 2025 (Q4 end) — 1,273 log entries
- `2601` — Jan 2026 (mid-quarter) — 979 log entries
- `2602` — Feb 2026 (mid-quarter) — 1,376 log entries
- `2603` — Mar 2026 (Q1 end) — 598 log entries

### Mapping logic (the rules)

1. **Prefix-based script → subprocess mapping** — ~50 prefix rules covering all non-adhoc production scripts
2. **Step-level split** — scripts that combine two subprocesses (e.g. NLRB's Fair Value + PDCE) are split by step prefix patterns
3. **Per-location merge across all scripts** — all scripts mapping to the same `(location, subprocess)` are grouped; steps are merged in chronological order across runs/reruns
4. **Union across months** — final step list for a location+subprocess is the union of unique steps across all 4 months (handles partial-month snapshots like 2603)
5. **Quarterly auto-detection** — subprocesses that appear only in quarter-end months (2512, 2603) and never in mid-quarter (2601, 2602) → tagged `Quarterly`
6. **Main vs Location Specific threshold** — subprocess appearing in more than half of locations (≥8 of 15) → `Main Process`, else `Location Specific`

### ADHOC removal rules (what's NOT in the golden source)

- **ICDB** location entirely (all subprocesses, all scripts)
- **GAP ITS Planning** subprocess entirely
- **Load Position (RAS)** subprocess entirely (was 2 locations, all adhoc)
- **RAS scripts at NLBTR, BEGT, DEGT** (e.g. `Planning_DEGT_MCP_RAS`, `Load_POS_NLBTR_MCP_RAS`)
- **AUDB CSRBB Export + TRISS** (adhoc one-offs for AUDB only)
- **Single-step `CSRBB exports` scripts** without `_Export_updated` suffix
- Prefix-based: `ICDB_MCP`, `Copy_dbwruns`, `Copydbworuns`, `Copy_variables`, `CSRBB_Sideload`, `LAD_SF`

### Final golden mapping shape

| Metric | Value |
|---|---|
| Total locations | 15 (ICDB removed) |
| Total subprocesses | 27 (down from 39 initial) |
| Main Process subprocesses | 15 monthly + 2 quarterly (PDCE, EVE Optionality) |
| Location Specific subprocesses | 9 monthly + 1 quarterly (Fair Value) |
| In-scope location+subprocess cells | 223 (down from 405 sparse grid) |
| ADHOC entries removed | 123 |
| Still unmapped scripts flagged for review | 17 |

### Month-awareness (design decision for dashboard)

- **Quarter-end months** (Mar, Jun, Sep, Dec): quarterly subprocesses render inline with Main Processes, marked with a `Q` badge + purple column border
- **Regular months** (Jan, Feb, Apr, May, Jul, Aug, Oct, Nov): quarterly columns hidden entirely — dashboard is visibly smaller
- Logic is data-driven: a subprocess is "active this month" if it has any `ProcessLogEntry` for the current `reportMonth`

### Subprocess completion logic (the "Golden Source Check")

For each grid cell `(Location, Subprocess)`:
1. Fetch mandatory step list from `LocationStepRegistry`
2. Find the latest iteration of log entries for that `(Location, Subprocess)`
3. Evaluate:
   - All mandatory steps have `stateName='Completed'` → ✓ **Completed**
   - Any step in latest iter = 'Failed' → ✕ **Failed**
   - Any step = 'Running' → ▶ **Running**
   - Some done, others pending → ⏳ **Pending** (with `N/M` progress)
   - No log entries → — **Not Started**
   - Location not in golden source for this subprocess → (salmon) **Not in Scope**

---

## Artifacts produced

| File | Purpose |
|---|---|
| `docs/seed-data/golden-source-final-v6.xlsx` | **Authoritative golden source** — 6 sheets: Final Mapping, Subprocess Order, Step Evolution, Scope Matrix, ADHOC Removed, Still Unmapped |
| `docs/seed-data/consistency-analysis.xlsx` | 4-month step consistency check — flags which steps appear in every month vs only some |
| `docs/seed-data/matrix-data.json` | Scope matrix + subprocess groupings consumed by dashboard prototypes |
| `docs/seed-data/build-final-mapping.js` | Script that regenerates the golden source from `q3 logs.xlsx` + `log-entries.json` (reproducible) |
| `docs/seed-data/build-consistency-analysis.js` | Script that regenerates consistency report |
| `docs/seed-data/q3 logs.xlsx` | Source production data — 3 months (2512, 2602, 2603) |
| `docs/seed-data/log-entries.json` | Source production data — 1 month (2601) |

---

## Follow-up work required before Milestone 5

> ⚠ **The backend's seed data and `ScriptMapping` rules still reflect the initial 2601-only mapping (pre-refinement).** Before the next milestone that depends on golden source accuracy, refresh:
> - `backend/Data/SeedData.cs` → update the prefix rules array to match `build-final-mapping.js`
> - `backend/Data/SeedData/log-entries.json` + new monthly JSONs from `q3 logs.xlsx`
> - Re-seed to apply the Main Process / Location Specific / Quarterly tags
> - Populate `LocationStepRegistry` with the step lists from the final mapping so completion-check logic works correctly

## Status: ✅ Done — 2026-04-12 (initial DB), refined 2026-04-15 (golden mapping)
