"use client";

import { useCallback, useRef, useState } from "react";
import { Upload } from "lucide-react";
import { cn } from "@/lib/utils";
import { toApiMessage } from "@/lib/api/api-messages";
import { uploadDocument } from "@/lib/api/documents";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";

/** What the server accepts. Offered to the file picker as a hint, never trusted as the check. */
const ACCEPTED = ".pdf,.docx,.md,.markdown,.txt,.text";

/**
 * Mirrors DocumentLimits.MaxFileBytes. Checked here as well as there, and not as an
 * optimisation: a body over the server's limit is aborted mid-upload, and that response carries
 * no CORS headers — so the browser's fetch rejects rather than reading the refusal, and the
 * person is told we cannot be reached when in fact we answered. Refusing before sending is the
 * only way the real reason survives.
 */
const MAX_FILE_BYTES = 20 * 1024 * 1024;

function describeSize(bytes: number): string {
  const megabytes = bytes / 1024 / 1024;

  return megabytes >= 10 ? `${Math.round(megabytes)} MB` : `${megabytes.toFixed(1)} MB`;
}

interface UploadPanelProps {
  tenantId: string;
  onUploaded: () => Promise<void> | void;
  /** The empty state is itself the drop target, so it renders larger and explains more. */
  variant?: "empty" | "compact";
}

/**
 * Upload, by drop or by picker.
 *
 * The drop target is the whole panel rather than a strip inside it: somebody dragging a file
 * aims at the region they think of as "the documents", and a narrow zone quietly rejects the
 * throw.
 */
export function UploadPanel({ tenantId, onUploaded, variant = "compact" }: UploadPanelProps) {
  const { showToast } = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragging, setDragging] = useState(false);
  const [busy, setBusy] = useState(false);

  // Drag events fire on every child element too, so a plain boolean flickers as the pointer
  // crosses inner nodes. Counting enter and leave is what keeps the state honest.
  const depth = useRef(0);

  const clearDrag = useCallback(() => {
    depth.current = 0;
    setDragging(false);
  }, []);

  const send = useCallback(
    async (files: FileList | null) => {
      if (!files || files.length === 0) {
        return;
      }

      const chosen = Array.from(files);
      const tooLarge = chosen.filter((file) => file.size > MAX_FILE_BYTES);

      for (const file of tooLarge) {
        showToast(
          `${file.name} is ${describeSize(file.size)}. The limit is ${describeSize(MAX_FILE_BYTES)} — split it or upload a smaller version.`,
          "error",
        );
      }

      const sendable = chosen.filter((file) => file.size <= MAX_FILE_BYTES);

      if (sendable.length === 0) {
        return;
      }

      setBusy(true);

      try {
        // One at a time, deliberately: each upload is buffered and hashed server-side, and a
        // browser firing six at once is how a small instance runs out of memory.
        for (const file of sendable) {
          const { document, alreadyExisted } = await uploadDocument(tenantId, file);

          showToast(
            alreadyExisted
              ? `${document.fileName} is already here — nothing was duplicated.`
              : `${document.fileName} added. Processing it now.`,
            alreadyExisted ? "info" : "success",
          );
        }

        await onUploaded();
      } catch (caught) {
        showToast(toApiMessage(caught), "error");
      } finally {
        setBusy(false);
        clearDrag();
      }
    },
    [tenantId, onUploaded, showToast, clearDrag],
  );

  const empty = variant === "empty";

  return (
    <div
      onDragEnter={(event) => {
        event.preventDefault();
        depth.current += 1;
        setDragging(true);
      }}
      onDragOver={(event) => event.preventDefault()}
      onDragLeave={(event) => {
        event.preventDefault();
        depth.current -= 1;

        if (depth.current <= 0) {
          clearDrag();
        }
      }}
      onDrop={(event) => {
        event.preventDefault();

        // Cleared here rather than only after an upload finishes. Dropping something that is
        // not a file at all — selected text, a link — leaves `files` empty, and every path
        // that returned early from there skipped the reset and left the panel lit up with
        // nothing in flight to explain it.
        clearDrag();

        void send(event.dataTransfer.files);
      }}
      className={cn(
        "flex flex-col items-center gap-3 rounded-xl border-2 border-dashed text-center transition-colors",
        empty ? "px-8 py-16" : "px-6 py-8",
        dragging ? "border-primary bg-accent" : "border-input bg-card",
      )}
    >
      <span
        className={cn(
          "flex items-center justify-center rounded-full transition-colors",
          empty ? "size-13" : "size-11",
          dragging ? "text-primary bg-card shadow-sm" : "bg-accent text-primary",
        )}
      >
        <Upload className={empty ? "size-6" : "size-5"} aria-hidden />
      </span>

      <div className="flex flex-col gap-1.5">
        <p className={cn("font-medium", empty ? "text-base" : "text-sm")}>
          {dragging ? "Drop to upload" : empty ? "Add the first document" : "Add a document"}
        </p>
        <p className="text-muted-foreground max-w-sm text-sm text-pretty">
          {dragging
            ? "PDF, Word, Markdown or plain text"
            : "Drop a file here, or choose one. Everyone in this organization will be able to ask questions of it."}
        </p>
      </div>

      <Button
        type="button"
        size={empty ? "md" : "sm"}
        loading={busy}
        onClick={() => inputRef.current?.click()}
        className="mt-1"
      >
        Choose a file
      </Button>

      <p className="text-muted-foreground font-mono text-xs">
        PDF · DOCX · Markdown · plain text — up to {describeSize(MAX_FILE_BYTES)}
      </p>

      <input
        ref={inputRef}
        type="file"
        accept={ACCEPTED}
        multiple
        className="hidden"
        onChange={(event) => {
          void send(event.target.files);
          // Cleared so choosing the same file twice still fires a change event — otherwise a
          // failed upload cannot be retried from the picker.
          event.target.value = "";
        }}
      />
    </div>
  );
}
