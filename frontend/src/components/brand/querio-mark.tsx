import { cn } from "@/lib/utils";

/**
 * The product mark: a magnifier whose lens holds a page.
 *
 * Querio is search over documents, so the two ideas are drawn as one shape rather than an
 * arbitrary glyph. Uses currentColor throughout, so it inherits whatever it sits on and
 * needs no separate dark-mode asset.
 */
export function QuerioMark({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      className={cn("text-primary", className)}
    >
      <circle cx="10.5" cy="10.5" r="7.5" stroke="currentColor" strokeWidth="1.8" />
      <path
        d="M7.5 8h6M7.5 11h6M7.5 14h3.5"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinecap="round"
      />
      <path d="m16.2 16.2 4.3 4.3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
