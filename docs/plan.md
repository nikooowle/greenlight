# ALM Greenlight — Milestone-Based Build Plan

## Context

ALM Risk Services at ING runs the Monthly Closing Process (MCP) — a standardized global production cycle that generates risk, capital, and regulatory reports for all ING locations. It runs workday 6–20 each month using QRM Risk Engine orchestrated by q3 scripts. Currently, status is communicated via chat and end-of-day email updates — both reactive and often outdated by the time they're read.

**ALM Greenlight** is a production monitoring dashboard built on two equally important pillars:

**1. Real-Time Production Visibility** — Replaces chat updates and end-of-day status emails with a live dashboard. Risk managers, CROs, and senior management get on-demand visibility into MCP progress, SLA compliance, and ongoing issues — anytime, without asking the operations team.

**2. Integrated Data Quality Control** — Connects with **DAS** (ING's data ETL tool) to catch data quality breaches before they cause damage. DQ issues discovered late (e.g., wrong positions or strategies on workday 17) can wipe out days of processing, requiring full restart. Greenlight surfaces DAS rule breaches on the dashboard with error details and incident numbers, shifting DQ from reactive to proactive — catching bad inputs early instead of after days of wasted processing.

Delays in MCP can lead to late regulatory reporting, missed steering committee inputs, and potential financial impact.

### Dashboard Structure: 3-Phase Matrix

The dashboard is a **Location × Process matrix** with 3 phases:

| Phase | What it tracks |
|-------|---------------|
| **1. Data Ingestion** | Data availability → Processed in DAS → DQ gates passed → Loaded to QRM |
| **2. Processing** | MCP subprocess completion in QRM (risk calculations), grouped into Main Processes, Location Specific, Quarterly Runs |
| **3. Reporting** | Whether each subprocess result has been published to Power BI (incremental per subprocess) |

**Rows** = ING Locations (15+)
**Cells** = Status icons (7 states: Not in Scope, Not Started, Running, Completed, Failed, Stopped, For Rerun)
**Priority:** Processing first → Data Ingestion (partial) → full DAS integration → Reporting

---

## Vibe Coding Basics (Read First)

### What Is Vibe Coding?
Building software by describing what you want in plain language, then letting AI write the code. Your job: describe outcomes, review results, steer corrections. Think of AI as a very fast, very literal contractor.

### How to Work Effectively
- **Describe outcomes, not code.** Say "a table of subprocesses with color-coded status badges" not "a React component with useState."
- **Work in small steps.** Complete one milestone, verify it works, then move on.
- **Always test.** Open `http://localhost:3000` after each milestone and click through everything.
- **Describe what you see when something breaks.** "The page shows a white screen" is better than "it doesn't work."
- **Ask for explanations.** Say "explain what this file does in plain language" anytime.

### Key Concepts (Not Code — Just Ideas)
| Concept | What It Is |
|---------|-----------|
| **Component** | A reusable UI building block (button, table, sidebar) — they snap together like LEGO |
| **Page** | A full screen the user sees (dashboard, issue list, etc.) — made of components |
| **API route** | A behind-the-scenes endpoint the page calls to get/send data from the database |
| **Database** | Where all data lives permanently — like a structured spreadsheet |
| **Schema** | The database structure — which tables exist and how they relate |
| **Seed data** | Fake but realistic data so you can see how the app looks before connecting real systems |

### Tips
1. Copy-paste error messages exactly — don't paraphrase
2. Use screenshots to describe visual problems
3. Say "do not change anything else" when you want a surgical fix
4. Commit after each milestone: tell AI "commit all changes with message 'Milestone X: description'"
5. Never approve changes you don't understand — ask the AI to explain

---

## Tech Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| Frontend | React + TypeScript | Aligned with what the tech team uses |
| Backend / API | ASP.NET Core Web API (C#) | Aligned with what the tech team uses |
| Styling | Tailwind CSS + shadcn/ui | Professional look with zero design effort |
| Animations (later) | Framer Motion | Smooth transitions and micro-interactions for the "expensive" feel — added in Milestone 11 |
| Database | Microsoft SQL Server | Matches team's existing database infrastructure |
| ORM | Entity Framework Core (Code First) | .NET standard for database access |
| Charts | Recharts | Timeline and progress visualizations |
| Notifications | Nodemailer or equivalent | Email alerts for SLA breaches |

### Design Direction (applied in Milestone 11)
- Dark theme primary — deep navy/slate backgrounds, crisp whites, accent colors for status
- Glassmorphism touches — frosted-glass card effects, soft shadows, depth layers
- Smooth animations — page transitions, cards fade/slide in, progress bars animate, status badges pulse
- Micro-interactions — hover effects, button scaling, animated number counters
- Premium typography — the kind of dashboard a CRO opens and thinks "this team has their act together"

**Early milestones (1–10):** Clean, professional basics using shadcn/ui defaults. Looks good, just not animated yet.

---

## The 11 Milestones

---

### Milestone 1: Project Setup & Skeleton

**Delivers:** A running app with the basic layout and skeleton of the 3-phase structure.

- Set up React + TypeScript frontend with Tailwind CSS + shadcn/ui
- Set up ASP.NET Core Web API backend project
- Basic layout: sidebar navigation + main content area
- Skeleton of the 3-phase grid: Data Ingestion | Processing | Reporting section headers with placeholder content
- Initialize Git repo
- Verify: frontend runs at `localhost:3000`, API runs at `localhost:5000`

**Complexity:** Low (30–60 min)

---

### Milestone 2: Database Schema & Seed Data

**Delivers:** Fully defined SQL Server database with 3 months of real production data loaded.

**Core tables:**
- `McpRun` — monthly run (id, month, year, status, startDate, endDate, EOM date)
- `Location` — ING locations (code, name, region, inScope flag)
- `Subprocess` — process definitions grouped by phase (name, phase: DataIngestion/Processing/Reporting, displayOrder, description)
- `SubprocessRun` — the matrix cells: status of each subprocess per location per MCP run (locationId, subprocessId, mcpRunId, status, startedAt, completedAt, elapsed time)
- `Issue` — failures/incidents (subprocessRunId, rootCauseCategory, severity, description, incidentNumber, operatorComment, status)
- `SlaTarget` — deadline per subprocess (workday + time)
- `DataQualityCheck` — DQ rule results (for future DAS integration)

**Status values:** Not in Scope, Not Started, Running, Completed, Failed, Stopped, For Rerun

**Seed data:**
- Rose shares SQL query + 3 months of real production query results
- Claude creates schema matching the query structure (Entity Framework Core)
- Seed script loads the real data into local SQL Server

**Setup:** Install SQL Server Express/Developer locally (free)

**Complexity:** Medium (60–90 min)

---

### Milestone 3: Data Simulator

**Delivers:** A fake data engine so the dashboard refreshes like it's live — essential for demos and testing.

- Uses 3 months of real production data as baseline for realistic patterns (timing, durations, failure rates)
- Simulates a new MCP run progressing: subprocesses move through statuses over time
- Configurable speed (e.g., 1 simulated hour = 10 real seconds)
- Occasionally injects failures and delays for realism
- Runs in background by default, with optional play/pause/speed controls for demos
- .NET background service updating SubprocessRun records in SQL Server on a timer
- Frontend picks up changes via polling/refresh

**Complexity:** Medium (60–90 min)

---

### Milestone 4: Processing Grid + Data Ingestion (Partial) Dashboard

**Delivers:** The main matrix view — your Excel prototype brought to life.

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
- UI approach for handling column density across 3 phases — tabs vs collapsible groups vs summary+drill-down. To be brainstormed when Excel data is shared
- Exact subprocess groupings and column names — from Excel file to be shared later

**Complexity:** Medium-High (90–120 min)

---

### Milestone 5: Subprocess Sidebar, SLA Predictions & Operator Tooling

**Delivers:** Click any grid cell → sidebar with full details, SLA predictions, and operator tooling.

**Sidebar contents:**
- SLA status: deadline, elapsed time, time remaining, RAG indicator
- Predicted finish time (weighted historical average from 3 months of data)
- SLA breach probability: on time / at risk / will likely breach
- ETA for overall MCP run completion
- Run details: start time, duration, expected duration
- **On failures:** root cause category (Data Quality / Configuration / Production Error / Tech), incident number, operator comment — required fields
- **On any status:** optional general notes field

**Grid cell SLA overlay (open design decision):**
- Each cell carries two layers: process status (cell color) + SLA risk (badge/border overlay)
- Example: green cell + clock badge = completed but late; blue cell + amber border = running but approaching SLA
- Early warning indicators for predicted breaches
- Exact visual approach (clock badge, RAG border, dot indicator) to be finalized during build

**SLA trend views:**
- Compliance over time per subprocess
- Consistently late subprocesses, slowest locations

**Prediction approach:**
- Start simple: weighted historical average from 3 months of data
- Upgrade later: server capacity, more advanced models

**API endpoints** to fetch/update subprocess details + SLA calculations

**Complexity:** High (120–150 min)

---

### Milestone 6: Notifications & Alerts

**Delivers:** Automated email and in-app alerts so the right people know immediately when something goes wrong.

- **Notification triggers:**
  - SLA breach: subprocess misses deadline
  - SLA warning: subprocess approaching deadline (configurable threshold)
  - Subprocess failure: task fails or stops
  - DQ failure: data quality check fails (wired up in Milestone 7)
  - MCP completion: full run completes
- **Recipient management:** configure who gets which alerts (by email, by role: operator, risk manager, management)
- **In-app notification bell:** unread count badge, dropdown panel with recent alerts, click to navigate to related item
- **Notification history log:** all sent alerts with timestamp, type, recipient, content
- **Email sending** via SMTP (can point at ING's mail relay)

**Complexity:** Medium-High (90–120 min)

---

### Milestone 7: Data Quality Integration with DAS

**Delivers:** The "DQ stopper" — catches bad inputs before they waste days of processing.

- Greenlight calls DAS API to pull DQ rule results
- Dashboard shows DQ gate status per input per location
- Failed gates: rule name, error description, incident number, affected source, severity
- Details accessible via sidebar on click
- Start with 1–5 rules, expand as data product lead defines more
- DQ failures trigger notifications (wired from Milestone 6)

**Open design decisions (parked for later brainstorming):**
- Uniform color system across all phases — red/green/amber must mean the same thing everywhere
- Data Ingestion detail level: 4 stages (Available → DAS Processed → DQ Passed → Loaded to QRM) × many inputs per location is too dense for columns. Need to brainstorm: summary cells with hover/sidebar drill-down? Collapsed groups? Separate tab? To be decided when full data picture is available

**Complexity:** Medium-High (90–120 min)

---

### Milestone 8: Analytics, Trending & Multi-Month Views

**Delivers:** Operational insights from historical data — what's getting better, what's getting worse, where are the patterns.

- **Multi-month comparison:** this run vs previous months, duration trends per subprocess
- **Location analysis:** which locations are consistently problematic
- **Issue analytics:** root cause breakdown over time, most common failure points (subprocess × location), resolution time trends
- **SLA analytics:** compliance % trending, consistently breaching subprocesses, improvement/degradation tracking
- **For operators AND management** — actionable patterns for daily operations + steering improvement points
- **Data review & edit:** operators can review and correct analytics data before it's used — e.g., exclude outliers (50hr bug run that was actually 2hrs), adjust actual durations, flag data points as anomalies. Analytics recalculates based on corrected data
- Exportable data for further analysis

**Complexity:** Medium-High (90–120 min)

---

### Milestone 9: Reporting Phase — Power BI Publication Tracking

**Delivers:** The third phase added to the grid — tracking whether subprocess results are published to Power BI.

- Reporting columns in the matrix per subprocess
- Status: Published / Not Published / Not in Scope
- Incremental: shows which results are available vs still pending
- Pulls publication status from existing tracking source (details TBD)
- Sidebar on click: subprocess details, publication timestamp, link to Power BI report

**Open design decisions:**
- 10 subprocesses × 15 locations = 150 potential columns — same density challenge as Data Ingestion. Need to brainstorm compressed UI (summary cells, hover/sidebar drill-down, tabs, etc.)
- Exact source/API for publication status to be confirmed with tech team

**Complexity:** Medium (60–90 min)

---

### Milestone 10: Process Dependency Visualization (Nice-to-Have)

**Delivers:** Visual diagram of q3 script subprocess dependencies for rerun planning.

- Mermaid-style flowchart showing subprocess dependencies (A before B, B and C in parallel, etc.)
- Current status highlighted on each node (completed, running, failed)
- Rerun impact: if a subprocess needs rerun, shows affected downstream subprocesses
- Useful for input redelivery: "positions corrected on workday 12 — what do we rerun?"
- Available from sidebar or dedicated page
- Per-location or generic process model view

**Complexity:** Medium (60–90 min)

---

### Milestone 11: Premium Design Polish, Auth & Deployment

**Delivers:** The "expensive-looking" product — polished, secured, and ready for production.

**Premium design:**
- Framer Motion animations: page transitions, cards fade/slide in, progress bars animate, status badges pulse
- Glassmorphism: frosted-glass card effects, soft shadows, depth layers
- Micro-interactions: hover effects, button scaling, animated number counters
- Dark theme primary: deep navy/slate, crisp whites, accent colors
- Premium typography and spacing
- Chart polish: animated draws, glowing accent lines, gradient fills

**Authentication:**
- Login system with roles: admin, operator, risk manager
- Role-based access: operators see tooling, management sees analytics, admin sees everything

**Deployment:**
- Containerization / deployment config
- Environment variable documentation
- README with setup instructions for tech team
- Status update API endpoint for future q3 integration
- Responsive design

**Complexity:** High (120–150 min)

---

## Summary

| # | Milestone | Effort |
|---|-----------|--------|
| 1 | Project Setup & Skeleton | 30–60 min |
| 2 | Database Schema & Seed Data | 60–90 min |
| 3 | Data Simulator | 60–90 min |
| 4 | Processing Grid + Data Ingestion (Partial) Dashboard | 90–120 min |
| 5 | Subprocess Sidebar, SLA Predictions & Operator Tooling | 120–150 min |
| 6 | Notifications & Alerts | 90–120 min |
| 7 | Data Quality Integration with DAS | 90–120 min |
| 8 | Analytics, Trending & Multi-Month Views | 90–120 min |
| 9 | Reporting Phase — Power BI Publication Tracking | 60–90 min |
| 10 | Process Dependency Visualization (nice-to-have) | 60–90 min |
| 11 | Premium Design Polish, Auth & Deployment | 120–150 min |
| | **Total** | **~14–18 hours** |

## Open Design Decisions (Parked for Brainstorming)

1. **Column density across phases:** Data Ingestion has 4 stages × many inputs; Reporting has subprocesses × locations. Too dense for raw columns. Options: tabs, collapsible groups, summary cells with hover/sidebar drill-down.
2. **Uniform color system:** Red/green/amber must mean the same thing across all phases (process status vs DQ failure vs SLA breach).
3. **Grid cell SLA overlay:** Two-layer cells showing process status + SLA risk. Exact visual approach TBD.
4. **Data Ingestion detail level:** How much of the 4-stage pipeline (Available → DAS Processed → DQ Passed → Loaded to QRM) to show at grid level vs sidebar.

## Project Documentation Structure (13 files total)

```
D:\greenlight/
  docs/
    plan.md                        ← full project plan (this document)
    backlog.md                     ← living status tracker — auto-updated after each milestone
    milestones/
      milestone-1.md               ← starts as plan, grows into full record after completion
      milestone-2.md
      ...
      milestone-11.md
```

### How Each milestone.md Works

Each file starts as a plan, then Claude adds completion details to the **same file** after building:

```markdown
# Milestone 4: Processing Grid + Data Ingestion (Partial)

## Plan
- Build the processing grid matrix view...
- [detailed build instructions]

## Status: ✅ Complete (2026-04-15)

## What Was Built
- Created ProcessingGrid component at frontend/src/components/...
- API endpoint at backend/Controllers/...
- Key decisions: used tab view for phase switching

## Key Files
- frontend/src/components/ProcessingGrid.tsx
- backend/Controllers/McpRunController.cs
```

### backlog.md — Living Status Tracker

Auto-updated by Claude after each milestone completes:

```markdown
# ALM Greenlight — Backlog

| # | Milestone | Status | Completed |
|---|-----------|--------|-----------|
| 1 | Project Setup & Skeleton | ✅ Done | 2026-04-13 |
| 2 | Database Schema & Seed Data | ✅ Done | 2026-04-14 |
| 3 | Data Simulator | 🔄 In Progress | — |
| 4 | Processing Grid + Data Ingestion (Partial) | ⬚ Not Started | — |
| ... | ... | ... | ... |
```

### backlog.md Also Tracks App Structure

At the bottom of `backlog.md`, Claude maintains a **Current App Structure** section — a quick map of the main folders and files. Updated after each milestone. Any new session reads this and immediately knows where everything lives:

```markdown
## Current App Structure
frontend/
  src/
    components/ProcessingGrid.tsx    ← the main matrix grid (Milestone 4)
    components/SubprocessSidebar.tsx  ← click-cell sidebar (Milestone 5)
    pages/Dashboard.tsx              ← main page (Milestone 4)
backend/
  Controllers/McpRunController.cs    ← grid data API (Milestone 4)
  Services/SlaPredictionService.cs   ← SLA calculations (Milestone 5)
  Services/SimulatorService.cs       ← data simulator (Milestone 3)
```

### Why This Saves Tokens

Starting a new Claude session at Milestone 6? Instead of scanning all code:
1. Claude reads `backlog.md` — knows what's done, what's next, AND where all key files are
2. Claude reads `milestone-5.md` — most recent completed milestone for context
3. Claude reads `milestone-6.md` — what to build now
4. Only 3 files, minimal tokens, full context

### Workflow Per Milestone

1. Claude reads `milestone-X.md` (the plan section)
2. Claude builds it
3. Claude adds completion details to the same `milestone-X.md`
4. Claude updates `backlog.md` status
5. Git commit: "Milestone X: description"

## How to Start

### One-time setup: Cursor + Anthropic API key
1. Open Cursor → Settings (gear icon) → Models
2. Add your Anthropic API key (from console.anthropic.com)
3. Select Claude as the model
4. Now you can chat with Claude in Cursor's sidebar — it sees your code and edits files directly

### Building
1. Open Cursor in `D:\greenlight`
2. Give the AI the Milestone 1 prompt in the chat sidebar
3. When done, verify frontend runs at `localhost:3000` and API at `localhost:5000`
4. Claude updates `milestone-1.md` with completion details and updates `backlog.md`
5. Git commit: "Milestone 1: Project setup"
6. Move to Milestone 2

**Milestone 1 starter prompt:**
> "Set up a React + TypeScript frontend with Tailwind CSS and shadcn/ui, and an ASP.NET Core Web API backend project. Create a basic layout with sidebar navigation (Dashboard, Issues, SLA, Data Quality, Analytics, Notifications, Management) and a main content area. The main page should show 'ALM Greenlight' with subtitle 'MCP Production Monitoring Dashboard' and a skeleton 3-phase grid with section headers: Data Ingestion | Processing | Reporting. Initialize a Git repo. Create docs/backlog.md with all 11 milestones listed as Not Started."

## Inputs Rose Will Provide

- Excel prototype with subprocess names and structure
- SQL query + 3 months of production query results (subprocess durations, statuses, locations)
- Production logs (format and logic to be discussed)
- DAS API details (from data/tech team)
- Power BI publication tracking source (from tech team)
