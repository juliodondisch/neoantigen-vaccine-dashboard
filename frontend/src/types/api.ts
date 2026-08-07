export interface ApiError {
  status: number;
  message: string;
  detail?: string;
}

export interface ToolStatus {
  toolName: string;
  isAvailable: boolean;
  version?: string;
  resolvedPath?: string;
  error?: string;
  usedBySteps: string[];
}

export interface DiskStatus {
  availableBytes: number;
  dataUsedBytes: number;
}

export interface TestRunResult {
  stepId: string;
  testName: string;
  outcome: "Passed" | "Failed" | "Skipped";
  message?: string;
  skipReason?: string;
  durationSeconds: number;
  assertions: string[];
}
