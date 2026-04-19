import type { McpRun, Location, Subprocess, MatrixCell, SimulatorStatus, SimMode, ScriptedEvent, CatalogEntry } from "@/types"

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

async function del<T>(url: string): Promise<T> {
  const res = await fetch(url, { method: "DELETE" })
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
    mode: (m: SimMode) => post<{ message: string }>(`/api/simulator/mode/${m}`),
    catalog: () => get<CatalogEntry[]>("/api/simulator/catalog"),
    queue: () => get<ScriptedEvent[]>("/api/simulator/queue"),
    queueDelete: (id: number) => del<{ message: string }>(`/api/simulator/queue/${id}`),
    queueRelease: (id: number) => post<{ message: string }>(`/api/simulator/queue/${id}/release`),
    inject: async (body: InjectPayload) => {
      const res = await fetch("/api/simulator/inject", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      })
      if (!res.ok) {
        const errBody = await res.json().catch(() => ({ error: `${res.status}` }))
        throw new Error(errBody.error ?? `${res.status} ${res.statusText}`)
      }
      return res.json() as Promise<{ id: number; message: string }>
    },
  },
}

export interface InjectPayload {
  action: "slow" | "fail" | "critical" | "delay" | "hold"
  location: string
  subprocess?: string
  slowMultiplier?: number
  failAfterPercent?: number
  extraIterations?: number
  opportunityCostHours?: number
  workingDays?: number
  discoveryWd?: number
  reason?: string
  errorMessage?: string
}
