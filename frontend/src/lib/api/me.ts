import { apiFetch } from "./client";

export type TenantRole = "Member" | "Admin" | "Owner";

export interface Organization {
  id: string;
  name: string;
  slug: string;
  role: TenantRole;
  memberCount: number;
}

export interface UserProfile {
  id: string;
  email: string;
  emailVerified: boolean;
  displayName: string | null;
  organizations: Organization[];
}

/**
 * Creates or refreshes the Querio profile from the Firebase token. Called after every
 * sign-in, not only the first: it also picks up a changed display name or an address
 * verified since last time.
 */
export function bootstrapProfile(): Promise<UserProfile> {
  return apiFetch<UserProfile>("/api/v1/me/bootstrap", { method: "POST" });
}

export function getProfile(): Promise<UserProfile> {
  return apiFetch<UserProfile>("/api/v1/me", { cache: "no-store" });
}
