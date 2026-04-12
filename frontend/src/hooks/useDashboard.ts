import { useState, useEffect, useCallback, useRef } from "react"
import { api } from "@/lib/api"
import type { McpRun, Location, Subprocess, MatrixCell } from "@/types"

export function useDashboard() {
  const [run, setRun] = useState<McpRun | null>(null)
  const [locations, setLocations] = useState<Location[]>([])
  const [subprocesses, setSubprocesses] = useState<Subprocess[]>([])
  const [matrix, setMatrix] = useState<MatrixCell[]>([])
  const [error, setError] = useState<string | null>(null)
  const lastMonth = useRef<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      const currentRun = await api.getCurrentRun()
      setRun(currentRun)

      // Only re-fetch static data when run changes
      if (currentRun.reportMonth !== lastMonth.current) {
        lastMonth.current = currentRun.reportMonth
        const [locs, subs] = await Promise.all([
          api.getLocations(),
          api.getSubprocesses(),
        ])
        setLocations(locs)
        setSubprocesses(subs)
      }

      const cells = await api.getMatrix(currentRun.reportMonth)
      setMatrix(cells)
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unknown error")
    }
  }, [])

  useEffect(() => {
    refresh()
    const id = setInterval(refresh, 2000)
    return () => clearInterval(id)
  }, [refresh])

  return { run, locations, subprocesses, matrix, error, refresh }
}
