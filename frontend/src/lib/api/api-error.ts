import type { ProblemDetails } from "./problem-details";

/**
 * Every non-2xx response from the Querio API arrives here. Carrying the traceId means a
 * user-reported failure can be tied straight back to the API log line.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly errorCode: string;
  readonly traceId: string | undefined;
  readonly fieldErrors: Record<string, string[]> | undefined;
  /** From the Retry-After header, so a throttled caller can be told when, not just "later". */
  readonly retryAfterSeconds: number | undefined;

  constructor(
    status: number,
    problem: ProblemDetails | undefined,
    fallbackMessage: string,
    retryAfterSeconds?: number,
  ) {
    super(problem?.detail ?? problem?.title ?? fallbackMessage);

    this.name = "ApiError";
    this.status = status;
    this.errorCode = problem?.errorCode ?? "client.unknown_error";
    this.traceId = problem?.traceId;
    this.fieldErrors = problem?.errors;
    this.retryAfterSeconds = retryAfterSeconds;
  }

  /** The caller can fix this by changing the request. */
  get isClientError(): boolean {
    return this.status >= 400 && this.status < 500;
  }

  /** Retrying may succeed; the failure is ours, not the caller's. */
  get isServerError(): boolean {
    return this.status >= 500;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  get isValidationError(): boolean {
    return this.fieldErrors !== undefined;
  }

  /** Network failure, DNS, CORS — no HTTP response ever arrived. */
  static networkFailure(cause: unknown): ApiError {
    const error = new ApiError(0, undefined, "Could not reach Querio.");

    error.cause = cause;

    return error;
  }

  /**
   * The grep-able part of a W3C traceparent, short enough to read down a phone line.
   * Undefined when there is nothing useful to quote, so callers can omit the sentence
   * entirely rather than print "reference: undefined".
   */
  get reference(): string | undefined {
    const traceId = this.traceId?.split("-")[1];

    return traceId && traceId.length > 0 ? traceId : undefined;
  }
}
