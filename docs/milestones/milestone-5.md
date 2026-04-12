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

## Status: ⬚ Not Started
