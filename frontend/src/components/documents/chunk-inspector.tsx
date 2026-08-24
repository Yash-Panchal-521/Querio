"use client";

import { useCallback, useState } from "react";
import { Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, EmptyState } from "@/components/app/page-shell";
import { Skeleton } from "@/components/ui/skeleton";
import { listDocumentChunks, type DocumentChunkPage } from "@/lib/api/documents";
import { useAsyncData } from "@/lib/use-async-data";

const PAGE_SIZE = 25;

/**
 * The passages a document became.
 *
 * Comparable products treat chunking as an implementation detail. Showing it is the point:
 * when an answer is wrong later, the cause is inspectable rather than a matter of trust — and
 * it is the difference between a pipeline somebody can believe in and one they cannot check.
 */
export function ChunkInspector({ tenantId, documentId }: { tenantId: string; documentId: string }) {
  const [skip, setSkip] = useState(0);

  const load = useCallback(
    () => listDocumentChunks(tenantId, documentId, skip, PAGE_SIZE),
    [tenantId, documentId, skip],
  );

  const { data, loading, error } = useAsyncData<DocumentChunkPage>(load);

  const total = data?.total ?? 0;
  const from = total === 0 ? 0 : skip + 1;
  const to = Math.min(skip + PAGE_SIZE, total);

  return (
    <Card
      title="Passages"
      description="What search actually looks at. Each one is embedded separately."
      actions={
        total > 0 ? (
          <span className="text-muted-foreground font-mono text-xs">
            {from}–{to} of {total}
          </span>
        ) : undefined
      }
    >
      {loading && !data ? (
        <div className="flex flex-col gap-5">
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex flex-col gap-2">
              <Skeleton className="h-4 w-64" />
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-4/5" />
            </div>
          ))}
        </div>
      ) : error ? (
        <p className="text-destructive text-sm">{error}</p>
      ) : total === 0 ? (
        <EmptyState
          icon={Search}
          title="No passages yet"
          description="They appear here once this document has been read and split."
        />
      ) : (
        <>
          <div className="flex flex-col">
            {data?.chunks.map((chunk) => (
              <article
                key={chunk.id}
                className="border-border flex gap-4 border-b py-4 first:pt-0 last:border-b-0 last:pb-0"
              >
                <span className="text-muted-foreground w-7 shrink-0 pt-0.5 font-mono text-xs">
                  {String(chunk.ordinal + 1).padStart(2, "0")}
                </span>

                <div className="flex min-w-0 flex-1 flex-col gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    {chunk.breadcrumb ? (
                      // The citation hue, not the brand one: this is provenance, not a link.
                      <span className="text-citation bg-citation-subtle rounded-md px-2 py-0.5 text-xs font-medium">
                        {chunk.breadcrumb}
                      </span>
                    ) : null}
                    <span className="text-muted-foreground font-mono text-[11px]">
                      {chunk.pageNumber !== null ? `page ${chunk.pageNumber} · ` : ""}
                      {/* Approximate by construction, and labelled as such rather than implied. */}
                      ≈{chunk.approximateTokenCount} tokens
                      {chunk.hasEmbedding ? "" : " · not yet embedded"}
                    </span>
                  </div>

                  <p className="text-sm leading-relaxed text-pretty">{chunk.text}</p>
                </div>
              </article>
            ))}
          </div>

          {total > PAGE_SIZE ? (
            <div className="flex items-center justify-center gap-2">
              <Button
                variant="secondary"
                size="sm"
                disabled={skip === 0}
                onClick={() => setSkip((current) => Math.max(0, current - PAGE_SIZE))}
              >
                Previous
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={to >= total}
                onClick={() => setSkip((current) => current + PAGE_SIZE)}
              >
                Next
              </Button>
            </div>
          ) : null}
        </>
      )}
    </Card>
  );
}
