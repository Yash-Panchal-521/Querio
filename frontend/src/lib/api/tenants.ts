import { apiFetch } from "./client";
import type { Organization } from "./me";

export function createOrganization(name: string): Promise<Organization> {
  return apiFetch<Organization>("/api/v1/tenants", { method: "POST", body: { name } });
}

export function getOrganization(tenantId: string): Promise<Organization> {
  return apiFetch<Organization>(`/api/v1/tenants/${tenantId}`, { cache: "no-store" });
}

export function renameOrganization(tenantId: string, name: string): Promise<Organization> {
  return apiFetch<Organization>(`/api/v1/tenants/${tenantId}`, { method: "PATCH", body: { name } });
}

export function deleteOrganization(tenantId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/tenants/${tenantId}`, { method: "DELETE" });
}
