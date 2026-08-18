import { apiFetch } from "./client";
import type { Organization, TenantRole } from "./me";

export interface IssuedInvitation {
  id: string;
  email: string;
  role: TenantRole;
  expiresAt: string;
  /** The only copy that will ever exist — the API stores a hash. */
  token: string;
}

export interface PendingInvitation {
  id: string;
  email: string;
  role: TenantRole;
  expiresAt: string;
  invitedAt: string;
}

export interface InvitationPreview {
  organizationName: string;
  email: string;
  role: TenantRole;
  expiresAt: string;
}

export function inviteMember(
  tenantId: string,
  email: string,
  role: TenantRole,
): Promise<IssuedInvitation> {
  return apiFetch<IssuedInvitation>(`/api/v1/tenants/${tenantId}/invitations`, {
    method: "POST",
    body: { email, role },
  });
}

export function listInvitations(tenantId: string): Promise<PendingInvitation[]> {
  return apiFetch<PendingInvitation[]>(`/api/v1/tenants/${tenantId}/invitations`, {
    cache: "no-store",
  });
}

export function revokeInvitation(tenantId: string, invitationId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/tenants/${tenantId}/invitations/${invitationId}`, {
    method: "DELETE",
  });
}

// Both of these POST a token in the body rather than putting it in a URL, because request
// paths end up in server logs.
export function previewInvitation(token: string): Promise<InvitationPreview> {
  return apiFetch<InvitationPreview>("/api/v1/invitations/preview", {
    method: "POST",
    body: { token },
  });
}

export function acceptInvitation(token: string): Promise<Organization> {
  return apiFetch<Organization>("/api/v1/invitations/accept", {
    method: "POST",
    body: { token },
  });
}

/**
 * Carries the token in the URL fragment, not the path.
 *
 * A fragment is never sent to a server: it stays out of access logs, out of Referer headers
 * on any outbound link, and out of anything a proxy or CDN records. For a value that grants
 * access to an organization, that is worth the small cost of needing JavaScript to read it.
 */
export function buildInvitationLink(origin: string, token: string): string {
  return `${origin}/invite#${encodeURIComponent(token)}`;
}
