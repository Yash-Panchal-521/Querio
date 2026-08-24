import { Check } from "lucide-react";
import { cn } from "@/lib/utils";
import type { DocumentStatus } from "@/lib/api/documents";
import { describeStatus, type StatusTone } from "@/lib/documents/presentation";

/**
 * Paused is amber rather than red, and that is the whole point of having four tones instead
 * of three. When the daily embedding allowance runs out nothing is wrong and there is nothing
 * to do — dressing it as a failure invites a re-upload that spends the same allowance again
 * the moment it returns.
 */
const TONES: Record<StatusTone, string> = {
  working: "text-primary bg-accent",
  paused: "text-warning bg-warning/10",
  ready: "text-success bg-success/10",
  failed: "text-destructive bg-destructive/10",
};

export function StatusPill({ status, className }: { status: DocumentStatus; className?: string }) {
  const { label, tone } = describeStatus(status);

  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium",
        TONES[tone],
        className,
      )}
    >
      {tone === "ready" ? <Check className="size-3.5" aria-hidden /> : null}
      {label}
    </span>
  );
}
