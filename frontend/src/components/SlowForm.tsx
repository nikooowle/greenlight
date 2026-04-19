import { useEffect, useMemo, useState } from "react"
import { TurtleIcon, Check, AlertCircle } from "lucide-react"
import { api } from "@/lib/api"
import { useCatalog } from "@/hooks/useCatalog"
import { cn } from "@/lib/utils"

const PRESETS = [2, 5, 10]

export function SlowForm() {
  const { catalog, loading } = useCatalog()
  const [location, setLocation] = useState<string>("")
  const [subprocess, setSubprocess] = useState<string>("")
  const [multiplier, setMultiplier] = useState<number>(5)
  const [submitting, setSubmitting] = useState(false)
  const [toast, setToast] = useState<{ kind: "ok" | "err"; text: string } | null>(null)

  // Default to first location when catalog loads
  useEffect(() => {
    if (catalog.length > 0 && !location) setLocation(catalog[0].location)
  }, [catalog, location])

  // Reset subprocess when location changes
  const subsForLoc = useMemo(
    () => catalog.find((c) => c.location === location)?.subprocesses ?? [],
    [catalog, location],
  )
  useEffect(() => {
    if (subsForLoc.length > 0 && !subsForLoc.some((s) => s.subprocess === subprocess)) {
      setSubprocess(subsForLoc[0].subprocess)
    }
  }, [subsForLoc, subprocess])

  // Preview: compute normal-total vs slowed-total
  const selectedSub = subsForLoc.find((s) => s.subprocess === subprocess)
  const normalTotalMin = selectedSub?.steps.reduce((a, s) => a + s.avgMinutes, 0) ?? 0
  const slowedTotalMin = normalTotalMin * multiplier
  const fmtH = (min: number) => (min / 60).toFixed(2)

  const submit = async () => {
    if (!location || !subprocess) return
    setSubmitting(true)
    setToast(null)
    try {
      const res = await api.simulator.inject({
        action: "slow",
        location,
        subprocess,
        slowMultiplier: multiplier,
      })
      setToast({ kind: "ok", text: `Queued (#${res.id}): ${location} / ${subprocess} × ${multiplier}` })
    } catch (e) {
      setToast({ kind: "err", text: e instanceof Error ? e.message : "Inject failed" })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="border border-slate-800 rounded-md bg-slate-900/50 p-4 space-y-4">
      <div className="flex items-center gap-2">
        <TurtleIcon className="h-4 w-4 text-yellow-400" />
        <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Slow a Subprocess</h3>
      </div>

      {loading ? (
        <p className="text-xs text-slate-500 italic">Loading catalog…</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-4">
            <label className="flex flex-col gap-1.5">
              <span className="text-[11px] font-medium text-slate-400">Location</span>
              <select
                value={location}
                onChange={(e) => setLocation(e.target.value)}
                className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-yellow-500 focus:outline-none"
              >
                {catalog.map((c) => (
                  <option key={c.location} value={c.location}>
                    {c.location}
                  </option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1.5">
              <span className="text-[11px] font-medium text-slate-400">Subprocess</span>
              <select
                value={subprocess}
                onChange={(e) => setSubprocess(e.target.value)}
                className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-yellow-500 focus:outline-none"
              >
                {subsForLoc.map((s) => (
                  <option key={s.subprocess} value={s.subprocess}>
                    {s.subprocess}{s.isQuarterly ? " (Q)" : ""}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {/* Multiplier presets + custom */}
          <div className="flex flex-col gap-1.5">
            <span className="text-[11px] font-medium text-slate-400">How much slower</span>
            <div className="flex items-center gap-2">
              {PRESETS.map((p) => (
                <button
                  key={p}
                  onClick={() => setMultiplier(p)}
                  className={cn(
                    "px-3 py-1.5 rounded text-xs font-mono transition-colors",
                    multiplier === p
                      ? "bg-yellow-600 text-white"
                      : "bg-slate-800 border border-slate-700 text-slate-300 hover:bg-slate-700",
                  )}
                >
                  {p}×
                </button>
              ))}
              <div className="flex items-center gap-1">
                <span className="text-[11px] text-slate-500">custom:</span>
                <input
                  type="number"
                  step={0.5}
                  min={1.5}
                  max={20}
                  value={multiplier}
                  onChange={(e) => setMultiplier(parseFloat(e.target.value) || 1)}
                  className="w-16 bg-slate-800 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200 focus:border-yellow-500 focus:outline-none"
                />
                <span className="text-[11px] text-slate-500">×</span>
              </div>
            </div>
          </div>

          {/* Preview */}
          <div className="text-[11px] text-slate-500 italic leading-relaxed">
            {selectedSub ? (
              <>Normal total: <span className="font-mono text-slate-400">{fmtH(normalTotalMin)}h</span> ({selectedSub.steps.length} steps) → slowed: <span className="font-mono text-yellow-400">{fmtH(slowedTotalMin)}h</span>. Multiplier applies to every step in the subprocess.</>
            ) : "Pick a subprocess to see the preview."}
          </div>

          {/* Submit + toast */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-800">
            {toast && (
              <span className={cn("flex items-center gap-1.5 text-xs", toast.kind === "ok" ? "text-emerald-400" : "text-red-400")}>
                {toast.kind === "ok" ? <Check className="h-3.5 w-3.5" /> : <AlertCircle className="h-3.5 w-3.5" />}
                {toast.text}
              </span>
            )}
            <button
              onClick={submit}
              disabled={submitting || !location || !subprocess}
              className="px-3 py-1.5 rounded-md bg-yellow-600 hover:bg-yellow-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-medium transition-colors"
            >
              {submitting ? "Queuing…" : "Add to queue"}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
