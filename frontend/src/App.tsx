import { useState } from "react"
import { Sidebar } from "@/components/Sidebar"
import { MetricCards } from "@/components/MetricCards"
import { ProcessingGrid } from "@/components/ProcessingGrid"
import { StatusLegend } from "@/components/StatusLegend"
import { SimulatorPage } from "@/components/SimulatorPage"
import { SimProgressBar } from "@/components/SimProgressBar"
import { useDashboard } from "@/hooks/useDashboard"
import { useSimulator } from "@/hooks/useSimulator"
import { Calendar, Clock } from "lucide-react"

const MONTH_NAMES = [
  "", "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
]

function formatRunLabel(_reportMonth: string, year: number, month: number) {
  return `${MONTH_NAMES[month]} ${year} MCP`
}

function formatEom(eomDate: string | null) {
  if (!eomDate) return "--"
  return new Date(eomDate).toLocaleDateString("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  })
}

function DashboardPage() {
  const { run, locations, subprocesses, matrix, error: dashError } = useDashboard()
  const { status: simStatus } = useSimulator()

  const runLabel = run
    ? formatRunLabel(run.reportMonth, run.year, run.month)
    : "No active run"

  return (
    <main className="flex-1 overflow-auto">
      <header className="border-b border-slate-800 px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-white">ALM Greenlight</h1>
            <p className="text-sm text-slate-400">MCP Production Monitoring Dashboard</p>
          </div>
          <div className="flex items-center gap-6 text-sm text-slate-400">
            <div className="flex items-center gap-2">
              <Calendar className="h-4 w-4" />
              <span>EOM: {formatEom(run?.eomDate ?? null)}</span>
            </div>
            <div className="flex items-center gap-2">
              <Clock className="h-4 w-4" />
              <span>Run: {run?.status ?? "--"}</span>
            </div>
            <SimProgressBar status={simStatus} variant="compact" />
            <div className="px-3 py-1 rounded-full bg-slate-800 border border-slate-700 text-xs font-medium">
              {runLabel}
            </div>
          </div>
        </div>
      </header>

      <div className="p-6 space-y-6">
        {dashError && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
            API Error: {dashError}
          </div>
        )}
        <MetricCards matrix={matrix} locations={locations} />
        <ProcessingGrid
          locations={locations}
          subprocesses={subprocesses}
          matrix={matrix}
        />
        <StatusLegend />
      </div>
    </main>
  )
}

function PlaceholderPage({ title }: { title: string }) {
  return (
    <main className="flex-1 overflow-auto">
      <header className="border-b border-slate-800 px-6 py-4">
        <h1 className="text-xl font-bold text-white">{title}</h1>
        <p className="text-sm text-slate-400">Not yet built.</p>
      </header>
    </main>
  )
}

function App() {
  const [activePath, setActivePath] = useState("/")

  return (
    <div className="flex h-screen bg-slate-950 text-white">
      <Sidebar activePath={activePath} onNavigate={setActivePath} />
      {activePath === "/" && <DashboardPage />}
      {activePath === "/simulator" && <SimulatorPage />}
      {activePath !== "/" && activePath !== "/simulator" && (
        <PlaceholderPage title={activePath.slice(1).replace(/-/g, " ").replace(/\b\w/g, c => c.toUpperCase())} />
      )}
    </div>
  )
}

export default App
