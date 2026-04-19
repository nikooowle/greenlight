import { useEffect, useState } from "react"
import { api } from "@/lib/api"
import type { CatalogEntry } from "@/types"

/**
 * Catalog of (location, subprocess, steps) available for injection. Fetched once on mount;
 * the underlying data is stable for the lifetime of the seeded DB.
 */
export function useCatalog() {
  const [catalog, setCatalog] = useState<CatalogEntry[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    api.simulator.catalog()
      .then(setCatalog)
      .finally(() => setLoading(false))
  }, [])

  return { catalog, loading }
}
