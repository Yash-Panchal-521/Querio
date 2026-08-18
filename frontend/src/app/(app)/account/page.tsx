import type { Metadata } from "next";
import { AccountOverview } from "./account-overview";

export const metadata: Metadata = { title: "Your account" };

export default function AccountPage() {
  return <AccountOverview />;
}
