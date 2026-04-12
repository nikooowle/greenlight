import { useCallback } from "react"
import { api } from "@/lib/api"
import { usePolling } from "./usePolling"

export function useSimulator() {
  const { data: status, error, refresh } = usePolling(
    () => api.simulator.status(),
    1000,
  )

  const start = useCallback(async () => { await api.simulator.start(); refresh() }, [refresh])
  const pause = useCallback(async () => { await api.simulator.pause(); refresh() }, [refresh])
  const resume = useCallback(async () => { await api.simulator.resume(); refresh() }, [refresh])
  const reset = useCallback(async () => { await api.simulator.reset(); refresh() }, [refresh])
  const setSpeed = useCallback(async (n: number) => { await api.simulator.speed(n); refresh() }, [refresh])

  return { status, error, start, pause, resume, reset, setSpeed }
}
