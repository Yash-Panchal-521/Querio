const DEV_API_BASE_URL = "http://localhost:5063";

function stripTrailingSlashes(value: string): string {
  return value.replace(/\/+$/, "");
}

/**
 * Resolved lazily rather than at module load. A missing value should fail the request that
 * needs it — not the production build that merely imports this file.
 *
 * Referenced as a literal `process.env.NEXT_PUBLIC_*` so Next can inline it at build time;
 * a dynamic lookup would silently yield undefined in the browser bundle.
 */
export function getApiBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();

  if (configured) {
    return stripTrailingSlashes(configured);
  }

  if (process.env.NODE_ENV === "production") {
    throw new Error(
      "NEXT_PUBLIC_API_BASE_URL is not set. Point it at the Querio API before serving production traffic.",
    );
  }

  return DEV_API_BASE_URL;
}
