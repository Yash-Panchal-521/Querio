"use client";

import Link from "next/link";
import { FileText, MessageSquareQuote, Settings, Upload, Users } from "lucide-react";
import { buttonClasses } from "@/components/ui/button";
import { Card, EmptyState, Page, PageHeader } from "@/components/app/page-shell";
import { OrganizationGate } from "@/components/app/organization-gate";
import { SetupChecklist } from "@/components/app/setup-checklist";
import { useOrganizations } from "@/lib/auth/use-organizations";

export function OrganizationOverview() {
  return (
    <OrganizationGate>
      <Overview />
    </OrganizationGate>
  );
}

function Overview() {
  const { active } = useOrganizations();

  if (!active) {
    return null;
  }

  return (
    <Page>
      <PageHeader
        eyebrow={active.slug}
        title={active.name}
        description="Everything your team uploads lives here, and every answer is drawn only from it."
        actions={
          active.role === "Owner" ? (
            <Link
              href={`/orgs/${active.id}/settings`}
              className={buttonClasses({ variant: "secondary" })}
            >
              <Settings />
              Settings
            </Link>
          ) : null
        }
      />

      {/* Renders only for a first organization that has not been dismissed, and decides
          that for itself. */}
      <SetupChecklist organization={active} />

      <div className="grid gap-4 sm:grid-cols-3">
        <Stat label="Your role" value={active.role} icon={Users} />
        <Stat label="Members" value={String(active.memberCount)} icon={Users} />
        <Stat label="Documents" value="0" icon={FileText} />
      </div>

      <Card
        title="Documents"
        description="Upload PDFs, Markdown or plain text. Querio reads them and answers from what it finds."
      >
        <EmptyState
          icon={Upload}
          title="No documents yet"
          description="Uploading and asking questions arrive with the next release. This organization is ready for them."
        />
      </Card>

      <Card
        title="Ask a question"
        description="Answers stream as they are written, with a citation on every claim."
      >
        <EmptyState
          icon={MessageSquareQuote}
          title="Nothing to ask yet"
          description="Once documents are uploaded, this is where you will ask about them."
        />
      </Card>
    </Page>
  );
}

function Stat({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string;
  icon: React.ComponentType<{ className?: string }>;
}) {
  return (
    <div className="border-border bg-card flex items-center gap-3 rounded-xl border p-4 shadow-xs">
      <span className="bg-primary/10 text-primary flex size-9 shrink-0 items-center justify-center rounded-lg">
        <Icon className="size-4" />
      </span>
      <div className="flex min-w-0 flex-col">
        <span className="text-muted-foreground text-xs">{label}</span>
        <span className="truncate text-lg font-semibold tracking-tight">{value}</span>
      </div>
    </div>
  );
}
