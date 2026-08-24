import { apiFetch, apiFetchResult } from "./client";

/** Mirrors the server's FileFormat. */
export type FileFormat = "PlainText" | "Markdown" | "Pdf" | "Word";

/**
 * Mirrors the server's DocumentStatus.
 *
 * Four of these mean "working" and one means "paused" — the interface distinguishes them
 * because a document that has stalled should say where, and a pause is not a failure.
 */
export type DocumentStatus =
  "Pending" | "Extracting" | "Chunking" | "Embedding" | "WaitingForQuota" | "Ready" | "Failed";

export interface Document {
  id: string;
  fileName: string;
  format: FileFormat;
  byteSize: number;
  status: DocumentStatus;
  chunkCount: number;
  embeddedChunkCount: number;
  failureCode: string | null;
  failureReason: string | null;
  /** Why it is paused, in the server's words. Only set while WaitingForQuota. */
  pauseReason: string | null;
  /** When the queue picks it up again. Only set while WaitingForQuota. */
  resumesAt: string | null;
  uploadedByUserId: string;
  createdAt: string;
}

export interface DocumentChunk {
  id: string;
  ordinal: number;
  text: string;
  breadcrumb: string | null;
  pageNumber: number | null;
  approximateTokenCount: number;
  hasEmbedding: boolean;
}

export interface DocumentChunkPage {
  chunks: DocumentChunk[];
  total: number;
}

export interface DownloadLink {
  url: string;
  expiresAt: string;
}

export interface TenantUsage {
  documentCount: number;
  maxDocuments: number;
  storedBytes: number;
  maxStoredBytes: number;
  chunkCount: number;
  readyDocumentCount: number;
  failedDocumentCount: number;
}

/**
 * How long a pause is worth watching through. A throttle clears in a minute or two, which is
 * shorter than most people's patience; a spent daily allowance does not, and polling until
 * midnight would keep an idle instance and its database awake for nothing.
 */
const WATCHABLE_PAUSE_MS = 5 * 60 * 1000;

/**
 * Whether ingestion is still moving. Drives polling — the list refreshes only while there is
 * something to see change, rather than on a permanent timer.
 *
 * A paused document counts only when it resumes soon. Excluding every pause was the safe
 * reading of "this could sit for hours", but it also meant the common case — throttled for two
 * minutes — sat at "Paused" until someone reloaded the page, while the row promised it would
 * resume on its own.
 */
export function isDocumentWorking(document: Pick<Document, "status" | "resumesAt">): boolean {
  const { status } = document;

  if (
    status === "Pending" ||
    status === "Extracting" ||
    status === "Chunking" ||
    status === "Embedding"
  ) {
    return true;
  }

  if (status !== "WaitingForQuota" || !document.resumesAt) {
    return false;
  }

  const waitMs = new Date(document.resumesAt).getTime() - Date.now();

  // Past its resume time and still paused: the worker has not got to it yet, so keep watching
  // rather than freezing on a promise that has already come due.
  return waitMs < WATCHABLE_PAUSE_MS;
}

export function listDocuments(tenantId: string): Promise<Document[]> {
  return apiFetch<Document[]>(`/api/v1/tenants/${tenantId}/documents`, { cache: "no-store" });
}

export function getDocument(tenantId: string, documentId: string): Promise<Document> {
  return apiFetch<Document>(`/api/v1/tenants/${tenantId}/documents/${documentId}`, {
    cache: "no-store",
  });
}

export function listDocumentChunks(
  tenantId: string,
  documentId: string,
  skip: number,
  take: number,
): Promise<DocumentChunkPage> {
  return apiFetch<DocumentChunkPage>(`/api/v1/tenants/${tenantId}/documents/${documentId}/chunks`, {
    searchParams: { skip, take },
    cache: "no-store",
  });
}

export function createDownloadLink(tenantId: string, documentId: string): Promise<DownloadLink> {
  return apiFetch<DownloadLink>(
    `/api/v1/tenants/${tenantId}/documents/${documentId}/download-link`,
    { method: "POST" },
  );
}

export function deleteDocument(tenantId: string, documentId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/tenants/${tenantId}/documents/${documentId}`, {
    method: "DELETE",
  });
}

export function getTenantUsage(tenantId: string): Promise<TenantUsage> {
  return apiFetch<TenantUsage>(`/api/v1/tenants/${tenantId}/usage`, { cache: "no-store" });
}

/**
 * The server answers 201 for a new document and 200 for one this organization already has —
 * the same bytes under any name. Both return the document, so the caller needs the status to
 * tell the difference and say so rather than appearing to do nothing.
 */
export async function uploadDocument(
  tenantId: string,
  file: File,
): Promise<{ document: Document; alreadyExisted: boolean }> {
  const form = new FormData();
  form.append("file", file, file.name);

  // Deliberately no Content-Type header: only the browser knows the multipart boundary it
  // generated, so setting one here produces a body the server cannot parse.
  const result = await apiFetchResult<Document>(`/api/v1/tenants/${tenantId}/documents`, {
    method: "POST",
    body: form,
  });

  return { document: result.data, alreadyExisted: result.status === 200 };
}
