import type { Metadata } from "next";
import { AcceptInvitation } from "./accept-invitation";

export const metadata: Metadata = {
  title: "Join an organization",
  // The token lives in the fragment, but there is nothing here worth indexing either way.
  robots: { index: false, follow: false },
};

export default function InvitePage() {
  return <AcceptInvitation />;
}
