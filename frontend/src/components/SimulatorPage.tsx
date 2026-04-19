import { SimulatorControls } from "@/components/SimulatorControls"
import { SimProgressBar } from "@/components/SimProgressBar"
import { ScriptedEventsPanel } from "@/components/ScriptedEventsPanel"
import { DelayForm } from "@/components/DelayForm"
import { SlowForm } from "@/components/SlowForm"
import { HoldForm } from "@/components/HoldForm"
import { FailForm } from "@/components/FailForm"
import { useSimulator } from "@/hooks/useSimulator"
import { useQueue } from "@/hooks/useQueue"

export function SimulatorPage() {
  const {
    status: simStatus,
    start,
    pause,
    resume,
    reset,
    setSpeed,
    setMode,
  } = useSimulator()
  const { queue, cancel: cancelEvent, release: releaseEvent } = useQueue()

  return (
    <main className="flex-1 overflow-auto">
      {/* Page header */}
      <header className="border-b border-slate-800 px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-white">Simulator Control</h1>
            <p className="text-sm text-slate-400">
              Pre-stage scripted incidents, pick a mode, start a run. Dashboard stays clean for stakeholders.
            </p>
          </div>
        </div>
      </header>

      {/* Controls + scripted events */}
      <div className="p-6 space-y-6">
        <div className="border border-slate-800 rounded-md bg-slate-900/50 p-4 space-y-4">
          <h2 className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Run controls</h2>
          <SimulatorControls
            status={simStatus}
            onStart={start}
            onPause={pause}
            onResume={resume}
            onReset={reset}
            onSpeed={setSpeed}
            onMode={setMode}
          />
          <SimProgressBar status={simStatus} />
        </div>

        <ScriptedEventsPanel
          events={queue}
          onCancel={cancelEvent}
          onRelease={releaseEvent}
        />

        {/* Injection forms — one per action type. */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <SlowForm />
          <DelayForm />
          <HoldForm />
          <FailForm />
        </div>
      </div>
    </main>
  )
}
