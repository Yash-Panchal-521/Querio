"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { QuerioMark } from "@/components/brand/querio-mark";
import { useSession } from "@/lib/auth/session-context";

/**
 * Client-side gate. Firebase holds the session in the browser, so the server cannot know
 * whether a visitor is signed in — meaning this is a routing convenience, not a security
 * control. Nothing here is trusted: the API authorises every request on its own.
 */
export function RequireSession({ children }: { children: React.ReactNode }) {
  const { session } = useSession();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (session.status === "signed-out") {
      // Carries where they were headed, so signing in resumes there instead of dumping them
      // on a default page.
      router.replace(`/sign-in?next=${encodeURIComponent(pathname)}`);
    }
  }, [session.status, router, pathname]);

  if (session.status === "ready") {
    return <>{children}</>;
  }

  // A connection problem is reported by a toast and retried automatically, so there is
  // nothing to ask of the person here — just say what is happening.
  const message =
    session.status === "failed"
      ? "Reconnecting…"
      : session.status === "provisioning"
        ? "Setting up your account…"
        : "Loading Querio…";

  return (
    <div className="flex flex-1 items-center justify-center px-6 py-12">
      <div className="flex flex-col items-center gap-3">
        {/* The mark rather than a spinner: a moment of brand beats a generic wheel, and it
            reassures that the right app is loading. */}
        <QuerioMark className="size-8 animate-pulse motion-reduce:animate-none" />
        <p className="text-muted-foreground text-sm" role="status">
          {message}
        </p>
      </div>
    </div>
  );
}
