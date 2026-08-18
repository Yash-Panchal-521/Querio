import type { Metadata } from "next";
import { MembersScreen } from "./members-screen";

export const metadata: Metadata = { title: "Members" };

export default function MembersPage() {
  return <MembersScreen />;
}
