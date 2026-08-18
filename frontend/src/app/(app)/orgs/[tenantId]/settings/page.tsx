import type { Metadata } from "next";
import { OrganizationSettings } from "./organization-settings";

export const metadata: Metadata = { title: "Organization settings" };

export default function OrganizationSettingsPage() {
  return <OrganizationSettings />;
}
