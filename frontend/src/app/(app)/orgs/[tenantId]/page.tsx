import type { Metadata } from "next";
import { OrganizationOverview } from "./organization-overview";

export const metadata: Metadata = { title: "Organization" };

export default function OrganizationPage() {
  return <OrganizationOverview />;
}
