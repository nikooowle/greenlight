import { Sidebar } from "@/components/Sidebar"
import { MetricCards } from "@/components/MetricCards"
import { PhaseGrid } from "@/components/PhaseGrid"
import { Calendar, Clock } from "lucide-react"

function App() {
  return (
    <div className="flex h-screen bg-slate-950 text-white">
      <Sidebar />
      <main className="flex-1 overflow-auto">
        {/* Top header */}
        <header className="border-b border-slate-800 px-6 py-4">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-xl font-bold text-white">
                ALM Greenlight
              </h1>
              <p className="text-sm text-slate-400">
                MCP Production Monitoring Dashboard
              </p>
            </div>
            <div className="flex items-center gap-6 text-sm text-slate-400">
              <div className="flex items-center gap-2">
                <Calendar className="h-4 w-4" />
                <span>EOM: --</span>
              </div>
              <div className="flex items-center gap-2">
                <Clock className="h-4 w-4" />
                <span>Workday: --</span>
              </div>
              <div className="px-3 py-1 rounded-full bg-slate-800 border border-slate-700 text-xs">
                No active run
              </div>
            </div>
          </div>
        </header>

        {/* Dashboard content */}
        <div className="p-6 space-y-6">
          <MetricCards />
          <PhaseGrid />
        </div>
      </main>
    </div>
  )
}

export default App
