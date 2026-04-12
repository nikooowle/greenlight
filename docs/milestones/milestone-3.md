# Milestone 3: Data Simulator

## Plan

**Delivers:** A live data simulation engine so the dashboard refreshes like a real MCP run is progressing — essential for demos and testing.

**Implementation:**
- .NET `BackgroundService` that creates a new MCP run and progressively advances subprocesses through their lifecycle
- Uses real Jan 2026 data as a template: which location+subprocess combos are in scope, realistic phase ordering
- Simulates DataIngestion → Processing → Reporting phase order with realistic durations
- ~8% random failure rate with production-like error messages
- Configurable speed (default 60x, supports 1x–1000x)
- Play/pause/speed controls via API

**Simulator API Endpoints:**
- `GET /api/simulator/status` — current state (running/paused, speed, progress %, current subprocess)
- `POST /api/simulator/start` — begin a new simulated run
- `POST /api/simulator/pause` — pause simulation
- `POST /api/simulator/resume` — resume simulation
- `POST /api/simulator/speed/{multiplier}` — change speed (e.g. 60, 300, 500)
- `POST /api/simulator/reset` — stop and clean up simulated run

**Files created:**
- `backend/Services/SimulatorState.cs` — Thread-safe singleton holding sim state
- `backend/Services/SimulatorService.cs` — BackgroundService with simulation loop

**Files modified:**
- `backend/Program.cs` — Register services + 6 new control endpoints

**Behavior:**
- Auto-starts on server launch (creates next month's run)
- Pre-creates all SubprocessRun rows matching real data scoping (in-scope vs "Not in Scope")
- Steps progress: Not Started → Running → Completed (or Failed)
- Updates CompletedSteps/TotalRequiredSteps for progress bars
- Sets run status to "Completed with Failures" if any subprocess failed

## Status: ✅ Done — 2026-04-12
