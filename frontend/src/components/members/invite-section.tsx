"use client";

import { useCallback, useState } from "react";
import { Clock, Mail, UserPlus, X } from "lucide-react";
import { toApiMessage, toFieldErrors } from "@/lib/api/api-messages";
import type { Organization, TenantRole } from "@/lib/api/me";
import {
  buildInvitationLink,
  inviteMember,
  listInvitations,
  revokeInvitation,
  type IssuedInvitation,
} from "@/lib/api/invitations";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/app/page-shell";
import { Field } from "@/components/ui/field";
import { InvitationLink } from "@/components/members/invitation-link";
import { RoleSelect } from "@/components/members/role-select";
import { useAsyncData } from "@/lib/use-async-data";
import { useToast } from "@/components/ui/toast";

export function InviteSection({ organization }: { organization: Organization }) {
  const { showToast } = useToast();

  const [email, setEmail] = useState("");
  const [role, setRole] = useState<TenantRole>("Member");
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [issued, setIssued] = useState<IssuedInvitation | null>(null);
  const [pending, setPending] = useState(false);
  const [revoking, setRevoking] = useState<string | null>(null);

  const load = useCallback(() => listInvitations(organization.id), [organization.id]);
  const { data, reload } = useAsyncData(load);

  async function invite() {
    setFieldError(undefined);
    setPending(true);

    try {
      const invitation = await inviteMember(organization.id, email, role);

      // Held in state because this is the only moment the token exists outside the
      // database's hash of it — navigating away loses it for good.
      setIssued(invitation);
      setEmail("");

      await reload();
    } catch (caught) {
      setFieldError(toFieldErrors(caught).email);
      showToast(toApiMessage(caught), "error");
    } finally {
      setPending(false);
    }
  }

  async function revoke(invitationId: string) {
    setRevoking(invitationId);

    try {
      await revokeInvitation(organization.id, invitationId);

      // If the revoked invitation is the one on screen, its link is now dead — stop
      // offering it to be copied.
      setIssued((current) => (current?.id === invitationId ? null : current));

      await reload();
      showToast("Invitation revoked. That link no longer works.", "success");
    } catch (caught) {
      showToast(toApiMessage(caught), "error");
    } finally {
      setRevoking(null);
    }
  }

  return (
    <Card
      title="Invite a teammate"
      description="You will get a link to send them yourself. It works once, only for the address you enter, and expires after 7 days."
    >
      <form
        noValidate
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          void invite();
        }}
      >
        <div className="min-w-[15rem] flex-1">
          <Field
            label="Email address"
            type="email"
            name="email"
            icon={Mail}
            placeholder="teammate@company.com"
            required
            error={fieldError}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </div>

        <RoleSelect
          value={role}
          onChange={setRole}
          label="Role for the new member"
          // An admin cannot create an owner, so the option is not offered to one.
          allowOwner={organization.role === "Owner"}
        />

        <Button type="submit" loading={pending}>
          <UserPlus />
          Invite
        </Button>
      </form>

      {issued ? (
        <InvitationLink
          email={issued.email}
          link={buildInvitationLink(window.location.origin, issued.token)}
        />
      ) : null}

      {data && data.length > 0 ? (
        <div className="flex flex-col gap-2">
          <h3 className="text-muted-foreground flex items-center gap-1.5 text-xs font-medium">
            <Clock className="size-3.5" />
            Waiting to be accepted
          </h3>

          <ul className="divide-border divide-y">
            {data.map((invitation) => (
              <li key={invitation.id} className="flex flex-wrap items-center gap-3 py-2.5">
                <div className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate text-sm">{invitation.email}</span>
                  <span className="text-muted-foreground text-xs">
                    {invitation.role} · expires {formatDate(invitation.expiresAt)}
                  </span>
                </div>

                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Revoke invitation for ${invitation.email}`}
                  loading={revoking === invitation.id}
                  onClick={() => void revoke(invitation.id)}
                >
                  <X />
                </Button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </Card>
  );
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
