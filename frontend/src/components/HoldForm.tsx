import { useEffect, useMemo, useState } from "react"
import { PauseOctagon, Check, AlertCircle } from "lucide-react"
import { api } from "@/lib/api"
import { useCatalog } from "@/hooks/useCatalog"
import { cn } from "@/lib/utils"

export function HoldForm() {
  const { catalog, loading } = useCatalog()
  const [location, setLocation] = useState<string>("")
  const [subprocess, setSubprocess] = useState<string>("")
  const [submitting, setSubmitting] = useState(false)
  const [toast, setToast] = useState<{ kind: "ok" | "err"; text: string } | null>(null)

  useEffect(() => {
    if (catalog.length > 0 && !location) setLocation(catalog[0].location)
  }, [catalog, location])

  const subsForLoc = useMemo(
    () => catalog.find((c) => c.location === location)?.subprocesses ?? [],
    [catalog, location],
  )
  useEffect(() => {
    if (subsForLoc.length > 0 && !subsForLoc.some((s) => s.subprocess === subprocess)) {
      setSubprocess(subsForLoc[0].subprocess)
    }
  }, [subsForLoc, subprocess])

  const submit = async () => {
    if (!location || !subprocess) return
    setSubmitting(true)
    setToast(null)
    try {
      const res = await api.simulator.inject({
        action: "hold",
        location,
        subprocess,
      })
      setToast({ kind: "ok", text: `Queued (#${res.id}): ${location} / ${subprocess} will hold` })
    } catch (e) {
      setToast({ kind: "err", text: e instanceof Error ? e.message : "Inject failed" })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="border border-slate-800 rounded-md bg-slate-900/50 p-4 space-y-4">
      <div className="flex items-center gap-2">
        <PauseOctagon className="h-4 w-4 text-violet-400" />
        <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Hold a Subprocess</h3>
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
                className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-violet-500 focus:outline-none"
              >
                {catalog.map((c) => (
                  <option key={c.location} value={c.location}>{c.location}</option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1.5">
              <span className="text-[11px] font-medium text-slate-400">Subprocess</span>
              <select
                value={subprocess}
                onChange={(e) => setSubprocess(e.target.value)}
                className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-violet-500 focus:outline-none"
              >
                {subsForLoc.map((s) => (
                  <option key={s.subprocess} value={s.subprocess}>
                    {s.subprocess}{s.isQuarterly ? " (Q)" : ""}
                  </option>
                ))}
              </select>
            </label>
          </div>

          {/* Preview */}
          <div className="text-[11px] text-slate-500 italic leading-relaxed">
            When the sim reaches <span className="font-mono text-slate-400">{location || "(location)"} / {subprocess || "(subprocess)"}</span>, it will <span className="text-violet-400">pause before the subprocess starts</span> and wait for an operator Release click in the Scripted Events panel above. Useful for demo pacing — pause to explain something, then resume.
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
              className="px-3 py-1.5 rounded-md bg-violet-600 hover:bg-violet-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-medium transition-colors"
            >
              {submitting ? "Queuing…" : "Add to queue"}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
