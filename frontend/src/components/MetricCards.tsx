import {
  CheckCircle2,
  Loader2,
  XCircle,
  Clock,
  AlertTriangle,
  MapPin,
} from "lucide-react"
import { cn } from "@/lib/utils"

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

export function MetricCards() {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
      <MetricCard
        icon={CheckCircle2}
        label="Completed"
        value="--"
        subtext="Subprocesses done"
        color="text-emerald-400"
        bgColor="bg-emerald-500/10"
      />
      <MetricCard
        icon={Loader2}
        label="Running"
        value="--"
        subtext="In progress"
        color="text-blue-400"
        bgColor="bg-blue-500/10"
      />
      <MetricCard
        icon={XCircle}
        label="Failed"
        value="--"
        subtext="Need attention"
        color="text-red-400"
        bgColor="bg-red-500/10"
      />
      <MetricCard
        icon={Clock}
        label="Waiting"
        value="--"
        subtext="Not started"
        color="text-slate-400"
        bgColor="bg-slate-500/10"
      />
      <MetricCard
        icon={AlertTriangle}
        label="Issues"
        value="--"
        subtext="Open incidents"
        color="text-amber-400"
        bgColor="bg-amber-500/10"
      />
      <MetricCard
        icon={MapPin}
        label="Locations"
        value="15"
        subtext="ING entities"
        color="text-purple-400"
        bgColor="bg-purple-500/10"
      />
    </div>
  )
}
