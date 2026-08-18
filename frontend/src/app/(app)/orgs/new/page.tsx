import type { Metadata } from "next";
import { CreateOrganizationForm } from "./create-organization-form";

export const metadata: Metadata = { title: "Create an organization" };

export default function NewOrganizationPage() {
  return <CreateOrganizationForm />;
}
