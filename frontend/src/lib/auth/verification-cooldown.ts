/**
 * Firebase throttles repeated verification emails to the same address, and answers a second
 * send within a short window with TOO_MANY_ATTEMPTS_TRY_LATER. Since one is sent
 * automatically at sign-up, an eager "Resend" click lands squarely in that window and looks
 * like a bug to the person pressing it.
 *
 * Recording when a link was last sent lets the interface wait instead of spending the
 * attempt. Stored per account so switching users does not inherit someone else's countdown.
 */
const COOLDOWN_SECONDS = 60;

function key(uid: string): string {
  return `querio.verification-sent.${uid}`;
}

export function recordVerificationSent(uid: string): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(key(uid), Date.now().toString());
}

/**
 * Zero when sending is allowed — including when nothing was ever recorded, since someone
 * who signed up on another device should not be blocked by a countdown that never started.
 */
export function secondsUntilResendAllowed(uid: string): number {
  if (typeof window === "undefined") {
    return 0;
  }

  const raw = window.localStorage.getItem(key(uid));

  if (!raw) {
    return 0;
  }

  const sentAt = Number(raw);

  if (!Number.isFinite(sentAt)) {
    return 0;
  }

  const elapsed = Math.floor((Date.now() - sentAt) / 1000);

  return Math.max(0, COOLDOWN_SECONDS - elapsed);
}
