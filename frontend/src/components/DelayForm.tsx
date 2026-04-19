import { useEffect, useState } from "react"
import { Clock, Check, AlertCircle } from "lucide-react"
import { api } from "@/lib/api"
import { cn } from "@/lib/utils"
import type { Location } from "@/types"

interface DelayFormProps {
  onInjected?: () => void
}

const REASONS = [
  "Late upstream data delivery",
  "Holiday",
  "Team unavailable",
  "System maintenance window",
  "Dependency blocker",
] as const

export function DelayForm({ onInjected }: DelayFormProps) {
  const [locations, setLocations] = useState<Location[]>([])
  const [location, setLocation] = useState<string>("")
  const [workingDays, setWorkingDays] = useState<number>(1.0)
  const [reason, setReason] = useState<string>(REASONS[0])
  const [submitting, setSubmitting] = useState(false)
  const [toast, setToast] = useState<{ kind: "ok" | "err"; text: string } | null>(null)

  useEffect(() => {
    api.getLocations().then((locs) => {
      const inScope = locs.filter((l) => l.inScope).sort((a, b) => a.code.localeCompare(b.code))
      setLocations(inScope)
      if (inScope.length > 0) setLocation(inScope[0].code)
    })
  }, [])

  const submit = async () => {
    if (!location) return
    setSubmitting(true)
    setToast(null)
    try {
      const res = await api.simulator.inject({
        action: "delay",
        location,
        workingDays,
        reason,
      })
      setToast({ kind: "ok", text: `Queued (#${res.id}): ${location} delayed by ${workingDays} WD` })
      onInjected?.()
      // Keep form values so operator can inject several similar events quickly
    } catch (e) {
      setToast({ kind: "err", text: e instanceof Error ? e.message : "Inject failed" })
    } finally {
      setSubmitting(false)
    }
  }

  const preview = `${location || "(location)"}'s clock advances by ${workingDays} WD before any work at that location starts. Good for modeling late data deliveries or planned stand-downs.`

  return (
    <div className="border border-slate-800 rounded-md bg-slate-900/50 p-4 space-y-4">
      <div className="flex items-center gap-2">
        <Clock className="h-4 w-4 text-cyan-400" />
        <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Schedule a Delay</h3>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Location */}
        <label className="flex flex-col gap-1.5">
          <span className="text-[11px] font-medium text-slate-400">Location</span>
          <select
            value={location}
            onChange={(e) => setLocation(e.target.value)}
            className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-cyan-500 focus:outline-none"
          >
            {locations.map((l) => (
              <option key={l.code} value={l.code}>
                {l.code} — {l.name}
              </option>
            ))}
          </select>
        </label>

        {/* Reason */}
        <label className="flex flex-col gap-1.5">
          <span className="text-[11px] font-medium text-slate-400">Reason (cosmetic)</span>
          <select
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-cyan-500 focus:outline-none"
          >
            {REASONS.map((r) => (
              <option key={r} value={r}>{r}</option>
            ))}
          </select>
        </label>
      </div>

      {/* Working days slider */}
      <div className="flex flex-col gap-1.5">
        <div className="flex justify-between items-baseline">
          <span className="text-[11px] font-medium text-slate-400">Delay by</span>
          <span className="text-sm font-mono text-cyan-400">{workingDays.toFixed(1)} WD</span>
        </div>
        <input
          type="range"
          min={0.5}
          max={3}
          step={0.5}
          value={workingDays}
          onChange={(e) => setWorkingDays(parseFloat(e.target.value))}
          className="accent-cyan-500"
        />
        <div className="flex justify-between text-[10px] text-slate-500 font-mono">
          <span>0.5</span>
          <span>1.0</span>
          <span>1.5</span>
          <span>2.0</span>
          <span>2.5</span>
          <span>3.0</span>
        </div>
      </div>

      {/* Preview */}
      <div className="text-[11px] text-slate-500 italic leading-relaxed">
        {preview}
      </div>

      {/* Submit + toast */}
      <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-800">
        {toast && (
          <span
            className={cn(
              "flex items-center gap-1.5 text-xs",
              toast.kind === "ok" ? "text-emerald-400" : "text-red-400",
            )}
          >
            {toast.kind === "ok" ? <Check className="h-3.5 w-3.5" /> : <AlertCircle className="h-3.5 w-3.5" />}
            {toast.text}
          </span>
        )}
        <button
          onClick={submit}
          disabled={submitting || !location}
          className="px-3 py-1.5 rounded-md bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-xs font-medium transition-colors"
        >
          {submitting ? "Queuing…" : "Add to queue"}
        </button>
      </div>
    </div>
  )
}
