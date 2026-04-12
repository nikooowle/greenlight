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

## Status: ⬚ Not Started
