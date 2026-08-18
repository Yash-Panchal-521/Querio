"use client";

import Link from "next/link";
import { Building2, Plus } from "lucide-react";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { buttonClasses } from "@/components/ui/button";
import { Card, EmptyState, Page, PageHeader } from "@/components/app/page-shell";
import { VerifyEmailNotice } from "@/components/auth/verify-email-notice";
import { useSession } from "@/lib/auth/session-context";

export function AccountOverview() {
  const { session, refresh } = useSession();

  if (session.status !== "ready") {
    return null;
  }

  const { profile, user } = session;
  const name = profile.displayName ?? profile.email;

  return (
    <Page>
      <div className="flex items-center gap-4">
        <Avatar className="size-14">
          <AvatarFallback className="bg-primary/10 text-primary text-lg font-medium">
            {initials(name)}
          </AvatarFallback>
        </Avatar>

        <PageHeader title={profile.displayName ?? "Your account"} description={profile.email} />
      </div>

      {profile.emailVerified ? null : (
        <VerifyEmailNotice uid={user.uid} email={profile.email} onVerified={refresh} />
      )}

      <Card
        title="Your organizations"
        description="Each one keeps its documents separate from every other."
        actions={
          <Link href="/orgs/new" className={buttonClasses({ variant: "secondary", size: "sm" })}>
            <Plus />
            New
          </Link>
        }
      >
        {profile.organizations.length === 0 ? (
          <EmptyState
            icon={Building2}
            title="No organizations yet"
            description="Create one to upload documents and invite your team, or accept an invitation you have been sent."
            action={
              <Link href="/orgs/new" className={buttonClasses()}>
                Create an organization
              </Link>
            }
          />
        ) : (
          <ul className="divide-border -my-1 divide-y">
            {profile.organizations.map((organization) => (
              <li key={organization.id}>
                <Link
                  href={`/orgs/${organization.id}`}
                  className="hover:bg-accent/60 -mx-2 flex items-center gap-3 rounded-md px-2 py-3 transition-colors"
                >
                  <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-lg">
                    <Building2 className="size-4" />
                  </span>
                  <div className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-sm font-medium">{organization.name}</span>
                    <span className="text-muted-foreground truncate font-mono text-xs">
                      {organization.slug}
                    </span>
                  </div>
                  <span className="border-border text-muted-foreground rounded-full border px-2.5 py-0.5 text-xs">
                    {organization.role}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </Page>
  );
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
