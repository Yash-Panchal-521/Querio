"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useOrganizations } from "@/lib/auth/use-organizations";
import { useSession } from "@/lib/auth/session-context";

/**
 * Decides where a signed-in visitor actually lands.
 *
 * No organizations means the app has nothing to show, so send them to create one rather than
 * to an empty shell. Otherwise the default organization — always the same one, since Querio
 * does not remember the last used.
 */
export function OrgsEntry() {
  const { session } = useSession();
  const { defaultOrganization } = useOrganizations();
  const router = useRouter();

  const ready = session.status === "ready";
  const target = defaultOrganization ? `/orgs/${defaultOrganization.id}` : "/orgs/new";

  useEffect(() => {
    if (ready) {
      router.replace(target);
    }
  }, [ready, router, target]);

  return (
    <div className="flex flex-1 items-center justify-center px-6 py-12">
      <p className="text-muted-foreground text-sm" role="status">
        Loading…
      </p>
    </div>
  );
}
