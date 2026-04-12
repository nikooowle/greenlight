import type { McpRun, Location, Subprocess, MatrixCell, SimulatorStatus } from "@/types"

async function get<T>(url: string): Promise<T> {
  const res = await fetch(url)
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json()
}

async function post<T>(url: string): Promise<T> {
  const res = await fetch(url, { method: "POST" })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json()
}

export const api = {
  getCurrentRun: () => get<McpRun>("/api/mcp-runs/current"),
  getLocations: () => get<Location[]>("/api/locations"),
  getSubprocesses: () => get<Subprocess[]>("/api/subprocesses"),
  getMatrix: (month: string) => get<MatrixCell[]>(`/api/mcp-runs/${month}/matrix`),

  simulator: {
    status: () => get<SimulatorStatus>("/api/simulator/status"),
    start: () => post<SimulatorStatus>("/api/simulator/start"),
    pause: () => post<SimulatorStatus>("/api/simulator/pause"),
    resume: () => post<SimulatorStatus>("/api/simulator/resume"),
    speed: (n: number) => post<SimulatorStatus>(`/api/simulator/speed/${n}`),
    reset: () => post<SimulatorStatus>("/api/simulator/reset"),
  },
}
