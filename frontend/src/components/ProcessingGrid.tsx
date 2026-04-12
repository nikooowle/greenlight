import { useState, useMemo } from "react"
import { cn } from "@/lib/utils"
import type { Location, Subprocess, MatrixCell } from "@/types"

const STATUS_COLORS: Record<string, { bg: string; text: string; ring: string }> = {
  "Completed":    { bg: "bg-emerald-500", text: "text-white",    ring: "ring-emerald-400" },
  "Failed":       { bg: "bg-red-500",     text: "text-white",    ring: "ring-red-400" },
  "Running":      { bg: "bg-blue-500",    text: "text-white",    ring: "ring-blue-400" },
  "Not Started":  { bg: "bg-slate-600",   text: "text-slate-300",ring: "ring-slate-500" },
  "Not in Scope": { bg: "bg-slate-800",   text: "text-slate-600",ring: "ring-slate-700" },
  "Stopped":      { bg: "bg-orange-500",  text: "text-white",    ring: "ring-orange-400" },
  "For Rerun":    { bg: "bg-amber-500",   text: "text-white",    ring: "ring-amber-400" },
}

const PHASE_LABELS: Record<string, string> = {
  DataIngestion: "Data Ingestion",
  Processing: "Processing",
  Reporting: "Reporting",
}

const PHASES = ["DataIngestion", "Processing", "Reporting"] as const

interface ProcessingGridProps {
  locations: Location[]
  subprocesses: Subprocess[]
  matrix: MatrixCell[]
}

export function ProcessingGrid({ locations, subprocesses, matrix }: ProcessingGridProps) {
  const [activePhase, setActivePhase] = useState<string>("Processing")

  // Build a lookup: `${locationCode}::${subprocessName}` -> MatrixCell
  const cellMap = useMemo(() => {
    const map = new Map<string, MatrixCell>()
    for (const cell of matrix) {
      map.set(`${cell.location}::${cell.subprocess}`, cell)
    }
    return map
  }, [matrix])

  // Subprocesses filtered by active phase
  const phaseSubs = useMemo(
    () => subprocesses.filter((s) => s.phase === activePhase),
    [subprocesses, activePhase],
  )

  // Phase counts for tab badges
  const phaseCounts = useMemo(() => {
    const counts: Record<string, { total: number; completed: number }> = {}
    for (const phase of PHASES) {
      const cells = matrix.filter((c) => c.phase === phase)
      counts[phase] = {
        total: cells.length,
        completed: cells.filter((c) => c.status === "Completed").length,
      }
    }
    return counts
  }, [matrix])

  // In-scope locations only
  const activeLocations = useMemo(
    () => locations.filter((l) => l.inScope),
    [locations],
  )

  if (locations.length === 0) {
    return (
      <div className="rounded-lg border border-slate-700 bg-slate-800/50 p-12 text-center">
        <p className="text-slate-400">Waiting for simulation data...</p>
        <p className="text-xs text-slate-500 mt-1">Start the simulator or wait for it to initialize</p>
      </div>
    )
  }

  return (
    <div>
      {/* Phase tabs */}
      <div className="flex items-center gap-1 mb-4">
        {PHASES.map((phase) => {
          const count = phaseCounts[phase]
          const isActive = phase === activePhase
          return (
            <button
              key={phase}
              onClick={() => setActivePhase(phase)}
              className={cn(
                "px-4 py-2 rounded-t-lg text-sm font-medium transition-colors flex items-center gap-2",
                isActive
                  ? "bg-slate-800 text-white border border-b-0 border-slate-600"
                  : "text-slate-400 hover:text-slate-200 hover:bg-slate-800/50",
              )}
            >
              {PHASE_LABELS[phase]}
              {count && count.total > 0 && (
                <span
                  className={cn(
                    "text-[10px] px-1.5 py-0.5 rounded-full",
                    isActive ? "bg-slate-600 text-slate-200" : "bg-slate-700 text-slate-400",
                  )}
                >
                  {count.completed}/{count.total}
                </span>
              )}
            </button>
          )
        })}
      </div>

      {/* Grid table */}
      <div className="rounded-lg border border-slate-600 bg-slate-800/50 overflow-auto max-h-[calc(100vh-320px)]">
        <table className="w-full border-collapse text-xs">
          <thead className="sticky top-0 z-10">
            <tr className="bg-slate-800">
              <th className="sticky left-0 z-20 bg-slate-800 px-3 py-2 text-left text-slate-400 font-medium border-b border-r border-slate-600 min-w-[80px]">
                Location
              </th>
              {phaseSubs.map((sub) => (
                <th
                  key={sub.id}
                  className="px-1 py-2 border-b border-slate-600 font-medium text-slate-400 min-w-[40px]"
                >
                  <div className="writing-vertical-lr rotate-180 h-24 flex items-center justify-start overflow-hidden whitespace-nowrap"
                    style={{ writingMode: "vertical-lr" }}
                    title={sub.name}
                  >
                    {sub.name}
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {activeLocations.map((loc) => (
              <tr key={loc.id} className="hover:bg-slate-700/30 transition-colors">
                <td className="sticky left-0 z-10 bg-slate-800 px-3 py-1.5 font-mono text-slate-300 border-r border-slate-600 whitespace-nowrap">
                  {loc.code}
                </td>
                {phaseSubs.map((sub) => {
                  const cell = cellMap.get(`${loc.code}::${sub.name}`)
                  const status = cell?.status ?? "Not in Scope"
                  const colors = STATUS_COLORS[status] ?? STATUS_COLORS["Not in Scope"]
                  return (
                    <td key={sub.id} className="px-1 py-1.5 text-center">
                      <div className="group relative inline-block">
                        <div
                          className={cn(
                            "w-7 h-7 rounded-sm flex items-center justify-center text-[9px] font-bold transition-all",
                            colors.bg,
                            colors.text,
                            status === "Running" && "animate-pulse",
                          )}
                        >
                          {status === "Completed" && "\u2713"}
                          {status === "Failed" && "\u2717"}
                          {status === "Running" && "\u25B6"}
                          {status === "Stopped" && "\u25A0"}
                          {status === "For Rerun" && "\u21BB"}
                        </div>
                        {/* Tooltip */}
                        <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 hidden group-hover:block z-30 pointer-events-none">
                          <div className="bg-slate-900 border border-slate-600 rounded px-3 py-2 text-left whitespace-nowrap shadow-lg">
                            <p className="text-slate-200 font-medium">{sub.name}</p>
                            <p className="text-slate-400">{loc.code} &mdash; {status}</p>
                            {cell?.completedSteps != null && (
                              <p className="text-slate-500">
                                Steps: {cell.completedSteps}/{cell.totalRequiredSteps}
                              </p>
                            )}
                            {cell?.elapsedMinutes != null && cell.elapsedMinutes > 0 && (
                              <p className="text-slate-500">
                                {cell.elapsedMinutes < 60
                                  ? `${Math.round(cell.elapsedMinutes)}m`
                                  : `${(cell.elapsedMinutes / 60).toFixed(1)}h`}
                              </p>
                            )}
                          </div>
                        </div>
                      </div>
                    </td>
                  )
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
