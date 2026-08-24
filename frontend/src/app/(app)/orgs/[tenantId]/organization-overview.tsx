"use client";

import { useCallback } from "react";
import Link from "next/link";
import { FileText, MessageSquareQuote, Settings, Upload, Users } from "lucide-react";
import { buttonClasses } from "@/components/ui/button";
import { Card, EmptyState, Page, PageHeader } from "@/components/app/page-shell";
import { OrganizationGate } from "@/components/app/organization-gate";
import { SetupChecklist } from "@/components/app/setup-checklist";
import { getTenantUsage } from "@/lib/api/documents";
import type { Organization } from "@/lib/api/me";
import { useAsyncData } from "@/lib/use-async-data";
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

  // Split so the data hook sits above no early return — the organization is resolved first,
  // then everything that depends on it.
  return <OverviewFor active={active} />;
}

function OverviewFor({ active }: { active: Organization }) {
  const load = useCallback(() => getTenantUsage(active.id), [active.id]);
  const { data: usage } = useAsyncData(load);

  const documentCount = usage?.documentCount ?? 0;

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
        <Stat label="Documents" value={usage ? String(documentCount) : "—"} icon={FileText} />
      </div>

      <Card
        title="Documents"
        description="Upload PDFs, Word documents, Markdown or plain text. Querio reads them and answers from what it finds."
        actions={
          documentCount > 0 ? (
            <Link
              href={`/orgs/${active.id}/documents`}
              className={buttonClasses({ variant: "secondary", size: "sm" })}
            >
              View all
            </Link>
          ) : undefined
        }
      >
        {documentCount > 0 ? (
          <p className="text-muted-foreground text-sm">
            {documentCount} document{documentCount === 1 ? "" : "s"} stored, searchable by everyone
            in {active.name}.
          </p>
        ) : (
          <EmptyState
            icon={Upload}
            title="No documents yet"
            description="Add one and Querio will read it, split it into passages and make it searchable."
            action={
              <Link
                href={`/orgs/${active.id}/documents`}
                className={buttonClasses({ variant: "primary", size: "sm" })}
              >
                Upload a document
              </Link>
            }
          />
        )}
      </Card>

      <Card
        title="Ask a question"
        description="Answers stream as they are written, with a citation on every claim."
      >
        <EmptyState
          icon={MessageSquareQuote}
          title="Asking arrives next"
          description="Documents are already being read and indexed. Answering questions from them is the next release."
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
