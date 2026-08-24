"use client";

import { useCallback, useState } from "react";
import { OrganizationGate } from "@/components/app/organization-gate";
import { Page, PageHeader } from "@/components/app/page-shell";
import { DocumentRow } from "@/components/documents/document-row";
import { UploadPanel } from "@/components/documents/upload-panel";
import { UsageStrip } from "@/components/documents/usage-strip";
import { Skeleton } from "@/components/ui/skeleton";
import { useToast } from "@/components/ui/toast";
import { toApiMessage } from "@/lib/api/api-messages";
import {
  createDownloadLink,
  deleteDocument,
  getTenantUsage,
  isDocumentWorking,
  listDocuments,
  type Document,
  type TenantUsage,
} from "@/lib/api/documents";
import type { Organization } from "@/lib/api/me";
import { useAsyncData } from "@/lib/use-async-data";
import { usePoll } from "@/lib/use-poll";
import { useOrganizations } from "@/lib/auth/use-organizations";

export function DocumentsScreen() {
  return (
    <OrganizationGate>
      <Documents />
    </OrganizationGate>
  );
}

function Documents() {
  const { active } = useOrganizations();

  if (!active) {
    return null;
  }

  return <DocumentsFor organization={active} />;
}

function DocumentsFor({ organization }: { organization: Organization }) {
  const { showToast } = useToast();
  const [busyId, setBusyId] = useState<string | null>(null);

  const loadDocuments = useCallback(() => listDocuments(organization.id), [organization.id]);
  const loadUsage = useCallback(() => getTenantUsage(organization.id), [organization.id]);

  const { data: documents, loading, reload } = useAsyncData<Document[]>(loadDocuments);
  const { data: usage, reload: reloadUsage } = useAsyncData<TenantUsage>(loadUsage);

  const refresh = useCallback(async () => {
    await Promise.all([reload(), reloadUsage()]);
  }, [reload, reloadUsage]);

  // Only while something is actually moving. A permanent timer would keep an idle instance —
  // and the database behind it — awake for nothing, which on a plan that suspends when idle is
  // the whole month's compute allowance.
  const working = (documents ?? []).some((document) => isDocumentWorking(document));

  // Everything that can change while ingestion runs, and nothing that cannot. Progress on any
  // document restarts the poll at full speed; a queue that has not moved lets it slow down.
  const progress = (documents ?? [])
    .map(
      (document) =>
        `${document.id}:${document.status}:${document.embeddedChunkCount}:${document.resumesAt ?? ""}`,
    )
    .join("|");

  usePoll(refresh, working, { resetKey: progress });

  async function download(document: Document) {
    setBusyId(document.id);

    try {
      const link = await createDownloadLink(organization.id, document.id);

      // Straight to storage, so the file never passes through the API. The link is short-lived
      // and the bucket stays private.
      window.open(link.url, "_blank", "noopener,noreferrer");
    } catch (caught) {
      showToast(toApiMessage(caught), "error");
    } finally {
      setBusyId(null);
    }
  }

  async function remove(document: Document) {
    setBusyId(document.id);

    try {
      await deleteDocument(organization.id, document.id);
      await refresh();
      showToast(`${document.fileName} was deleted.`, "success");
    } catch (caught) {
      showToast(toApiMessage(caught), "error");
    } finally {
      setBusyId(null);
    }
  }

  const empty = !loading && (documents?.length ?? 0) === 0;

  return (
    <Page>
      <PageHeader
        eyebrow={organization.name}
        title="Documents"
        description="Everything here is searchable by everyone in this organization."
      />

      {usage && !empty ? <UsageStrip usage={usage} /> : null}

      {loading && !documents ? (
        <div className="border-border bg-card flex flex-col gap-4 rounded-xl border p-5">
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex items-center gap-3.5">
              <Skeleton className="size-9 rounded-lg" />
              <div className="flex flex-1 flex-col gap-2">
                <Skeleton className="h-4 w-56" />
                <Skeleton className="h-3 w-40" />
              </div>
              <Skeleton className="h-5 w-16 rounded-full" />
            </div>
          ))}
        </div>
      ) : empty ? (
        <UploadPanel tenantId={organization.id} onUploaded={refresh} variant="empty" />
      ) : (
        <>
          <section className="border-border bg-card overflow-hidden rounded-xl border shadow-xs">
            {(documents ?? []).map((document) => (
              <DocumentRow
                key={document.id}
                document={document}
                href={`/orgs/${organization.id}/documents/${document.id}`}
                busy={busyId === document.id}
                onDownload={() => void download(document)}
                onDelete={() => void remove(document)}
              />
            ))}
          </section>

          <UploadPanel tenantId={organization.id} onUploaded={refresh} />
        </>
      )}

      {empty ? (
        <p className="text-muted-foreground -mt-4 text-center text-xs">
          This organization can store {usage?.maxDocuments ?? 200} documents, up to{" "}
          {usage ? Math.round(usage.maxStoredBytes / 1_000_000) : 500} MB in total.
        </p>
      ) : null}

      <span className="sr-only" aria-live="polite">
        {working ? "Documents are still being processed." : "All documents are up to date."}
      </span>
    </Page>
  );
}
