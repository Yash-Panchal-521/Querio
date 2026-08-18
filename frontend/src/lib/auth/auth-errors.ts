import { FirebaseError } from "firebase/app";

/**
 * Firebase error codes are not user-facing text. Left unmapped they surface as
 * "Firebase: Error (auth/invalid-credential)", which tells someone nothing about what to do
 * next.
 */
const MESSAGES: Record<string, string> = {
  "auth/email-already-in-use": "An account with this email already exists.",
  "auth/invalid-email": "That does not look like a valid email address.",
  "auth/weak-password": "Choose a password of at least 8 characters.",
  "auth/missing-password": "Enter your password.",
  // Firebase deliberately collapses wrong-password and no-such-user into one code so the
  // form cannot be used to discover who has an account. Keep that property in the wording.
  "auth/invalid-credential": "That email and password do not match.",
  "auth/user-disabled": "This account has been disabled. Contact your administrator.",
  // Also returned for a second verification email to the same address within about a
  // minute, which is why the resend button waits rather than spending the attempt.
  "auth/too-many-requests":
    "Too many attempts for this address. Wait a few minutes before trying again.",
  "auth/network-request-failed":
    "Could not reach the authentication service. Check your connection.",
  "auth/popup-blocked": "Your browser blocked the sign-in window. Allow popups and try again.",
  "auth/account-exists-with-different-credential":
    "This email is already registered with a different sign-in method.",
  "auth/unauthorized-domain": "This domain is not authorised for sign-in in the Firebase console.",
  // Reached when a sign-in provider is switched off in the console — the first thing a new
  // Firebase project hits, and baffling without this wording.
  "auth/operation-not-allowed":
    "This sign-in method is not enabled for the project. Turn it on under Authentication → Sign-in method.",
};

/** Dismissing the Google window is a decision, not a failure, so it gets no error banner. */
export const CANCELLED_CODES = new Set([
  "auth/popup-closed-by-user",
  "auth/cancelled-popup-request",
  "auth/user-cancelled",
]);

export class AuthCancelledError extends Error {
  constructor() {
    super("Sign-in was cancelled.");
    this.name = "AuthCancelledError";
  }
}

export function toAuthMessage(error: unknown): string {
  if (error instanceof FirebaseError) {
    return MESSAGES[error.code] ?? "Something went wrong signing you in. Try again.";
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Something went wrong. Try again.";
}

export function isCancelled(error: unknown): boolean {
  return (
    error instanceof AuthCancelledError ||
    (error instanceof FirebaseError && CANCELLED_CODES.has(error.code))
  );
}
