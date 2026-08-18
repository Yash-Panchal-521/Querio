import type { Metadata } from "next";
import { ControlsPreview } from "./controls-preview";
import { ThemeToggle } from "@/components/theme-toggle";

// Internal reference surface for reviewing the token system, not a product page.
export const metadata: Metadata = {
  title: "Design tokens",
  robots: { index: false, follow: false },
};

interface SurfacePair {
  /** Utility pair, written out in full so Tailwind's source scanner finds them. */
  surface: string;
  text: string;
  label: string;
  /** Measured with WCAG 2.1 relative luminance; see the table in globals.css. */
  ratios: string;
  usage: string;
}

const SURFACES: SurfacePair[] = [
  {
    surface: "bg-background",
    text: "text-foreground",
    label: "background / foreground",
    ratios: "16.74 · 15.44",
    usage: "Page field and body copy.",
  },
  {
    surface: "bg-card",
    text: "text-card-foreground",
    label: "card / card-foreground",
    ratios: "17.33 · 14.26",
    usage: "Document rows, answer containers, settings panels.",
  },
  {
    surface: "bg-popover",
    text: "text-popover-foreground",
    label: "popover / popover-foreground",
    ratios: "17.33 · 13.58",
    usage: "Menus, command palette, citation hover cards.",
  },
  {
    surface: "bg-muted",
    text: "text-muted-foreground",
    label: "muted / muted-foreground",
    ratios: "6.04 · 6.34",
    usage: "Table headers, timestamps, token counts.",
  },
  {
    surface: "bg-secondary",
    text: "text-secondary-foreground",
    label: "secondary / secondary-foreground",
    ratios: "11.94 · 11.72",
    usage: "Secondary buttons, filter chips.",
  },
  {
    surface: "bg-accent",
    text: "text-accent-foreground",
    label: "accent / accent-foreground",
    ratios: "11.62 · 10.79",
    usage: "Hover and active surfaces — not the brand colour.",
  },
];

const FILLED: SurfacePair[] = [
  {
    surface: "bg-primary",
    text: "text-primary-foreground",
    label: "primary / primary-foreground",
    ratios: "7.16 · 7.87",
    usage: "Send, upload, invite — the one action per view.",
  },
  {
    surface: "bg-citation",
    text: "text-citation-foreground",
    label: "citation / citation-foreground",
    ratios: "6.32 · 8.93",
    usage: "Inline citation chips linking back to a source chunk.",
  },
  {
    surface: "bg-success",
    text: "text-success-foreground",
    label: "success / success-foreground",
    ratios: "6.22 · 8.39",
    usage: "Ingestion complete, index healthy.",
  },
  {
    surface: "bg-warning",
    text: "text-warning-foreground",
    label: "warning / warning-foreground",
    ratios: "5.93 · 9.04",
    usage: "Approaching quota, partial ingestion.",
  },
  {
    surface: "bg-destructive",
    text: "text-destructive-foreground",
    label: "destructive / destructive-foreground",
    ratios: "6.07 · 6.47",
    usage: "Delete document, revoke member.",
  },
];

interface InkToken {
  className: string;
  label: string;
  ratios: string;
}

// The same hues used as text rather than as a fill — how links and inline status
// actually render in a paragraph.
const INKS: InkToken[] = [
  { className: "text-primary", label: "primary", ratios: "7.22 · 8.07" },
  { className: "text-citation", label: "citation", ratios: "6.36 · 9.23" },
  { className: "text-success", label: "success", ratios: "6.26 · 8.69" },
  { className: "text-warning", label: "warning", ratios: "5.99 · 9.25" },
  { className: "text-destructive", label: "destructive", ratios: "6.13 · 6.59" },
  { className: "text-muted-foreground", label: "muted-foreground", ratios: "6.46 · 7.51" },
];

interface LineToken {
  label: string;
  className: string;
  ratios: string;
  usage: string;
}

const LINES: LineToken[] = [
  {
    label: "border",
    className: "border-border",
    ratios: "1.41 · 1.47",
    usage: "Dividers and card outlines. Decorative, so no 3:1 duty.",
  },
  {
    label: "input",
    className: "border-input",
    ratios: "3.06 · 3.37",
    usage: "Field outlines. Carries SC 1.4.11, so it is much darker than border.",
  },
  {
    label: "ring",
    className: "border-ring",
    ratios: "4.86 · 6.45",
    usage: "Focus indicator.",
  },
];

const TYPE_SCALE = [
  { className: "text-xs", label: "text-xs", note: "12px — trace ids, table meta" },
  { className: "text-sm", label: "text-sm", note: "14px — UI chrome, labels" },
  { className: "text-base", label: "text-base", note: "16px / 1.65 — default copy" },
  { className: "text-reading", label: "text-reading", note: "17px / 1.75 — answers, excerpts" },
  { className: "text-lg", label: "text-lg", note: "18px / 1.6 — lead paragraphs" },
  { className: "text-xl", label: "text-xl", note: "20px — section headings" },
  { className: "text-2xl", label: "text-2xl", note: "24px / 1.3" },
  { className: "text-3xl", label: "text-3xl", note: "30px / 1.2" },
  { className: "text-4xl", label: "text-4xl", note: "36px / 1.15 — page titles" },
] as const;

const RADII = [
  { className: "rounded-sm", label: "rounded-sm", note: "radius − 4px — chips, badges" },
  { className: "rounded-md", label: "rounded-md", note: "radius − 2px — inputs, buttons" },
  { className: "rounded-lg", label: "rounded-lg", note: "radius (10px) — cards" },
  { className: "rounded-xl", label: "rounded-xl", note: "radius + 4px — modals, panels" },
  { className: "rounded-full", label: "rounded-full", note: "avatars, status dots" },
] as const;

function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="text-muted-foreground mb-3 font-mono text-xs tracking-wider uppercase">
      {children}
    </h3>
  );
}

function SurfaceRow({ pair }: { pair: SurfacePair }) {
  return (
    <div className={`${pair.surface} border-border rounded-md border p-3`}>
      <div className={`${pair.text} flex items-baseline justify-between gap-3`}>
        <span className="font-mono text-xs">{pair.label}</span>
        <span className="font-mono text-xs opacity-70">{pair.ratios}</span>
      </div>
      <p className={`${pair.text} mt-1 text-sm opacity-80`}>{pair.usage}</p>
    </div>
  );
}

/**
 * One complete rendering of the palette, pinned to a theme by class rather than
 * by the active preference — which is what lets both appear at once. Everything
 * inside must be expressed through tokens; a `dark:` utility would resolve
 * against the document root, not this wrapper, and read the wrong theme.
 */
function ThemePanel({ theme }: { theme: "light" | "dark" }) {
  return (
    <div className={`${theme} bg-background border-border flex-1 rounded-xl border p-5`}>
      <h2 className="text-foreground mb-5 font-mono text-sm font-semibold">
        {theme} — contrast shown as light · dark
      </h2>

      <SectionHeading>Surfaces</SectionHeading>
      <div className="mb-6 flex flex-col gap-2">
        {SURFACES.map((pair) => (
          <SurfaceRow key={pair.label} pair={pair} />
        ))}
      </div>

      <SectionHeading>Filled</SectionHeading>
      <div className="mb-6 flex flex-col gap-2">
        {FILLED.map((pair) => (
          <SurfaceRow key={pair.label} pair={pair} />
        ))}
      </div>

      <SectionHeading>Ink on background</SectionHeading>
      <ul className="mb-6 flex flex-col gap-1.5">
        {INKS.map((ink) => (
          <li key={ink.label} className="flex items-baseline justify-between gap-3">
            <span className={`${ink.className} text-sm font-medium`}>
              {ink.label} — the quick brown fox
            </span>
            <span className="text-muted-foreground font-mono text-xs">{ink.ratios}</span>
          </li>
        ))}
      </ul>

      <SectionHeading>Source highlight</SectionHeading>
      <div className="mb-6">
        <p className="text-foreground text-sm">
          A grounded answer quotes its source inline:{" "}
          <mark className="bg-citation-subtle text-foreground rounded-sm px-1 py-0.5">
            the retrieved passage is washed in citation-subtle
          </mark>{" "}
          and tagged
          <span className="bg-citation text-citation-foreground ml-1 rounded-sm px-1.5 py-0.5 font-mono text-xs">
            §4.2
          </span>
          .
        </p>
        <p className="text-muted-foreground mt-2 font-mono text-xs">
          foreground on citation-subtle — 15.13 · 10.90
        </p>
      </div>

      <SectionHeading>Lines</SectionHeading>
      <div className="flex flex-col gap-2">
        {LINES.map((line) => (
          <div key={line.label} className={`${line.className} rounded-md border-2 p-2.5`}>
            <div className="flex items-baseline justify-between gap-3">
              <span className="text-foreground font-mono text-xs">{line.label}</span>
              <span className="text-muted-foreground font-mono text-xs">{line.ratios}</span>
            </div>
            <p className="text-muted-foreground mt-1 text-xs">{line.usage}</p>
          </div>
        ))}
      </div>
    </div>
  );
}

export default function DesignTokensPage() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-10 px-6 py-12">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-semibold tracking-tight">Design tokens</h1>
          <p className="text-muted-foreground max-w-reading mt-1.5 text-base">
            Every colour token in both themes, with the measured WCAG 2.1 contrast ratio for each
            pair. AA needs 4.5:1 for body text and 3:1 for focus and field outlines.
          </p>
        </div>
        <ThemeToggle />
      </header>

      <section aria-labelledby="palette-heading">
        <h2 id="palette-heading" className="sr-only">
          Colour palette
        </h2>
        <div className="flex flex-col gap-5 lg:flex-row">
          <ThemePanel theme="light" />
          <ThemePanel theme="dark" />
        </div>
      </section>

      <section aria-labelledby="type-heading" className="border-border rounded-xl border p-5">
        <h2 id="type-heading" className="mb-5 text-xl font-semibold">
          Type scale
        </h2>
        <div className="flex flex-col gap-4">
          {TYPE_SCALE.map((step) => (
            <div key={step.label} className="flex flex-col gap-1">
              <div className="text-muted-foreground flex items-baseline gap-3 font-mono text-xs">
                <span>{step.label}</span>
                <span className="opacity-70">{step.note}</span>
              </div>
              <p className={`${step.className} max-w-reading`}>
                Querio answers from your own documents, with every claim traceable to the passage it
                came from.
              </p>
            </div>
          ))}
        </div>
        <p className="text-muted-foreground mt-6 font-mono text-xs">
          max-w-reading — 68ch, the measure cap for prose columns
        </p>
      </section>

      <section aria-labelledby="controls-heading" className="border-border rounded-xl border p-5">
        <SectionHeading>
          <span id="controls-heading">Controls</span>
        </SectionHeading>
        <ControlsPreview />
      </section>

      <section aria-labelledby="radius-heading" className="border-border rounded-xl border p-5">
        <h2 id="radius-heading" className="mb-5 text-xl font-semibold">
          Radii
        </h2>
        <div className="flex flex-wrap gap-5">
          {RADII.map((radius) => (
            <div key={radius.label} className="flex flex-col gap-2">
              <div className={`${radius.className} bg-accent border-input size-20 border-2`} />
              <div className="text-muted-foreground font-mono text-xs">
                <div className="text-foreground">{radius.label}</div>
                <div>{radius.note}</div>
              </div>
            </div>
          ))}
        </div>
        <p className="text-muted-foreground mt-6 font-mono text-xs">
          All derived from --radius (0.625rem), the token shadcn components read.
        </p>
      </section>
    </main>
  );
}
