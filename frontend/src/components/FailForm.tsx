import { useEffect, useMemo, useState } from "react"
import { AlertTriangle, AlertOctagon, Check, AlertCircle } from "lucide-react"
import { api } from "@/lib/api"
import { useCatalog } from "@/hooks/useCatalog"
import { cn } from "@/lib/utils"

type FailureType = "subprocess" | "critical"
type SeverityPreset = "minor" | "moderate" | "major" | "custom"

const SEVERITIES: Record<Exclude<SeverityPreset, "custom">, { failAfterPercent: number; extraIterations: number; opportunityCostHours: number; label: string; hint: string }> = {
  minor:    { failAfterPercent: 0.5, extraIterations: 1, opportunityCostHours: 1,  label: "Minor",    hint: "1 retry, fails 50% through, 1h gap" },
  moderate: { failAfterPercent: 0.8, extraIterations: 2, opportunityCostHours: 4,  label: "Moderate", hint: "2 retries, fails 80% through, 4h gap" },
  major:    { failAfterPercent: 1.0, extraIterations: 3, opportunityCostHours: 12, label: "Major",    hint: "3 retries, fails 100% through, 12h gap" },
}

const CRITICAL_REASONS = [
  "Data quality breach — missing / stale / validation",
  "Configuration change — bad parameter deployed",
  "Human error — missed manual step / forgot aftercare load",
  "Tech error — server failure / network interruption / access issue",
] as const

export function FailForm() {
  const { catalog, loading } = useCatalog()
  const [failureType, setFailureType] = useState<FailureType>("subprocess")
  const [location, setLocation] = useState<string>("")

  // Subprocess-level state
  const [subprocess, setSubprocess] = useState<string>("")
  const [severity, setSeverity] = useState<SeverityPreset>("moderate")
  const [customFailAt, setCustomFailAt] = useState<number>(80)
  const [customRetries, setCustomRetries] = useState<number>(2)
  const [customGap, setCustomGap] = useState<number>(4)
  const [errorText, setErrorText] = useState<string>("")

  // Critical state
  const [discoveryWd, setDiscoveryWd] = useState<number>(17)
  const [fixHours, setFixHours] = useState<number>(24)
  const [reason, setReason] = useState<string>(CRITICAL_REASONS[0])

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

  const effective = severity === "custom"
    ? { failAfterPercent: customFailAt / 100, extraIterations: customRetries, opportunityCostHours: customGap }
    : SEVERITIES[severity]

  const submit = async () => {
    if (!location) return
    if (failureType === "subprocess" && !subprocess) return
    setSubmitting(true)
    setToast(null)
    try {
      const payload = failureType === "subprocess"
        ? {
            action: "fail" as const,
            location,
            subprocess,
            failAfterPercent: effective.failAfterPercent,
            extraIterations: effective.extraIterations,
            opportunityCostHours: effective.opportunityCostHours,
            errorMessage: errorText || undefined,
          }
        : {
            action: "critical" as const,
            location,
            discoveryWd,
            opportunityCostHours: fixHours,
            reason,
          }
      const res = await api.simulator.inject(payload)
      setToast({
        kind: "ok",
        text: failureType === "subprocess"
          ? `Queued (#${res.id}): ${location}/${subprocess} — ${severity}`
          : `Queued (#${res.id}): ${location} critical WD ${discoveryWd}, fix ${fixHours}h`,
      })
    } catch (e) {
      setToast({ kind: "err", text: e instanceof Error ? e.message : "Inject failed" })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="border border-slate-800 rounded-md bg-slate-900/50 p-4 space-y-4 lg:col-span-2">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {failureType === "subprocess"
            ? <AlertTriangle className="h-4 w-4 text-orange-400" />
            : <AlertOctagon className="h-4 w-4 text-red-400" />}
          <h3 className="text-xs font-semibold text-slate-300 uppercase tracking-wide">Inject a Failure</h3>
        </div>

        {/* Subtype toggle */}
        <div className="flex items-center gap-1 bg-slate-800 rounded p-0.5">
          <button
            onClick={() => setFailureType("subprocess")}
            className={cn(
              "px-2.5 py-1 rounded text-[10px] font-medium transition-colors",
              failureType === "subprocess" ? "bg-orange-600 text-white" : "text-slate-400 hover:text-white",
            )}
          >
            Subprocess-level
          </button>
          <button
            onClick={() => setFailureType("critical")}
            className={cn(
              "px-2.5 py-1 rounded text-[10px] font-medium transition-colors",
              failureType === "critical" ? "bg-red-600 text-white" : "text-slate-400 hover:text-white",
            )}
          >
            Critical
          </button>
        </div>
      </div>

      {loading ? (
        <p className="text-xs text-slate-500 italic">Loading catalog…</p>
      ) : (
        <>
          {/* Location (shared between both types) */}
          <label className="flex flex-col gap-1.5">
            <span className="text-[11px] font-medium text-slate-400">Location</span>
            <select
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-orange-500 focus:outline-none"
            >
              {catalog.map((c) => (
                <option key={c.location} value={c.location}>{c.location}</option>
              ))}
            </select>
          </label>

          {failureType === "subprocess" ? (
            <>
              <label className="flex flex-col gap-1.5">
                <span className="text-[11px] font-medium text-slate-400">Subprocess</span>
                <select
                  value={subprocess}
                  onChange={(e) => setSubprocess(e.target.value)}
                  className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-orange-500 focus:outline-none"
                >
                  {subsForLoc.map((s) => (
                    <option key={s.subprocess} value={s.subprocess}>
                      {s.subprocess}{s.isQuarterly ? " (Q)" : ""}
                    </option>
                  ))}
                </select>
              </label>

              {/* Severity presets */}
              <div className="flex flex-col gap-1.5">
                <span className="text-[11px] font-medium text-slate-400">Severity</span>
                <div className="grid grid-cols-4 gap-2">
                  {(["minor", "moderate", "major"] as const).map((s) => (
                    <button
                      key={s}
                      onClick={() => setSeverity(s)}
                      title={SEVERITIES[s].hint}
                      className={cn(
                        "px-2 py-2 rounded text-xs font-medium transition-colors text-left",
                        severity === s
                          ? "bg-orange-600 text-white"
                          : "bg-slate-800 border border-slate-700 text-slate-300 hover:bg-slate-700",
                      )}
                    >
                      <div>{SEVERITIES[s].label}</div>
                      <div className="text-[9px] opacity-80 font-normal">{SEVERITIES[s].hint}</div>
                    </button>
                  ))}
                  <button
                    onClick={() => setSeverity("custom")}
                    className={cn(
                      "px-2 py-2 rounded text-xs font-medium transition-colors text-left",
                      severity === "custom"
                        ? "bg-orange-600 text-white"
                        : "bg-slate-800 border border-slate-700 text-slate-300 hover:bg-slate-700",
                    )}
                  >
                    <div>Custom</div>
                    <div className="text-[9px] opacity-80 font-normal">set each knob</div>
                  </button>
                </div>
              </div>

              {severity === "custom" && (
                <div className="grid grid-cols-3 gap-3 p-3 bg-slate-800/50 rounded border border-slate-700">
                  <label className="flex flex-col gap-1">
                    <span className="text-[10px] text-slate-400">Fail at % through</span>
                    <input type="number" min={10} max={100} value={customFailAt}
                      onChange={(e) => setCustomFailAt(parseFloat(e.target.value) || 80)}
                      className="bg-slate-800 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200" />
                  </label>
                  <label className="flex flex-col gap-1">
                    <span className="text-[10px] text-slate-400">Retries before success</span>
                    <input type="number" min={1} max={10} value={customRetries}
                      onChange={(e) => setCustomRetries(parseInt(e.target.value) || 1)}
                      className="bg-slate-800 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200" />
                  </label>
                  <label className="flex flex-col gap-1">
                    <span className="text-[10px] text-slate-400">Gap between retries (h)</span>
                    <input type="number" min={0.25} step={0.25} value={customGap}
                      onChange={(e) => setCustomGap(parseFloat(e.target.value) || 1)}
                      className="bg-slate-800 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200" />
                  </label>
                </div>
              )}

              <label className="flex flex-col gap-1.5">
                <span className="text-[11px] font-medium text-slate-400">
                  Error message <span className="text-slate-600">(optional — auto-generates if blank)</span>
                </span>
                <input
                  type="text"
                  value={errorText}
                  onChange={(e) => setErrorText(e.target.value)}
                  placeholder="e.g. Workflow finished with errors — timeout on curve node EUR-6M"
                  className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-orange-500 focus:outline-none"
                />
              </label>

              <div className="text-[11px] text-slate-500 italic leading-relaxed">
                During the run: {location || "(location)"}/{subprocess || "(subprocess)"} will run for {Math.round(effective.failAfterPercent * 100)}% of its normal duration then fail, with {effective.extraIterations} retry iteration{effective.extraIterations === 1 ? "" : "s"} (each with a {effective.opportunityCostHours}h business-hour gap) before the canonical success.
              </div>
            </>
          ) : (
            <>
              {/* CRITICAL branch */}
              <div className="grid grid-cols-2 gap-4">
                <div className="flex flex-col gap-1.5">
                  <div className="flex justify-between items-baseline">
                    <span className="text-[11px] font-medium text-slate-400">Discovered at</span>
                    <span className="text-sm font-mono text-red-400">WD {discoveryWd}</span>
                  </div>
                  <input
                    type="range"
                    min={1}
                    max={22}
                    step={1}
                    value={discoveryWd}
                    onChange={(e) => setDiscoveryWd(parseInt(e.target.value))}
                    className="accent-red-500"
                  />
                  <div className="flex justify-between text-[10px] text-slate-500 font-mono">
                    <span>WD 1</span>
                    <span>WD 11</span>
                    <span>WD 22</span>
                  </div>
                </div>
                <label className="flex flex-col gap-1.5">
                  <span className="text-[11px] font-medium text-slate-400">Fix duration (business hours)</span>
                  <input
                    type="number"
                    min={1}
                    max={168}
                    value={fixHours}
                    onChange={(e) => setFixHours(parseFloat(e.target.value) || 24)}
                    className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-red-500 focus:outline-none"
                  />
                </label>
              </div>

              <label className="flex flex-col gap-1.5">
                <span className="text-[11px] font-medium text-slate-400">Reason (category — for log/audit)</span>
                <select
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  className="bg-slate-800 border border-slate-700 rounded px-2 py-1.5 text-sm text-slate-200 focus:border-red-500 focus:outline-none"
                >
                  {CRITICAL_REASONS.map((r) => (
                    <option key={r} value={r}>{r}</option>
                  ))}
                </select>
              </label>

              <div className="text-[11px] text-slate-500 italic leading-relaxed">
                Every subprocess at {location || "(location)"} will complete iter 1 normally, then {fixHours}h business-hour gap, then rerun iter 2 (canonical) after the fix. Iter 1 rolls into failed runtime; iter 2 becomes the efficient canonical. {`discoveryWd=${discoveryWd}`} is captured as audit metadata but doesn't gate per-sub application.
              </div>
            </>
          )}

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
              disabled={submitting || !location || (failureType === "subprocess" && !subprocess)}
              className={cn(
                "px-3 py-1.5 rounded-md text-white text-xs font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed",
                failureType === "subprocess"
                  ? "bg-orange-600 hover:bg-orange-500"
                  : "bg-red-600 hover:bg-red-500",
              )}
            >
              {submitting ? "Queuing…" : "Add to queue"}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
