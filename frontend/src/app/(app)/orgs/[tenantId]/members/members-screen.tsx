"use client";

import { OrganizationGate } from "@/components/app/organization-gate";
import { InviteSection } from "@/components/members/invite-section";
import { LeaveOrganization } from "@/components/members/leave-organization";
import { MemberList } from "@/components/members/member-list";
import { Page, PageHeader } from "@/components/app/page-shell";
import { useOrganizations } from "@/lib/auth/use-organizations";

export function MembersScreen() {
  return (
    <OrganizationGate>
      <Members />
    </OrganizationGate>
  );
}

function Members() {
  const { active } = useOrganizations();

  if (!active) {
    return null;
  }

  // Owner and Admin share the invite surface; only an Owner can change roles.
  const canInvite = active.role === "Owner" || active.role === "Admin";

  return (
    <Page>
      <PageHeader
        eyebrow={active.name}
        title="Members"
        description="Everyone here can read every document in this organization."
      />

      {canInvite ? <InviteSection organization={active} /> : null}

      <MemberList organization={active} />

      <LeaveOrganization organization={active} />
    </Page>
  );
}
