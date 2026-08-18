"use client";

import { useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useSession } from "./session-context";

export const DEFAULT_SIGNED_IN_PATH = "/orgs";

/**
 * Only same-origin, non-protocol-relative paths are honoured.
 *
 * `next` arrives from the query string, so echoing it into a redirect unchecked turns every
 * sign-in link into an open redirect — the classic phishing primitive, since the victim
 * really did land on our domain first. "//evil.example" is the case a naive startsWith("/")
 * check misses.
 */
export function safeRedirectTarget(next: string | null): string {
  if (!next || !next.startsWith("/") || next.startsWith("//")) {
    return DEFAULT_SIGNED_IN_PATH;
  }

  return next;
}

/** Sends a signed-in visitor onward, once their profile actually exists. */
export function useAuthRedirect(): void {
  const { session } = useSession();
  const router = useRouter();
  const searchParams = useSearchParams();

  const target = safeRedirectTarget(searchParams.get("next"));

  useEffect(() => {
    // Deliberately waits for "ready" rather than "provisioning": landing in the app before
    // the profile exists means every request there fails with user.not_provisioned.
    if (session.status === "ready") {
      router.replace(target);
    }
  }, [session.status, router, target]);
}
