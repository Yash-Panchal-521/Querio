import type { DocumentStatus, FileFormat } from "@/lib/api/documents";

/**
 * How a state is named to a reader, and what it means.
 *
 * The names are not the enum's. "Extracting" and "Chunking" describe the implementation;
 * "Reading" and "Splitting" describe what is happening to their file. Four of the seven are
 * one idea — working — and saying which part is what lets a stalled document say where.
 */
export type StatusTone = "working" | "paused" | "ready" | "failed";

interface StatusPresentation {
  label: string;
  tone: StatusTone;
}

const STATUSES: Record<DocumentStatus, StatusPresentation> = {
  Pending: { label: "Queued", tone: "working" },
  Extracting: { label: "Reading", tone: "working" },
  Chunking: { label: "Splitting", tone: "working" },
  Embedding: { label: "Embedding", tone: "working" },
  WaitingForQuota: { label: "Paused", tone: "paused" },
  Ready: { label: "Ready", tone: "ready" },
  Failed: { label: "Failed", tone: "failed" },
};

export function describeStatus(status: DocumentStatus): StatusPresentation {
  return STATUSES[status];
}

const FORMATS: Record<FileFormat, string> = {
  PlainText: "Text",
  Markdown: "Markdown",
  Pdf: "PDF",
  Word: "Word",
};

export function describeFormat(format: FileFormat): string {
  return FORMATS[format];
}

/**
 * Bytes as a person reads them.
 *
 * Decimal units, not binary: a file manager says 2.4 MB for the same file, and matching the
 * uploader's own machine matters more than matching the disk.
 */
export function formatBytes(bytes: number): string {
  if (bytes < 1000) {
    return `${bytes} B`;
  }

  const units = ["kB", "MB", "GB"];
  let value = bytes / 1000;
  let unit = 0;

  while (value >= 1000 && unit < units.length - 1) {
    value /= 1000;
    unit += 1;
  }

  return `${value < 10 ? value.toFixed(1) : Math.round(value)} ${units[unit]}`;
}

/** "12 August" — the year only when it is not this one, because it is noise until it isn't. */
export function formatUploadedAt(iso: string): string {
  const date = new Date(iso);
  const sameYear = date.getUTCFullYear() === new Date().getUTCFullYear();

  return date.toLocaleDateString(undefined, {
    day: "numeric",
    month: "long",
    ...(sameYear ? {} : { year: "numeric" }),
  });
}
