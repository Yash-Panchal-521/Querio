import Link from "next/link";
import { Quote, ShieldCheck, Sparkles } from "lucide-react";
import { QuerioMark } from "@/components/brand/querio-mark";

/**
 * Split layout rather than a lone card on an empty page.
 *
 * The left panel does the work a marketing page would: it says what Querio is while someone
 * is deciding whether to hand over an email address. It collapses away below `lg`, where the
 * form is the only thing that matters and vertical space is scarce.
 */
export function AuthCard({
  title,
  subtitle,
  children,
  footer,
}: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
}) {
  return (
    <div className="grid min-h-full flex-1 lg:grid-cols-[1.1fr_1fr]">
      <BrandPanel />

      <main className="flex flex-col justify-center px-6 py-12 sm:px-10">
        <div className="mx-auto flex w-full max-w-sm flex-col gap-8">
          <Link href="/" className="flex items-center gap-2 lg:hidden">
            <QuerioMark className="size-6" />
            <span className="text-sm font-semibold tracking-tight">Querio</span>
          </Link>

          <div className="flex flex-col gap-2">
            <h1 className="text-3xl font-semibold tracking-tight text-balance">{title}</h1>
            {subtitle ? (
              <p className="text-muted-foreground text-sm text-pretty">{subtitle}</p>
            ) : null}
          </div>

          {children}

          {footer ? <div className="text-muted-foreground text-sm">{footer}</div> : null}
        </div>
      </main>
    </div>
  );
}

function BrandPanel() {
  return (
    <aside className="bg-muted/40 relative hidden flex-col justify-between overflow-hidden border-r px-10 py-12 lg:flex">
      {/* Two soft washes rather than a flat fill, so the panel has depth without an image to
          download or a gradient that fights the accent. */}
      <div
        aria-hidden="true"
        className="bg-primary/10 pointer-events-none absolute -top-32 -left-24 size-96 rounded-full blur-3xl"
      />
      <div
        aria-hidden="true"
        className="bg-citation/10 pointer-events-none absolute -right-24 -bottom-32 size-96 rounded-full blur-3xl"
      />

      <Link href="/" className="relative flex items-center gap-2">
        <QuerioMark className="size-7" />
        <span className="font-semibold tracking-tight">Querio</span>
      </Link>

      <div className="relative flex flex-col gap-8">
        <p className="max-w-sm text-2xl leading-snug font-medium tracking-tight text-balance">
          Ask your team&rsquo;s documents in plain language.
        </p>

        <ul className="flex flex-col gap-4">
          <Point icon={Quote} title="Cited, not asserted">
            Every answer links back to the passage it came from.
          </Point>
          <Point icon={Sparkles} title="Your knowledge, not the web">
            Answers are drawn only from what your team uploads.
          </Point>
          <Point icon={ShieldCheck} title="Separated at the database">
            One organization can never read another&rsquo;s content.
          </Point>
        </ul>
      </div>

      <p className="text-muted-foreground relative text-xs">
        Querio — AI knowledge assistant for teams.
      </p>
    </aside>
  );
}

function Point({
  icon: Icon,
  title,
  children,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  children: React.ReactNode;
}) {
  return (
    <li className="flex gap-3">
      <span className="bg-background/70 border-border flex size-8 shrink-0 items-center justify-center rounded-md border">
        <Icon className="text-primary size-4" />
      </span>
      <div className="flex flex-col gap-0.5">
        <span className="text-sm font-medium">{title}</span>
        <span className="text-muted-foreground max-w-xs text-sm text-pretty">{children}</span>
      </div>
    </li>
  );
}
