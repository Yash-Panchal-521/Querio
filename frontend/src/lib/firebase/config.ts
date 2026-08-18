import { getApp, getApps, initializeApp, type FirebaseApp } from "firebase/app";
import { getAuth, type Auth } from "firebase/auth";

/**
 * Firebase web configuration is public by design: it ships in every client bundle and
 * identifies the project rather than authorising anything. Access is controlled by the
 * Authorized domains list in the Firebase console, so these are configuration rather than
 * secrets — but they still come from the environment so each deployment points at its own
 * project.
 */
function readConfig() {
  const apiKey = process.env.NEXT_PUBLIC_FIREBASE_API_KEY;
  const authDomain = process.env.NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN;
  const projectId = process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID;
  const appId = process.env.NEXT_PUBLIC_FIREBASE_APP_ID;

  if (!apiKey || !authDomain || !projectId) {
    throw new Error(
      "Firebase is not configured. Set NEXT_PUBLIC_FIREBASE_API_KEY, " +
        "NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN and NEXT_PUBLIC_FIREBASE_PROJECT_ID.",
    );
  }

  return { apiKey, authDomain, projectId, appId };
}

/**
 * Modules evaluate more than once — server and client, and repeatedly under Fast Refresh —
 * so initialising unconditionally throws "Firebase App named '[DEFAULT]' already exists".
 * Reusing the existing app is the documented guard.
 */
function firebaseApp(): FirebaseApp {
  return getApps().length > 0 ? getApp() : initializeApp(readConfig());
}

export function firebaseAuth(): Auth {
  return getAuth(firebaseApp());
}
