"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { onIdTokenChanged, signOut, type User } from "firebase/auth";
import { setAuthTokenProvider, setUnauthorizedHandler } from "@/lib/api/client";
import { bootstrapProfile, type UserProfile } from "@/lib/api/me";
import { firebaseAuth } from "@/lib/firebase/config";
import { useToast } from "@/components/ui/toast";

/**
 * Signing in is not complete until the Querio profile exists, so provisioning is a state of
 * the session rather than something that happens quietly afterwards. Without that, the app
 * would briefly render as signed-in for an account the API does not yet know about, and
 * every early request would fail.
 */
export type Session =
  | { status: "loading" }
  | { status: "signed-out" }
  | { status: "provisioning"; user: User }
  | { status: "ready"; user: User; profile: UserProfile }
  | { status: "failed"; user: User };

interface SessionValue {
  session: Session;
  /** Re-reads the profile — after verifying an email, or joining an organization. */
  refresh: () => Promise<void>;
}

const SessionContext = createContext<SessionValue | null>(null);

/**
 * Escalating gaps rather than a tight loop: a server restarting locally is back within
 * seconds, and hammering it in the meantime achieves nothing.
 */
const RETRY_DELAYS_MS = [2000, 5000, 10000];

export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<Session>({ status: "loading" });
  const { showToast } = useToast();

  // Guards against an out-of-order provisioning response overwriting a newer sign-in — a
  // real possibility when someone signs out and back in quickly.
  const generation = useRef(0);

  const provision = useCallback(
    async (user: User) => {
      // Retries run as a loop rather than a self-scheduling callback: recursion through
      // useCallback is something neither the React Compiler nor a reader can follow, and the
      // generation check below already makes a stale pass harmless.
      const token = ++generation.current;

      setSession({ status: "provisioning", user });

      for (let index = 0; index <= RETRY_DELAYS_MS.length; index++) {
        try {
          const profile = await bootstrapProfile();

          if (token !== generation.current) {
            return;
          }

          setSession({ status: "ready", user, profile });

          return;
        } catch {
          if (token !== generation.current) {
            return;
          }

          setSession({ status: "failed", user });

          const delay = RETRY_DELAYS_MS[index];

          // Nothing here is the person's fault and none of it is theirs to fix, so the
          // message says what is happening rather than what broke, and offers no buttons.
          if (delay === undefined) {
            showToast(
              "We still can't connect. Please check your internet and refresh the page.",
              "error",
            );

            return;
          }

          if (index === 0) {
            showToast("Having trouble connecting. We'll keep trying.", "error");
          }

          await new Promise((resolve) => setTimeout(resolve, delay));
        }
      }
    },
    [showToast],
  );

  useEffect(() => {
    const auth = firebaseAuth();

    // Asked per request rather than cached, so Firebase's silent refresh is what keeps the
    // token current instead of us tracking expiry.
    setAuthTokenProvider(() => auth.currentUser?.getIdToken() ?? Promise.resolve(null));

    // A rejected token means the session is over — revoked, disabled, or expired beyond
    // refresh. Ending it here sends the person to sign in rather than leaving the app
    // failing every request with no explanation.
    setUnauthorizedHandler(() => {
      void signOut(auth);
    });

    // onIdTokenChanged rather than onAuthStateChanged: it also fires on token refresh, which
    // is when a freshly verified email becomes visible in the claims.
    const unsubscribe = onIdTokenChanged(auth, (user) => {
      if (user) {
        void provision(user);
      } else {
        generation.current += 1;
        setSession({ status: "signed-out" });
      }
    });

    return () => {
      unsubscribe();
      setAuthTokenProvider(null);
      setUnauthorizedHandler(null);

      // Invalidates any retry loop still sleeping, so it exits instead of writing state
      // into an unmounted tree.
      generation.current += 1;
    };
  }, [provision]);

  const refresh = useCallback(async () => {
    const user = firebaseAuth().currentUser;

    if (user) {
      await provision(user);
    }
  }, [provision]);

  const value = useMemo<SessionValue>(() => ({ session, refresh }), [session, refresh]);

  return <SessionContext value={value}>{children}</SessionContext>;
}

export function useSession(): SessionValue {
  const value = useContext(SessionContext);

  if (!value) {
    throw new Error("useSession must be used inside a SessionProvider.");
  }

  return value;
}
