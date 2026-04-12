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

export interface SimulatorStatus {
  isRunning: boolean
  isPaused: boolean
  speedMultiplier: number
  phase: string
  completedSubprocesses: number
  totalSubprocesses: number
  currentSubprocess: string | null
  currentLocation: string | null
}
