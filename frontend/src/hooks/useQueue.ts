import { useCallback } from "react"
import { api } from "@/lib/api"
import { usePolling } from "./usePolling"

export function useQueue() {
  const { data: queue, error, refresh } = usePolling(
    () => api.simulator.queue(),
    2000,
  )

  const cancel = useCallback(async (id: number) => { await api.simulator.queueDelete(id); refresh() }, [refresh])
  const release = useCallback(async (id: number) => { await api.simulator.queueRelease(id); refresh() }, [refresh])

  return { queue: queue ?? [], error, refresh, cancel, release }
}
