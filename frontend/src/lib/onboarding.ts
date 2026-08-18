/**
 * Tracks whether the setup checklist still has something to say for an organization.
 *
 * Only ever set for someone's *first* organization — a person creating their third does not
 * need onboarding again — and cleared once dismissed or complete, so the checklist is a
 * moment rather than furniture.
 */
const KEY_PREFIX = "querio.onboarding.";

function key(tenantId: string): string {
  return `${KEY_PREFIX}${tenantId}`;
}

export function markOnboardingPending(tenantId: string): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.setItem(key(tenantId), "pending");
}

export function isOnboardingPending(tenantId: string): boolean {
  if (typeof window === "undefined") {
    return false;
  }

  return window.localStorage.getItem(key(tenantId)) === "pending";
}

export function dismissOnboarding(tenantId: string): void {
  if (typeof window === "undefined") {
    return;
  }

  window.localStorage.removeItem(key(tenantId));
}
