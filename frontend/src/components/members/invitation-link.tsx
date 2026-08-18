"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";

/**
 * Shown once, immediately after an invitation is issued.
 *
 * The API keeps only a hash of the token, so this link cannot be recovered later — which is
 * the point, and why the wording says so rather than letting someone assume they can come
 * back for it.
 */
export function InvitationLink({ email, link }: { email: string; link: string }) {
  const { showToast } = useToast();
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(link);

      setCopied(true);
      showToast("Invitation link copied.", "success");
    } catch {
      // Clipboard access is refused outside a secure context, and on a shared machine the
      // permission may simply be denied. Selecting the text still works.
      showToast("Could not copy automatically. Select the link and copy it.", "error");
    }
  }

  return (
    <div className="border-citation/40 bg-citation-subtle flex flex-col gap-2 rounded-md border p-3">
      <p className="text-sm font-medium">Send this link to {email}</p>

      <div className="flex flex-wrap items-center gap-2">
        <input
          readOnly
          value={link}
          aria-label="Invitation link"
          onFocus={(event) => event.currentTarget.select()}
          className="border-border bg-background text-foreground h-9 min-w-0 flex-1 rounded-md border px-2 font-mono text-xs"
        />
        <Button variant="secondary" onClick={() => void copy()}>
          {copied ? "Copied" : "Copy"}
        </Button>
      </div>

      <p className="text-muted-foreground text-xs">
        This is the only time the link is shown. If it is lost, revoke the invitation and send a new
        one.
      </p>
    </div>
  );
}
