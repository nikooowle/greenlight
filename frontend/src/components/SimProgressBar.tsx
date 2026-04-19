import { useEffect, useRef, useState } from "react"
import { Activity, Zap } from "lucide-react"
import { cn } from "@/lib/utils"
import type { SimulatorStatus } from "@/types"

interface SimProgressBarProps {
  status: SimulatorStatus | null
  variant?: "full" | "compact"
}

function formatDuration(ms: number): string {
  if (!Number.isFinite(ms) || ms < 0) return "—"
  const s = Math.round(ms / 1000)
  if (s < 60) return `${s}s`
  const m = Math.floor(s / 60)
  const rem = s % 60
  if (m < 60) return `${m}m ${rem}s`
  const h = Math.floor(m / 60)
  return `${h}h ${m % 60}m`
}

const SAMPLE_WINDOW_MS = 60_000 // 60s rolling window — long enough to span heavy individual subs

export function SimProgressBar({ status, variant = "full" }: SimProgressBarProps) {
  const samplesRef = useRef<{ time: number; progress: number }[]>([])
  const lastEtaRef = useRef<string | null>(null)
  const [, force] = useState(0)

  const progress = status && status.totalSubprocesses > 0
    ? (status.completedSubprocesses / status.totalSubprocesses) * 100
    : 0
  const running = !!status && status.isRunning && !status.isPaused

  // Push a sample every tick (not just when progress changes) — heavy subs may not tick
  // progress for 20–30s, which would otherwise leave us with only 1 sample and "computing…"
  useEffect(() => {
    const id = setInterval(() => {
      const now = Date.now()
      if (!running) {
        samplesRef.current = []
      } else {
        const buf = samplesRef.current.filter(s => now - s.time < SAMPLE_WINDOW_MS)
        buf.push({ time: now, progress })
        samplesRef.current = buf.slice(-30)
      }
      force(v => v + 1)
    }, 1000)
    return () => clearInterval(id)
  }, [running, progress])

  if (!status) return null

  const complete = status.phase === "Complete" || (!running && progress >= 100 && status.totalSubprocesses > 0)

  let etaText = "—"
  if (complete) {
    etaText = "done"
    lastEtaRef.current = null
  } else if (status.isPaused) {
    etaText = "paused"
  } else if (!running && progress === 0) {
    etaText = "idle"
    lastEtaRef.current = null
  } else if (running) {
    const samples = samplesRef.current
    if (samples.length < 2) {
      etaText = lastEtaRef.current ?? "computing…"
    } else {
      const first = samples[0]
      const last = samples[samples.length - 1]
      const dtMs = last.time - first.time
      const dPct = last.progress - first.progress
      if (dtMs < 500 || dPct <= 0) {
        // Progress hasn't advanced in this window — keep showing the last computed ETA
        etaText = lastEtaRef.current ?? "computing…"
      } else {
        const rate = dPct / dtMs
        const remainingMs = (100 - last.progress) / rate
        etaText = `≈ ${formatDuration(remainingMs)}`
        lastEtaRef.current = etaText
      }
    }
  }

  if (variant === "compact") {
    const isCachedReplay = running && status.phase?.includes("cached")
    const simLabel = isCachedReplay ? "Replaying" :
                     running ? "Running" :
                     status.isPaused ? "Paused" :
                     complete ? "Complete" : "Idle"
    const pillColor =
      isCachedReplay ? "bg-yellow-500/20 text-yellow-300 border-yellow-500/30" :
      running ? "bg-emerald-500/20 text-emerald-400 border-emerald-500/30" :
      status.isPaused ? "bg-amber-500/20 text-amber-400 border-amber-500/30" :
      complete ? "bg-emerald-500/20 text-emerald-400 border-emerald-500/30" :
      "bg-slate-800 text-slate-400 border-slate-700"
    const barColor =
      isCachedReplay ? "bg-yellow-400" :
      complete ? "bg-emerald-500" :
      running ? "bg-blue-500" :
      status.isPaused ? "bg-amber-500" :
      "bg-slate-600"
    const hasProgress = status.totalSubprocesses > 0
    const Icon = isCachedReplay ? Zap : Activity

    return (
      <div className={cn("flex items-center gap-2 px-3 py-1 rounded-full border text-xs font-medium", pillColor)}>
        <Icon className={cn("h-3.5 w-3.5", isCachedReplay && "fill-yellow-300")} />
        <span>Sim: {simLabel}{isCachedReplay ? " (cached)" : ""}</span>
        {hasProgress && (
          <>
            <div className="h-1.5 w-20 rounded-full bg-slate-800/60 overflow-hidden">
              <div
                className={cn("h-full transition-all duration-500", barColor)}
                style={{ width: `${Math.max(0, Math.min(100, progress))}%` }}
              />
            </div>
            <span className="font-mono text-[10px] opacity-80">{progress.toFixed(0)}%</span>
            {running && !isCachedReplay && (
              <span className="font-mono text-[10px] opacity-70">{etaText}</span>
            )}
          </>
        )}
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-baseline justify-between text-xs">
        <div className="flex items-center gap-3">
          <span className="font-semibold text-slate-300">
            {progress.toFixed(1)}% complete
          </span>
          <span className="text-slate-500 font-mono text-[11px]">
            {status.completedSubprocesses}/{status.totalSubprocesses} subprocesses
          </span>
        </div>
        <div className="text-slate-400">
          ETA: <span className="font-mono text-slate-200">{etaText}</span>
        </div>
      </div>
      <div className="h-2 rounded-full bg-slate-800 overflow-hidden">
        <div
          className={cn(
            "h-full transition-all duration-500",
            complete ? "bg-emerald-500" :
            running ? "bg-blue-500" :
            status.isPaused ? "bg-amber-500" :
            "bg-slate-700",
          )}
          style={{ width: `${Math.max(0, Math.min(100, progress))}%` }}
        />
      </div>
      {running && status.currentLocation && status.currentSubprocess && (
        <div className="text-[10px] text-slate-500 font-mono">
          current: {status.currentLocation} / {status.currentSubprocess}
        </div>
      )}
    </div>
  )
}
