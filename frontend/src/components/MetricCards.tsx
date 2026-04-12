import {
  CheckCircle2,
  Loader2,
  XCircle,
  Clock,
  AlertTriangle,
  MapPin,
} from "lucide-react"
import { cn } from "@/lib/utils"
import type { MatrixCell, Location } from "@/types"
import { useMemo } from "react"

interface MetricCardProps {
  icon: React.ElementType
  label: string
  value: string
  subtext: string
  color: string
  bgColor: string
}

function MetricCard({ icon: Icon, label, value, subtext, color, bgColor }: MetricCardProps) {
  return (
    <div className="rounded-lg border border-slate-700 bg-slate-800/50 p-4">
      <div className="flex items-center gap-3 mb-2">
        <div className={cn("p-2 rounded-md", bgColor)}>
          <Icon className={cn("h-4 w-4", color)} />
        </div>
        <span className="text-xs text-slate-400 uppercase tracking-wide">{label}</span>
      </div>
      <p className={cn("text-2xl font-bold", color)}>{value}</p>
      <p className="text-xs text-slate-500 mt-1">{subtext}</p>
    </div>
  )
}

interface MetricCardsProps {
  matrix: MatrixCell[]
  locations: Location[]
}

export function MetricCards({ matrix, locations }: MetricCardsProps) {
  const counts = useMemo(() => {
    const scopedCells = matrix.filter((c) => c.status !== "Not in Scope")
    return {
      completed: scopedCells.filter((c) => c.status === "Completed").length,
      running: scopedCells.filter((c) => c.status === "Running").length,
      failed: scopedCells.filter((c) => c.status === "Failed" || c.status === "Stopped").length,
      waiting: scopedCells.filter((c) => c.status === "Not Started").length,
      issues: scopedCells.filter((c) => c.status === "Failed").length,
      locations: locations.filter((l) => l.inScope).length,
    }
  }, [matrix, locations])

  const hasData = matrix.length > 0

  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
      <MetricCard
        icon={CheckCircle2}
        label="Completed"
        value={hasData ? String(counts.completed) : "--"}
        subtext="Subprocesses done"
        color="text-emerald-400"
        bgColor="bg-emerald-500/10"
      />
      <MetricCard
        icon={Loader2}
        label="Running"
        value={hasData ? String(counts.running) : "--"}
        subtext="In progress"
        color="text-blue-400"
        bgColor="bg-blue-500/10"
      />
      <MetricCard
        icon={XCircle}
        label="Failed"
        value={hasData ? String(counts.failed) : "--"}
        subtext="Need attention"
        color="text-red-400"
        bgColor="bg-red-500/10"
      />
      <MetricCard
        icon={Clock}
        label="Waiting"
        value={hasData ? String(counts.waiting) : "--"}
        subtext="Not started"
        color="text-slate-400"
        bgColor="bg-slate-500/10"
      />
      <MetricCard
        icon={AlertTriangle}
        label="Issues"
        value={hasData ? String(counts.issues) : "--"}
        subtext="Open incidents"
        color="text-amber-400"
        bgColor="bg-amber-500/10"
      />
      <MetricCard
        icon={MapPin}
        label="Locations"
        value={hasData ? String(counts.locations) : "--"}
        subtext="ING entities"
        color="text-purple-400"
        bgColor="bg-purple-500/10"
      />
    </div>
  )
}
