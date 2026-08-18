/**
 * RFC 9457 payload as the Querio API emits it.
 *
 * `errorCode` and `traceId` are stamped on every failure the API produces, including ones
 * that never reach an endpoint (routing 404, method mismatch), so the UI can branch on a
 * stable code instead of matching on prose.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errorCode?: string;
  traceId?: string;
  /** Present on validation failures: field name to the messages for that field. */
  errors?: Record<string, string[]>;
}

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== "object" || value === null) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  return (
    typeof candidate.status === "number" ||
    typeof candidate.title === "string" ||
    typeof candidate.errorCode === "string"
  );
}
