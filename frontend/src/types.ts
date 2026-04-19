export interface McpRun {
  id: number
  reportMonth: string
  year: number
  month: number
  status: string
  startDate: string | null
  endDate: string | null
  eomDate: string | null
}

export interface Location {
  id: number
  code: string
  name: string
  region: string | null
  inScope: boolean
}

export interface Subprocess {
  id: number
  name: string
  phase: "DataIngestion" | "Processing" | "Reporting"
  displayOrder: number
  description: string | null
}

export interface MatrixCell {
  location: string
  subprocess: string
  phase: string
  status: "Completed" | "Failed" | "Running" | "Not Started" | "Not in Scope" | "Stopped" | "For Rerun"
  startedAt: string | null
  completedAt: string | null
  elapsedMinutes: number | null
  completedSteps: number
  totalRequiredSteps: number
}

export type SimMode = "clean" | "baseline" | "stressed"

export type ScriptedEventAction = "slow" | "fail" | "critical" | "delay" | "hold"

export interface CatalogStep {
  step: string
  avgMinutes: number
}

export interface CatalogSubprocess {
  subprocess: string
  phase: string
  isQuarterly: boolean
  steps: CatalogStep[]
  sampleErrors: string[]
}

export interface CatalogEntry {
  location: string
  subprocesses: CatalogSubprocess[]
}

export interface ScriptedEvent {
  id: number
  action: ScriptedEventAction
  location: string
  subprocess: string | null
  step: string | null
  slowMultiplier: number | null
  failAfterPercent: number | null
  extraIterations: number | null
  opportunityCostHours: number | null
  workingDays: number | null
  discoveryWd: number | null
  reason: string | null
  errorMessage: string | null
  isReleased: boolean
  status: "Pending" | "Firing" | "Done" | "Skipped"
}

export interface SimulatorStatus {
  isRunning: boolean
  isPaused: boolean
  speedMultiplier: number
  phase: string
  completedSubprocesses: number
  totalSubprocesses: number
  currentSubprocess: string | null
  currentLocation: string | null
  mode: SimMode
  targetMonth?: string
  isQuarterEnd?: boolean
}
