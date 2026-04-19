import { useState } from "react"
import {
  LayoutDashboard,
  AlertTriangle,
  Clock,
  ShieldCheck,
  BarChart3,
  Bell,
  Users,
  SlidersHorizontal,
  ChevronLeft,
  ChevronRight,
  Activity,
} from "lucide-react"
import { cn } from "@/lib/utils"

const navItems = [
  { icon: LayoutDashboard, label: "Dashboard", path: "/" },
  { icon: SlidersHorizontal, label: "Simulator", path: "/simulator" },
  { icon: AlertTriangle, label: "Issues", path: "/issues" },
  { icon: Clock, label: "SLA Monitor", path: "/sla" },
  { icon: ShieldCheck, label: "Data Quality", path: "/data-quality" },
  { icon: BarChart3, label: "Analytics", path: "/analytics" },
  { icon: Bell, label: "Notifications", path: "/notifications" },
  { icon: Users, label: "Management", path: "/management" },
]

interface SidebarProps {
  activePath: string
  onNavigate: (path: string) => void
}

export function Sidebar({ activePath, onNavigate }: SidebarProps) {
  const [collapsed, setCollapsed] = useState(false)

  return (
    <aside
      className={cn(
        "h-screen bg-slate-900 text-white flex flex-col border-r border-slate-700 transition-all duration-300",
        collapsed ? "w-16" : "w-56"
      )}
    >
      {/* Logo */}
      <div className="flex items-center gap-3 px-4 py-5 border-b border-slate-700">
        <Activity className="h-7 w-7 text-emerald-400 shrink-0" />
        {!collapsed && (
          <div>
            <h1 className="text-base font-semibold leading-tight">ALM Greenlight</h1>
            <p className="text-[11px] text-slate-400">MCP Monitoring</p>
          </div>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-4 space-y-1 px-2">
        {navItems.map((item) => (
          <button
            key={item.path}
            onClick={() => onNavigate(item.path)}
            className={cn(
              "w-full flex items-center gap-3 px-3 py-2.5 rounded-md text-sm transition-colors",
              activePath === item.path
                ? "bg-slate-700/60 text-emerald-400"
                : "text-slate-300 hover:bg-slate-800 hover:text-white"
            )}
          >
            <item.icon className="h-5 w-5 shrink-0" />
            {!collapsed && <span>{item.label}</span>}
          </button>
        ))}
      </nav>

      {/* Collapse toggle */}
      <button
        onClick={() => setCollapsed(!collapsed)}
        className="flex items-center justify-center py-3 border-t border-slate-700 text-slate-400 hover:text-white transition-colors"
      >
        {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
      </button>
    </aside>
  )
}
