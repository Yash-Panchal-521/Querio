"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useOrganizations } from "@/lib/auth/use-organizations";
import { useSession } from "@/lib/auth/session-context";

/**
 * Guards a tenant-scoped page against an organization the caller is not in.
 *
 * This is routing, not security — the API returns 404 for another organization regardless.
 * What it buys is the case that actually happens: someone is removed while the tab is open,
 * or follows a stale link, and would otherwise sit staring at a broken page.
 */
export function OrganizationGate({ children }: { children: React.ReactNode }) {
  const { session } = useSession();
  const { active, defaultOrganization } = useOrganizations();
  const params = useParams<{ tenantId?: string }>();
  const router = useRouter();

  const ready = session.status === "ready";
  const missing = ready && !active;

  useEffect(() => {
    if (!missing) {
      return;
    }

    // Somewhere they can actually be: another organization, or the create step.
    router.replace(defaultOrganization ? `/orgs/${defaultOrganization.id}` : "/orgs/new");
  }, [missing, router, defaultOrganization]);

  if (!ready) {
    return null;
  }

  if (missing) {
    return (
      <div className="flex flex-1 items-center justify-center px-6 py-12">
        <p className="text-muted-foreground text-sm" role="status">
          {params.tenantId ? "That organization is no longer available to you…" : "Loading…"}
        </p>
      </div>
    );
  }

  return <>{children}</>;
}
