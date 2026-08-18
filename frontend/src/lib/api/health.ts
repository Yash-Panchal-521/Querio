import { apiFetch } from "./client";

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export interface HealthCheckEntry {
  name: string;
  status: HealthStatus;
  durationMs: number;
  description: string | null;
}

export interface HealthReport {
  status: HealthStatus;
  totalDurationMs: number;
  checks: HealthCheckEntry[];
}

/**
 * Readiness rather than liveness: it reports on the API's dependencies, which is what a
 * status view actually needs to show.
 */
export function getReadiness(): Promise<HealthReport> {
  return apiFetch<HealthReport>("/health/ready", { cache: "no-store" });
}
