# Milestone 7: Data Quality Integration with DAS

## Plan

**Delivers:** The "DQ stopper" — catches bad inputs before they waste days of processing.

- Greenlight calls DAS API to pull DQ rule results
- Dashboard shows DQ gate status per input per location
- Failed gates: rule name, error description, incident number, affected source, severity
- Details accessible via sidebar on click
- Start with 1–5 rules, expand as data product lead defines more
- DQ failures trigger notifications (wired from Milestone 6)

**Open design decisions (parked for later brainstorming):**
- Uniform color system across all phases — red/green/amber must mean the same thing everywhere
- Data Ingestion detail level: 4 stages (Available → DAS Processed → DQ Passed → Loaded to QRM) x many inputs per location is too dense for columns. Need to brainstorm: summary cells with hover/sidebar drill-down? Collapsed groups? Separate tab?

## Status: ⬚ Not Started
