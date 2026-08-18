"use client";

import { useCallback, useState } from "react";
import { UserMinus, Users } from "lucide-react";
import { toApiMessage } from "@/lib/api/api-messages";
import type { Organization, TenantRole } from "@/lib/api/me";
import { changeMemberRole, listMembers, removeMember } from "@/lib/api/members";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, EmptyState } from "@/components/app/page-shell";
import { RoleSelect } from "@/components/members/role-select";
import { Skeleton } from "@/components/ui/skeleton";
import { useAsyncData } from "@/lib/use-async-data";
import { useSession } from "@/lib/auth/session-context";
import { useToast } from "@/components/ui/toast";

export function MemberList({ organization }: { organization: Organization }) {
  const { showToast } = useToast();
  const { refresh } = useSession();
  const [busyUserId, setBusyUserId] = useState<string | null>(null);

  const load = useCallback(() => listMembers(organization.id), [organization.id]);
  const { data, loading, reload } = useAsyncData(load);

  const isOwner = organization.role === "Owner";

  async function run(userId: string, action: () => Promise<void>, success: string) {
    setBusyUserId(userId);

    try {
      await action();
      await reload();
      // Member count and the caller's own role live on the session, so a change here can
      // alter the sidebar and the switcher too.
      await refresh();
      showToast(success, "success");
    } catch (caught) {
      showToast(toApiMessage(caught), "error");
    } finally {
      setBusyUserId(null);
    }
  }

  return (
    <Card
      title="People with access"
      description={
        organization.role === "Admin"
          ? "Admins can remove members, but not other admins or owners."
          : undefined
      }
    >
      {loading && !data ? (
        <div className="flex flex-col gap-4">
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex items-center gap-3">
              <Skeleton className="size-9 rounded-full" />
              <div className="flex flex-1 flex-col gap-1.5">
                <Skeleton className="h-3.5 w-40" />
                <Skeleton className="h-3 w-56" />
              </div>
            </div>
          ))}
        </div>
      ) : null}

      {data && data.length === 0 ? (
        <EmptyState
          icon={Users}
          title="Nobody else here yet"
          description="Invite a teammate above and they will appear here once they accept."
        />
      ) : null}

      {data && data.length > 0 ? (
        <ul className="divide-border -my-1 divide-y">
          {data.map((member) => (
            <li key={member.userId} className="flex flex-wrap items-center gap-3 py-3">
              <Avatar className="size-9">
                <AvatarFallback className="bg-muted text-muted-foreground text-xs font-medium">
                  {initials(member.displayName ?? member.email)}
                </AvatarFallback>
              </Avatar>

              <div className="flex min-w-0 flex-1 flex-col">
                <span className="truncate text-sm font-medium">
                  {member.displayName ?? member.email}
                </span>
                <span className="text-muted-foreground truncate text-xs">{member.email}</span>
              </div>

              <div className="flex items-center gap-2">
                {isOwner ? (
                  <RoleSelect
                    value={member.role}
                    size="sm"
                    label={`Role for ${member.email}`}
                    disabled={busyUserId === member.userId}
                    onChange={(role: TenantRole) =>
                      void run(
                        member.userId,
                        () => changeMemberRole(organization.id, member.userId, role),
                        "Role updated.",
                      )
                    }
                  />
                ) : (
                  <span className="border-border text-muted-foreground rounded-full border px-2.5 py-0.5 text-xs">
                    {member.role}
                  </span>
                )}

                {canRemove(organization.role, member.role) ? (
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={`Remove ${member.email}`}
                    loading={busyUserId === member.userId}
                    onClick={() =>
                      void run(
                        member.userId,
                        () => removeMember(organization.id, member.userId),
                        "Member removed.",
                      )
                    }
                  >
                    <UserMinus />
                  </Button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      ) : null}
    </Card>
  );
}

/**
 * Mirrors what the API will allow, so nobody is offered a button that can only be refused.
 * The server enforces this regardless — this is about not wasting someone's click.
 */
function canRemove(actorRole: TenantRole, targetRole: TenantRole): boolean {
  if (actorRole === "Owner") {
    return true;
  }

  return actorRole === "Admin" && targetRole === "Member";
}

/** Two letters at most — more turns an avatar into a word. */
function initials(value: string): string {
  const parts = value
    .trim()
    .split(/[\s@.]+/)
    .filter(Boolean);

  return (
    parts
      .slice(0, 2)
      .map((part) => part[0] ?? "")
      .join("")
      .toUpperCase() || "?"
  );
}
