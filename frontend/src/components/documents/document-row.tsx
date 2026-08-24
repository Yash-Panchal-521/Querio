"use client";

import Link from "next/link";
import { Download, FileText, MoreHorizontal, Trash2 } from "lucide-react";
import type { Document } from "@/lib/api/documents";
import { describeFormat, formatBytes, formatUploadedAt } from "@/lib/documents/presentation";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StatusPill } from "@/components/documents/status-pill";

interface DocumentRowProps {
  document: Document;
  href: string;
  busy: boolean;
  onDownload: () => void;
  onDelete: () => void;
}

export function DocumentRow({ document, href, busy, onDownload, onDelete }: DocumentRowProps) {
  return (
    <div className="border-border flex items-center gap-3.5 border-b px-5 py-3.5 last:border-b-0">
      <span className="bg-muted text-muted-foreground flex size-9 shrink-0 items-center justify-center rounded-lg">
        <FileText className="size-4.5" aria-hidden />
      </span>

      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <Link href={href} className="truncate text-sm font-medium hover:underline">
          {document.fileName}
        </Link>
        <Detail document={document} />
      </div>

      <StatusPill status={document.status} />

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            disabled={busy}
            aria-label={`Actions for ${document.fileName}`}
          >
            <MoreHorizontal className="size-4" aria-hidden />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onSelect={onDownload}>
            <Download className="size-4" aria-hidden />
            Download original
          </DropdownMenuItem>
          <DropdownMenuItem onSelect={onDelete} variant="destructive">
            <Trash2 className="size-4" aria-hidden />
            Delete
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

/**
 * The second line carries whatever is most useful in this state, rather than the same
 * metadata throughout. A failing document's size is irrelevant; its reason is the only thing
 * on the row anybody wants.
 */
function Detail({ document }: { document: Document }) {
  if (document.status === "Failed") {
    return (
      <p className="text-destructive text-xs text-pretty">
        {document.failureReason ?? "Something went wrong while processing this document."}
      </p>
    );
  }

  if (document.status === "WaitingForQuota") {
    return (
      <p className="text-muted-foreground text-xs text-pretty">
        {document.pauseReason ?? "Waiting for the embedding allowance to reset."} Nothing to do —
        this picks up on its own, and the passages already done are kept.
      </p>
    );
  }

  if (document.status === "Embedding" && document.chunkCount > 0) {
    const done = Math.min(document.embeddedChunkCount, document.chunkCount);

    return (
      <div className="flex items-center gap-2.5">
        <div
          className="bg-border h-1 w-40 overflow-hidden rounded-full"
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={document.chunkCount}
          aria-valuenow={done}
          aria-label="Passages embedded"
        >
          <div
            className="bg-primary h-full rounded-full transition-[width] duration-500"
            style={{ width: `${(done / document.chunkCount) * 100}%` }}
          />
        </div>
        <span className="text-muted-foreground font-mono text-xs">
          {done} of {document.chunkCount} passages
        </span>
      </div>
    );
  }

  const parts = [describeFormat(document.format), formatBytes(document.byteSize)];

  if (document.status === "Ready") {
    parts.push(`${document.chunkCount} passage${document.chunkCount === 1 ? "" : "s"}`);
  }

  parts.push(formatUploadedAt(document.createdAt));

  return <p className="text-muted-foreground truncate text-xs">{parts.join(" · ")}</p>;
}
