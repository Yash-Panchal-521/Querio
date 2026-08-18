import { FirebaseError } from "firebase/app";
import {
  createUserWithEmailAndPassword,
  GoogleAuthProvider,
  sendEmailVerification,
  sendPasswordResetEmail,
  signInWithEmailAndPassword,
  signInWithPopup,
  signOut,
  updateProfile,
} from "firebase/auth";
import { firebaseAuth } from "@/lib/firebase/config";
import { AuthCancelledError, CANCELLED_CODES } from "./auth-errors";
import { recordVerificationSent } from "./verification-cooldown";

export async function signUpWithPassword(
  email: string,
  password: string,
  displayName: string,
): Promise<void> {
  const auth = firebaseAuth();
  const credential = await createUserWithEmailAndPassword(auth, email.trim(), password);

  const trimmedName = displayName.trim();

  if (trimmedName.length > 0) {
    await updateProfile(credential.user, { displayName: trimmedName });
    // The token was minted before the profile update, so it still carries no name. Refreshing
    // means the very first bootstrap stores the display name rather than a null we would only
    // correct on the next sign-in.
    await credential.user.getIdToken(true);
  }

  // Address ownership is unproven until this is followed, and creating an organization is
  // gated on it — so send it as part of signing up rather than waiting to be asked.
  await sendEmailVerification(credential.user);

  // Recorded so the account page knows a link is already in flight and does not offer a
  // Resend button that Firebase will refuse.
  recordVerificationSent(credential.user.uid);
}

export async function signInWithPassword(email: string, password: string): Promise<void> {
  await signInWithEmailAndPassword(firebaseAuth(), email.trim(), password);
}

export async function signInWithGoogle(): Promise<void> {
  const provider = new GoogleAuthProvider();

  // Always show the chooser. Silently reusing the last Google account is a nasty surprise on
  // a shared machine, and worse when someone is redeeming an invitation sent to their other
  // address.
  provider.setCustomParameters({ prompt: "select_account" });

  try {
    await signInWithPopup(firebaseAuth(), provider);
  } catch (error) {
    if (error instanceof FirebaseError && CANCELLED_CODES.has(error.code)) {
      throw new AuthCancelledError();
    }

    throw error;
  }
}

export async function signOutOfQuerio(): Promise<void> {
  await signOut(firebaseAuth());
}

/**
 * Resolves identically whether or not the address is registered.
 *
 * Firebase reports auth/user-not-found here, and surfacing it would turn this form into a
 * way to discover who has a Querio account. Swallowing it is the whole point.
 */
export async function requestPasswordReset(email: string): Promise<void> {
  try {
    await sendPasswordResetEmail(firebaseAuth(), email.trim());
  } catch (error) {
    if (error instanceof FirebaseError && error.code === "auth/user-not-found") {
      return;
    }

    throw error;
  }
}

export async function resendVerificationEmail(): Promise<void> {
  const user = firebaseAuth().currentUser;

  if (!user) {
    throw new Error("You need to be signed in to resend a verification email.");
  }

  await sendEmailVerification(user);

  recordVerificationSent(user.uid);
}

/**
 * Firebase caches emailVerified on the client, so it stays false after someone follows the
 * link in another tab until the user is reloaded and a fresh token minted.
 */
export async function refreshVerificationState(): Promise<boolean> {
  const user = firebaseAuth().currentUser;

  if (!user) {
    return false;
  }

  await user.reload();
  await user.getIdToken(true);

  return firebaseAuth().currentUser?.emailVerified ?? false;
}
