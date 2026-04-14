# Milestone 5: Subprocess Sidebar, SLA Predictions & Operator Tooling

## Plan

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

---

## Design additions (2026-04-15 — from brainstorm, prototype, and Rose's real-world scenarios)

### 1. Manual Override Layer (operator acknowledgment)

**Problem:** When Q3 has a bug and an operator manually runs a step in QRM (e.g. manual export), the work is done but never logged, so the dashboard is stuck at "Pending" forever. Current "fix" would be editing the production log table — a bad idea.

**Solution — separate override table, log data stays read-only:**

```sql
OperatorOverride {
  id, location_id, subprocess_id, step_name,
  action: enum('complete','skip','fail'),
  operator_id, completed_at,
  reason: text (required),
  ticket_ref: varchar (optional),     -- INC / JIRA
  evidence_url: varchar (optional),    -- file path / screenshot
  created_at, revoked_at
}
```

**Dashboard logic:**
```
finalStatus = (operatorOverride exists AND NOT revoked)
              ? override.action
              : computeFromQ3Logs(location, subprocess)
```

**UX (prototype in `compressed-v4.html`):**
- Hover any step in the drawer → `⋯` menu appears
- Click → dropdown with 3 actions: Manually mark complete · Mark as skipped (not needed) · Mark as failed (needs rerun)
- Modal: required reason, optional ticket ref, optional evidence URL
- Cell flips to green with a small `👤` badge indicating "human completed"
- Toast with [Undo] button (5-second window)
- Overridden steps show their reason inline in the drawer in italic green
- KPI card: "N Manual Overrides this run" (clickable → audit view)
- Bulk override button for multi-location scenarios (e.g. Q3 bug affecting 5 locations)

**Automation escape hatch:** once a manual override pattern repeats (same step, multiple months), build a file-watcher or QRM API poll to auto-detect completion → eliminate the manual step entirely. Override becomes the fallback for edge cases only.

### 2. Drift Detector for Golden Mapping Source (no more Excel editing)

**Problem:** When production changes (e.g., "Light Maintenance removed because servers are powerful enough now"), the golden source of mandatory steps becomes stale. Manual file edits are error-prone and lose audit trail.

**Solution — versioned registry + background drift detector + human approval inbox:**

```sql
SubprocessStepRegistry {
  id, location_id, subprocess_id, step_name,
  is_mandatory: bool,
  effective_from: DATE,
  effective_to: DATE,           -- null if current
  confidence: float,            -- % of recent months step appeared
  last_seen_month: varchar
}
```

**Drift rules (configurable per subprocess):**
| Trigger | Suggestion |
|---|---|
| Step not seen in 2+ months across all locations | Remove from mandatory |
| New step appears in >75% of locations for 2+ months | Promote to mandatory |
| Step present in one location, absent in others | Flag for review |
| Step completion rate drops sharply | Silent-breakage alert |

**Admin UI — "Suggested Changes" inbox:**
- Background job compares each month's runs to mandatory list
- Creates suggestions operators review one-click: [Approve] / [Dismiss] / [Postpone 1 month]
- Approved change: new row with `effective_from = today`, old row gets `effective_to = today`
- **Historical correctness preserved** — any rendering of past runs queries by date and gets the mandatory list that was in effect then (critical for banking audit)

**Dashboard integration:**
- When a cell shows "Pending" because one step is missing, drawer shows inline: "Waiting for: Light Maintenance — not seen in 2 months. [Review in admin]"
- One click from "why is this red" to "propose the rule update"

### 3. Progress indicator inside cells
- Show `3/4` (completed/total mandatory steps) on Running/Pending cells so operators see the blocker's step count, not just "running"
- Mini progress ring or fraction text in cell

### Why these additions matter for M5
Operator tooling is not just SLA dashboards — it's giving operators the **control** to keep the dashboard truthful even when Q3 lies (manual overrides) and to keep the rules themselves current without developer involvement (drift detector). These two patterns together eliminate the two biggest maintenance burdens in ops monitoring.

## Status: ⬚ Not Started (design locked 2026-04-15)
