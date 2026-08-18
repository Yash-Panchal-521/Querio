import type { Metadata } from "next";
import { OrgsEntry } from "./orgs-entry";

export const metadata: Metadata = { title: "Home" };

export default function AppHomePage() {
  return <OrgsEntry />;
}
