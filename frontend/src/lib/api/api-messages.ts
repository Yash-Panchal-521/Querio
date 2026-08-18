import { ApiError } from "./api-error";

/**
 * The API already returns a human sentence in `detail`, so this maps only the cases where
 * the interface can say something more useful than the server can — because it knows what
 * the person was trying to do and which button to point at.
 */
const BY_ERROR_CODE: Record<string, string> = {
  "user.email_not_verified":
    "Verify your email address first. Check your inbox, or resend the link from your account page.",
  "tenant.last_owner":
    "You are the only owner. Promote another owner before stepping down or leaving.",
  "user.not_provisioned": "Your account is still being set up. Try again in a moment.",
  "invitation.expired": "This invitation has expired. Ask for a new one.",
  "invitation.revoked": "This invitation was cancelled. Ask for a new one.",
  "invitation.already_accepted": "This invitation has already been used.",
  "invitation.already_pending":
    "An invitation to that address is already waiting. Revoke it first to send a new link.",
  "membership.already_exists": "That person is already in this organization.",
};

export function toApiMessage(
  error: unknown,
  fallback = "Something went wrong. Try again.",
): string {
  if (error instanceof ApiError) {
    return describe(error, fallback);
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallback;
}

function describe(error: ApiError, fallback: string): string {
  if (error.status === 0) {
    return "We can't reach Querio right now. Check your connection and try again.";
  }

  if (error.errorCode === "quota.rate_limited") {
    // "Told when to retry" — the Retry-After header is the only thing that can say when, so
    // a vague "later" is used only when the server did not send one.
    return error.retryAfterSeconds
      ? `Too many attempts. Try again in ${formatDuration(error.retryAfterSeconds)}.`
      : "Too many attempts. Wait a moment and try again.";
  }

  const known = BY_ERROR_CODE[error.errorCode];

  if (known) {
    return known;
  }

  // Server faults are not the person's fault and carry nothing they can act on, so they get
  // an apology and a reference rather than whatever the server said. Client errors keep the
  // API's own sentence, which is written for the person who made the request.
  if (error.isServerError || error.errorCode === "server.unexpected_error") {
    const reference = error.reference;

    return reference
      ? `Something went wrong on our end. If it keeps happening, quote reference ${reference}.`
      : "Something went wrong on our end. Try again shortly.";
  }

  return error.message || fallback;
}

function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `${seconds} second${seconds === 1 ? "" : "s"}`;
  }

  const minutes = Math.ceil(seconds / 60);

  return `${minutes} minute${minutes === 1 ? "" : "s"}`;
}

/** Field errors from a validation failure, ready to render beside the offending input. */
export function toFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError) || !error.fieldErrors) {
    return {};
  }

  return Object.fromEntries(
    Object.entries(error.fieldErrors)
      .map(([field, messages]) => [field.toLowerCase(), messages[0]])
      .filter((entry): entry is [string, string] => typeof entry[1] === "string"),
  );
}
