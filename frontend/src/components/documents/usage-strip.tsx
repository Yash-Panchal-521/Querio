import type { TenantUsage } from "@/lib/api/documents";
import { formatBytes } from "@/lib/documents/presentation";
import { cn } from "@/lib/utils";

/**
 * What this organization has used, above the list rather than buried in settings.
 *
 * Both halves are shown — used and allowed. These free tiers are finite enough that people
 * will reach them, and a limit somebody only meets by hitting it is indistinguishable from a
 * bug.
 */
export function UsageStrip({ usage }: { usage: TenantUsage }) {
  const fraction = usage.maxStoredBytes > 0 ? usage.storedBytes / usage.maxStoredBytes : 0;
  const tight = fraction >= 0.9;

  return (
    <div className="border-border bg-muted flex items-center gap-5 rounded-lg border px-4 py-3">
      <div className="flex flex-1 flex-col gap-1.5">
        <div className="flex justify-between text-xs">
          <span className="text-muted-foreground">Storage</span>
          <span
            className={cn(
              "font-mono",
              tight ? "text-warning font-medium" : "text-muted-foreground",
            )}
          >
            {formatBytes(usage.storedBytes)} of {formatBytes(usage.maxStoredBytes)}
          </span>
        </div>
        <div
          className="bg-border h-1.5 overflow-hidden rounded-full"
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={usage.maxStoredBytes}
          aria-valuenow={usage.storedBytes}
          aria-label="Storage used"
        >
          <div
            className={cn("h-full rounded-full", tight ? "bg-warning" : "bg-primary")}
            // Always a sliver once anything is stored, so "some" never renders as "none".
            style={{ width: `${Math.max(fraction * 100, usage.storedBytes > 0 ? 2 : 0)}%` }}
          />
        </div>
      </div>

      <div className="bg-border h-7 w-px" aria-hidden />

      <div className="flex flex-col gap-0.5 text-right">
        <span className="font-mono text-sm font-medium">
          {usage.documentCount} of {usage.maxDocuments}
        </span>
        <span className="text-muted-foreground text-xs">documents</span>
      </div>
    </div>
  );
}
