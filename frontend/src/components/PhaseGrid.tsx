import { cn } from "@/lib/utils"

interface PhaseProps {
  title: string
  description: string
  status: "active" | "upcoming" | "not-ready"
  columnCount: number
}

function PhaseCard({ title, description, status, columnCount }: PhaseProps) {
  return (
    <div
      className={cn(
        "rounded-lg border p-6 flex-1 min-w-[280px]",
        status === "active" && "border-emerald-500/30 bg-emerald-500/5",
        status === "upcoming" && "border-slate-600 bg-slate-800/50",
        status === "not-ready" && "border-slate-700 bg-slate-800/30 opacity-60"
      )}
    >
      <div className="flex items-center justify-between mb-3">
        <h3
          className={cn(
            "text-sm font-semibold uppercase tracking-wider",
            status === "active" && "text-emerald-400",
            status === "upcoming" && "text-slate-300",
            status === "not-ready" && "text-slate-500"
          )}
        >
          {title}
        </h3>
        <span
          className={cn(
            "text-[10px] px-2 py-0.5 rounded-full font-medium",
            status === "active" && "bg-emerald-500/20 text-emerald-400",
            status === "upcoming" && "bg-slate-600/50 text-slate-400",
            status === "not-ready" && "bg-slate-700/50 text-slate-500"
          )}
        >
          {status === "active"
            ? "Priority"
            : status === "upcoming"
              ? "Upcoming"
              : "Pending Integration"}
        </span>
      </div>
      <p className="text-sm text-slate-400 mb-4">{description}</p>
      <div className="flex items-center justify-between text-xs text-slate-500">
        <span>{columnCount} subprocesses</span>
        <span>
          {status === "active"
            ? "Building now"
            : status === "upcoming"
              ? "After processing"
              : "Waiting on DAS / Power BI"}
        </span>
      </div>
      {/* Placeholder grid */}
      <div className="mt-4 grid grid-cols-4 gap-1.5">
        {Array.from({ length: 8 }).map((_, i) => (
          <div
            key={i}
            className={cn(
              "h-6 rounded",
              status === "active" && "bg-slate-700/50",
              status !== "active" && "bg-slate-800/50"
            )}
          />
        ))}
      </div>
    </div>
  )
}

export function PhaseGrid() {
  return (
    <div>
      <div className="flex items-center gap-3 mb-6">
        <h2 className="text-lg font-semibold text-white">3-Phase Process Matrix</h2>
        <span className="text-xs text-slate-500">Location x Subprocess</span>
      </div>
      <div className="flex gap-4 flex-wrap">
        <PhaseCard
          title="Data Ingestion"
          description="Data availability, DAS processing, DQ gates, loaded to QRM"
          status="upcoming"
          columnCount={7}
        />
        <PhaseCard
          title="Processing"
          description="MCP subprocess completion in QRM — risk calculations"
          status="active"
          columnCount={15}
        />
        <PhaseCard
          title="Reporting"
          description="Results published to Power BI per subprocess"
          status="not-ready"
          columnCount={10}
        />
      </div>
    </div>
  )
}
