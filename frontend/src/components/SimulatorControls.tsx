import { Play, Pause, RotateCcw, Zap } from "lucide-react"
import { cn } from "@/lib/utils"
import type { SimulatorStatus } from "@/types"

const SPEEDS = [1, 10, 60, 200, 1000]

interface SimulatorControlsProps {
  status: SimulatorStatus | null
  onStart: () => void
  onPause: () => void
  onResume: () => void
  onReset: () => void
  onSpeed: (n: number) => void
}

export function SimulatorControls({
  status,
  onStart,
  onPause,
  onResume,
  onReset,
  onSpeed,
}: SimulatorControlsProps) {
  if (!status) return null

  const isIdle = !status.isRunning && !status.isPaused && status.phase === "Idle"
  const isRunning = status.isRunning && !status.isPaused
  const isPaused = status.isPaused

  return (
    <div className="flex items-center gap-3">
      {/* Play/Pause */}
      {isIdle && (
        <button
          onClick={onStart}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium transition-colors"
        >
          <Play className="h-3.5 w-3.5" />
          Start
        </button>
      )}
      {isRunning && (
        <button
          onClick={onPause}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-amber-600 hover:bg-amber-500 text-white text-xs font-medium transition-colors"
        >
          <Pause className="h-3.5 w-3.5" />
          Pause
        </button>
      )}
      {isPaused && (
        <button
          onClick={onResume}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium transition-colors"
        >
          <Play className="h-3.5 w-3.5" />
          Resume
        </button>
      )}

      {/* Reset (only when not idle) */}
      {!isIdle && (
        <button
          onClick={onReset}
          className="flex items-center gap-1.5 px-3 py-1.5 rounded-md bg-slate-700 hover:bg-slate-600 text-slate-200 text-xs font-medium transition-colors"
        >
          <RotateCcw className="h-3.5 w-3.5" />
          Reset
        </button>
      )}

      {/* Speed selector */}
      <div className="flex items-center gap-1 ml-2">
        <Zap className="h-3.5 w-3.5 text-slate-400" />
        {SPEEDS.map((s) => (
          <button
            key={s}
            onClick={() => onSpeed(s)}
            className={cn(
              "px-2 py-1 rounded text-[10px] font-mono transition-colors",
              Math.round(status.speedMultiplier) === s
                ? "bg-blue-600 text-white"
                : "bg-slate-700 text-slate-400 hover:bg-slate-600 hover:text-white",
            )}
          >
            {s}x
          </button>
        ))}
      </div>

      {/* Phase + progress */}
      <div className="flex items-center gap-2 ml-2 text-xs">
        <span
          className={cn(
            "px-2 py-0.5 rounded-full font-medium",
            isRunning && "bg-emerald-500/20 text-emerald-400",
            isPaused && "bg-amber-500/20 text-amber-400",
            isIdle && "bg-slate-700 text-slate-400",
            !isIdle && !isRunning && !isPaused && "bg-slate-700 text-slate-400",
          )}
        >
          {status.phase}
        </span>
        {status.totalSubprocesses > 0 && (
          <span className="text-slate-500">
            {status.completedSubprocesses}/{status.totalSubprocesses}
          </span>
        )}
      </div>
    </div>
  )
}
