# Milestone 3: Data Simulator

## Plan

**Delivers:** A fake data engine so the dashboard refreshes like it's live — essential for demos and testing.

- Uses 3 months of real production data as baseline for realistic patterns (timing, durations, failure rates)
- Simulates a new MCP run progressing: subprocesses move through statuses over time
- Configurable speed (e.g., 1 simulated hour = 10 real seconds)
- Occasionally injects failures and delays for realism
- Runs in background by default, with optional play/pause/speed controls for demos
- .NET background service updating SubprocessRun records in SQL Server on a timer
- Frontend picks up changes via polling/refresh

## Status: ⬚ Not Started
