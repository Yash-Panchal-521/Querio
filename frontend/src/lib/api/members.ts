import { apiFetch } from "./client";
import type { TenantRole } from "./me";

export interface Member {
  userId: string;
  email: string;
  displayName: string | null;
  role: TenantRole;
  joinedAt: string;
}

export function listMembers(tenantId: string): Promise<Member[]> {
  return apiFetch<Member[]>(`/api/v1/tenants/${tenantId}/members`, { cache: "no-store" });
}

export function changeMemberRole(
  tenantId: string,
  userId: string,
  role: TenantRole,
): Promise<void> {
  return apiFetch<void>(`/api/v1/tenants/${tenantId}/members/${userId}`, {
    method: "PATCH",
    body: { role },
  });
}

export function removeMember(tenantId: string, userId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/tenants/${tenantId}/members/${userId}`, { method: "DELETE" });
}

export function leaveOrganization(tenantId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/tenants/${tenantId}/members/me`, { method: "DELETE" });
}
