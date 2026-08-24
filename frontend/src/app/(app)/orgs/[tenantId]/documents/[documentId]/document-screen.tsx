"use client";

import { useCallback, useState } from "react";
import Link from "next/link";
import { ChevronLeft, Download } from "lucide-react";
import { OrganizationGate } from "@/components/app/organization-gate";
import { Page } from "@/components/app/page-shell";
import { ChunkInspector } from "@/components/documents/chunk-inspector";
import { StatusPill } from "@/components/documents/status-pill";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useToast } from "@/components/ui/toast";
import { toApiMessage } from "@/lib/api/api-messages";
import {
  createDownloadLink,
  getDocument,
  isDocumentWorking,
  type Document,
} from "@/lib/api/documents";
import type { Organization } from "@/lib/api/me";
import { describeFormat, formatBytes, formatUploadedAt } from "@/lib/documents/presentation";
import { useAsyncData } from "@/lib/use-async-data";
import { usePoll } from "@/lib/use-poll";
import { useOrganizations } from "@/lib/auth/use-organizations";

export function DocumentScreen({ documentId }: { documentId: string }) {
  return (
    <OrganizationGate>
      <Detail documentId={documentId} />
    </OrganizationGate>
  );
}

function Detail({ documentId }: { documentId: string }) {
  const { active } = useOrganizations();

  if (!active) {
    return null;
  }

  return <DetailFor organization={active} documentId={documentId} />;
}

function DetailFor({
  organization,
  documentId,
}: {
  organization: Organization;
  documentId: string;
}) {
  const { showToast } = useToast();
  const [downloading, setDownloading] = useState(false);

  const load = useCallback(
    () => getDocument(organization.id, documentId),
    [organization.id, documentId],
  );

  const { data: document, loading, error, reload } = useAsyncData<Document>(load);

  const working = document !== null && isDocumentWorking(document);

  usePoll(reload, working, {
    resetKey: document
      ? `${document.status}:${document.embeddedChunkCount}:${document.resumesAt ?? ""}`
      : "",
  });

  async function download() {
    setDownloading(true);

    try {
      const link = await createDownloadLink(organization.id, documentId);

      window.open(link.url, "_blank", "noopener,noreferrer");
    } catch (caught) {
      showToast(toApiMessage(caught), "error");
    } finally {
      setDownloading(false);
    }
  }

  return (
    <Page>
      <div className="flex flex-col gap-3.5">
        <Link
          href={`/orgs/${organization.id}/documents`}
          className="text-muted-foreground hover:text-foreground inline-flex w-fit items-center gap-1.5 text-sm"
        >
          <ChevronLeft className="size-3.5" aria-hidden />
          Documents
        </Link>

        {loading && !document ? (
          <div className="flex flex-col gap-2">
            <Skeleton className="h-7 w-80" />
            <Skeleton className="h-4 w-64" />
          </div>
        ) : error ? (
          <p className="text-destructive text-sm">{error}</p>
        ) : document ? (
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="flex min-w-0 flex-col gap-2">
              <h1 className="truncate text-2xl font-semibold tracking-tight">
                {document.fileName}
              </h1>
              <div className="flex flex-wrap items-center gap-2.5">
                <StatusPill status={document.status} />
                <span className="text-muted-foreground text-sm">
                  {describeFormat(document.format)} · {formatBytes(document.byteSize)}
                  {document.status === "Ready" ? ` · ${document.chunkCount} passages` : ""} ·
                  uploaded {formatUploadedAt(document.createdAt)}
                </span>
              </div>
              {document.failureReason ? (
                <p className="text-destructive max-w-prose text-sm text-pretty">
                  {document.failureReason}
                </p>
              ) : null}
            </div>

            <Button
              variant="secondary"
              size="sm"
              loading={downloading}
              onClick={() => void download()}
            >
              <Download className="size-4" aria-hidden />
              Download
            </Button>
          </div>
        ) : null}
      </div>

      {document ? <ChunkInspector tenantId={organization.id} documentId={documentId} /> : null}
    </Page>
  );
}
