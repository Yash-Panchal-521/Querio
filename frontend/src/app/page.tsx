import type { Metadata } from "next";
import { LandingHero } from "@/components/marketing/landing-hero";

export const metadata: Metadata = {
  title: "Querio — answers grounded in your own documents",
};

/**
 * Static. The previous version probed API health server-side on every request, which was
 * scaffolding to prove the wiring: it made the landing page fail whenever the API was down,
 * and showed visitors a developer's diagnostic. Whether the API is reachable is something
 * the app finds out when it needs it, not something a marketing page reports.
 */
export default function HomePage() {
  return <LandingHero />;
}
