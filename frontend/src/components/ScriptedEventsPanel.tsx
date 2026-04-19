import { X, Play, Clock, TurtleIcon, AlertTriangle, AlertOctagon, PauseOctagon } from "lucide-react"
import { cn } from "@/lib/utils"
import type { ScriptedEvent, ScriptedEventAction } from "@/types"

interface ScriptedEventsPanelProps {
  events: ScriptedEvent[]
  onCancel: (id: number) => void
  onRelease: (id: number) => void
}

const ACTION_META: Record<ScriptedEventAction, { label: string; icon: typeof Clock; color: string }> = {
  slow:     { label: "Slow",     icon: TurtleIcon,    color: "text-yellow-400" },
  fail:     { label: "Fail",     icon: AlertTriangle, color: "text-orange-400" },
  critical: { label: "Critical", icon: AlertOctagon,  color: "text-red-400" },
  delay:    { label: "Delay",    icon: Clock,         color: "text-cyan-400" },
  hold:     { label: "Hold",     icon: PauseOctagon,  color: "text-violet-400" },
}

function formatTarget(e: ScriptedEvent): string {
  const parts = [e.location]
  if (e.subprocess) parts.push(e.subprocess)
  return parts.join(" / ")
}

function formatParams(e: ScriptedEvent): string {
  switch (e.action) {
    case "slow":     return e.slowMultiplier ? `${e.slowMultiplier}× slower` : ""
    case "fail":     return e.failAfterPercent != null ? `fail @ ${Math.round(e.failAfterPercent * 100)}%, ${e.extraIterations ?? 1} retries, ${e.opportunityCostHours ?? 4}h gap` : ""
    case "critical": return `discovered WD ${e.discoveryWd ?? "?"}, fix ${e.opportunityCostHours ?? 24}h${e.reason ? ` — ${e.reason}` : ""}`
    case "delay":    return e.workingDays != null ? `${e.workingDays} WD${e.reason ? ` — ${e.reason}` : ""}` : ""
    case "hold":     return e.isReleased ? "released" : "awaiting release"
    default:         return ""
  }
}

function statusBadge(status: ScriptedEvent["status"]) {
  const map = {
    Pending: "bg-slate-700 text-slate-300",
    Firing:  "bg-amber-500/20 text-amber-300 animate-pulse",
    Done:    "bg-slate-800 text-slate-500",
    Skipped: "bg-slate-800/50 text-slate-500 italic",
  } as const
  return (
    <span className={cn("px-2 py-0.5 rounded text-[10px] font-medium", map[status])}>
      {status.toLowerCase()}
    </span>
  )
}

function renderEventRow(e: ScriptedEvent, onCancel: (id: number) => void, onRelease: (id: number) => void, muted: boolean) {
  const meta = ACTION_META[e.action]
  const Icon = meta.icon
  return (
    <li key={e.id} className={cn("flex items-center gap-3 px-3 py-2 text-xs", muted && "opacity-60")}>
      <Icon className={cn("h-4 w-4 flex-shrink-0", meta.color)} />
      <span className="font-medium text-slate-200 w-16">{meta.label}</span>
      <span className="text-slate-300 min-w-0 flex-shrink-0 max-w-[280px] truncate">{formatTarget(e)}</span>
      <span className="text-slate-400 min-w-0 flex-1 truncate">{formatParams(e)}</span>
      {statusBadge(e.status)}
      {e.action === "hold" && e.status !== "Done" && e.status !== "Skipped" && !e.isReleased && (
        <button
          onClick={() => onRelease(e.id)}
          className="flex items-center gap-1 px-2 py-1 rounded bg-violet-600 hover:bg-violet-500 text-white text-[10px] font-medium transition-colors"
        >
          <Play className="h-3 w-3" />
          Release
        </button>
      )}
      <button
        onClick={() => onCancel(e.id)}
        className="p-1 rounded text-slate-500 hover:bg-slate-800 hover:text-slate-200 transition-colors"
        title={muted ? "Remove from history" : "Cancel event"}
      >
        <X className="h-3.5 w-3.5" />
      </button>
    </li>
  )
}

export function ScriptedEventsPanel({ events, onCancel, onRelease }: ScriptedEventsPanelProps) {
  const active = events.filter(e => e.status !== "Done" && e.status !== "Skipped")
  const history = events.filter(e => e.status === "Done" || e.status === "Skipped")

  return (
    <div className="border border-slate-800 rounded-md bg-slate-900/50">
      <div className="flex items-center justify-between px-3 py-2 border-b border-slate-800">
        <div className="flex items-center gap-2">
          <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Scripted Events</h3>
          <span className="px-2 py-0.5 rounded-full bg-slate-800 text-slate-400 text-[10px] font-mono">
            {active.length}
          </span>
        </div>
        <span className="text-[10px] text-slate-500">
          Pre-stage incidents. Fires during the next run if mode is Baseline or Stressed.
        </span>
      </div>

      {active.length === 0 ? (
        <div className="px-3 py-4 text-xs text-slate-500 text-center italic">
          No events queued. Use the forms below to inject Slow, Fail, Critical, Delay, or Hold scenarios.
        </div>
      ) : (
        <ul className="divide-y divide-slate-800/50">
          {active.map(e => renderEventRow(e, onCancel, onRelease, false))}
        </ul>
      )}

      {history.length > 0 && (
        <div className="border-t border-slate-800">
          <div className="px-3 py-1.5 text-[10px] text-slate-500 uppercase tracking-wide flex items-center justify-between">
            <span>History · {history.length}</span>
            <span className="italic normal-case">Done = fired during run · Skipped = mode/target never applied</span>
          </div>
          <ul className="divide-y divide-slate-800/40">
            {history.map(e => renderEventRow(e, onCancel, onRelease, true))}
          </ul>
        </div>
      )}
    </div>
  )
}
