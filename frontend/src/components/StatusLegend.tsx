import { cn } from "@/lib/utils"

const STATUSES = [
  { label: "Completed",    bg: "bg-emerald-500", icon: "\u2713" },
  { label: "Running",      bg: "bg-blue-500",    icon: "\u25B6" },
  { label: "Failed",       bg: "bg-red-500",     icon: "\u2717" },
  { label: "Stopped",      bg: "bg-orange-500",  icon: "\u25A0" },
  { label: "For Rerun",    bg: "bg-amber-500",   icon: "\u21BB" },
  { label: "Not Started",  bg: "bg-slate-600",   icon: "" },
  { label: "Not in Scope", bg: "bg-slate-800 border border-slate-600", icon: "" },
]

export function StatusLegend() {
  return (
    <div className="flex items-center gap-4 flex-wrap text-xs text-slate-400">
      <span className="font-medium text-slate-300">Status:</span>
      {STATUSES.map((s) => (
        <div key={s.label} className="flex items-center gap-1.5">
          <div
            className={cn(
              "w-4 h-4 rounded-sm flex items-center justify-center text-[8px] font-bold text-white",
              s.bg,
            )}
          >
            {s.icon}
          </div>
          <span>{s.label}</span>
        </div>
      ))}
    </div>
  )
}
