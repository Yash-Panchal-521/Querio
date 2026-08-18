"use client";

import { useParams } from "next/navigation";
import type { Organization } from "@/lib/api/me";
import { useSession } from "./session-context";

/**
 * Reads the active organization from the URL rather than holding it in state.
 *
 * Querio does not remember which organization someone last used, so the address bar is the
 * only place that choice lives — which makes switching ordinary navigation rather than a
 * state change that could leave one organization's data on screen under another's name.
 */
export function useOrganizations(): {
  organizations: Organization[];
  active: Organization | null;
  /** Where to send someone who has not chosen: the same one every time, never "most recent". */
  defaultOrganization: Organization | null;
} {
  const { session } = useSession();
  const params = useParams<{ tenantId?: string }>();

  const organizations = session.status === "ready" ? session.profile.organizations : [];
  const active = organizations.find((organization) => organization.id === params.tenantId) ?? null;

  return {
    organizations,
    active,
    defaultOrganization: organizations[0] ?? null,
  };
}
