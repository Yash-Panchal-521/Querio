import { getApiBaseUrl } from "@/lib/env";
import { ApiError } from "./api-error";
import { isProblemDetails, type ProblemDetails } from "./problem-details";

type QueryValue = string | number | boolean | null | undefined;

export interface ApiRequestOptions extends Omit<RequestInit, "body"> {
  /** Serialized as JSON unless it is already a BodyInit. */
  body?: unknown;
  searchParams?: Record<string, QueryValue>;
  /** Skips the bearer token for the rare call that must not carry one. */
  anonymous?: boolean;
}

type TokenProvider = () => Promise<string | null>;

let tokenProvider: TokenProvider | null = null;

/**
 * Registered once by the auth provider rather than threaded through every call site.
 *
 * The provider is asked per request instead of being handed a token, because Firebase
 * refreshes silently on expiry — caching a token here would eventually send a stale one and
 * produce 401s that look random.
 */
export function setAuthTokenProvider(provider: TokenProvider | null): void {
  tokenProvider = provider;
}

let unauthorizedHandler: (() => void) | null = null;

/**
 * Called when the API rejects a token outright. Registered by the auth layer so this module
 * never has to know Firebase exists.
 */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

/** Seconds, from either form the header allows. */
function readRetryAfterSeconds(response: Response): number | undefined {
  const header = response.headers.get("Retry-After");

  if (!header) {
    return undefined;
  }

  const seconds = Number(header);

  if (Number.isFinite(seconds)) {
    return Math.max(0, Math.ceil(seconds));
  }

  // The header may also be an HTTP date rather than a delta.
  const until = Date.parse(header);

  return Number.isNaN(until) ? undefined : Math.max(0, Math.ceil((until - Date.now()) / 1000));
}

function buildUrl(path: string, searchParams: Record<string, QueryValue> | undefined): string {
  const url = new URL(path.startsWith("/") ? path : `/${path}`, `${getApiBaseUrl()}/`);

  for (const [key, value] of Object.entries(searchParams ?? {})) {
    if (value !== undefined && value !== null) {
      url.searchParams.set(key, String(value));
    }
  }

  return url.toString();
}

function isBodyInit(value: unknown): value is BodyInit {
  return (
    typeof value === "string" ||
    value instanceof FormData ||
    value instanceof Blob ||
    value instanceof URLSearchParams ||
    value instanceof ArrayBuffer
  );
}

async function readProblemDetails(response: Response): Promise<ProblemDetails | undefined> {
  // A crashed proxy or a gateway timeout page will not be JSON. Never let parsing the
  // error body throw over the top of the error we are already reporting.
  try {
    const payload: unknown = await response.json();

    return isProblemDetails(payload) ? payload : undefined;
  } catch {
    return undefined;
  }
}

/** The body, plus the status for the rare caller that needs to tell 200 from 201. */
export interface ApiResult<T> {
  data: T;
  status: number;
}

/**
 * Single entry point for talking to the Querio API. Failures always surface as ApiError,
 * so callers never have to remember to check `response.ok`.
 */
export async function apiFetch<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  return (await apiFetchResult<T>(path, options)).data;
}

/**
 * As `apiFetch`, but keeps the status code.
 *
 * Almost nothing needs this — a success is a success. Uploading a document does: the API
 * answers 201 for a new one and 200 for a file this organization already has, and the
 * interface has to say which rather than appear to do nothing.
 */
export async function apiFetchResult<T>(
  path: string,
  options: ApiRequestOptions = {},
): Promise<ApiResult<T>> {
  const { body, searchParams, headers, anonymous, ...rest } = options;

  const requestHeaders = new Headers(headers);
  requestHeaders.set("Accept", "application/json, application/problem+json");

  if (!anonymous && !requestHeaders.has("Authorization") && tokenProvider) {
    const token = await tokenProvider();

    if (token) {
      requestHeaders.set("Authorization", `Bearer ${token}`);
    }
  }

  let requestBody: BodyInit | undefined;

  if (body !== undefined) {
    if (isBodyInit(body)) {
      requestBody = body;
    } else {
      requestBody = JSON.stringify(body);
      requestHeaders.set("Content-Type", "application/json");
    }
  }

  let response: Response;

  try {
    response = await fetch(buildUrl(path, searchParams), {
      ...rest,
      headers: requestHeaders,
      ...(requestBody !== undefined ? { body: requestBody } : {}),
    });
  } catch (cause) {
    throw ApiError.networkFailure(cause);
  }

  if (!response.ok) {
    const error = new ApiError(
      response.status,
      await readProblemDetails(response),
      // Never a status number: this becomes user-facing text whenever the response carried
      // no ProblemDetails — a crashed proxy, a gateway timeout page — and "failed with
      // status 502" tells nobody anything they can act on.
      "Something went wrong. Try again.",
      readRetryAfterSeconds(response),
    );

    if (error.isUnauthorized) {
      // The token was rejected outright: expired beyond refresh, or the account disabled.
      // Left alone the app would sit there failing every request with no explanation.
      unauthorizedHandler?.();
    }

    throw error;
  }

  // 204, or any success the API deliberately returns without a body.
  if (response.status === 204 || response.headers.get("Content-Length") === "0") {
    return { data: undefined as T, status: response.status };
  }

  return { data: (await response.json()) as T, status: response.status };
}
