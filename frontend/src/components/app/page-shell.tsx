import { cn } from "@/lib/utils";

/**
 * One page rhythm, applied everywhere.
 *
 * Previously each screen invented its own max-width, padding and heading size, which is why
 * they read as unrelated boxes rather than one product.
 */
export function Page({
  children,
  width = "wide",
}: {
  children: React.ReactNode;
  width?: "wide" | "narrow";
}) {
  return (
    <main
      className={cn(
        "mx-auto flex w-full flex-1 flex-col gap-8 px-6 py-10",
        width === "wide" ? "max-w-4xl" : "max-w-xl",
      )}
    >
      {children}
    </main>
  );
}

export function PageHeader({
  title,
  description,
  actions,
  eyebrow,
}: {
  title: string;
  description?: string;
  actions?: React.ReactNode;
  eyebrow?: string;
}) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div className="flex min-w-0 flex-col gap-1">
        {eyebrow ? (
          <span className="text-muted-foreground text-xs font-medium tracking-wide uppercase">
            {eyebrow}
          </span>
        ) : null}
        <h1 className="truncate text-2xl font-semibold tracking-tight">{title}</h1>
        {description ? (
          <p className="text-muted-foreground max-w-prose text-sm text-pretty">{description}</p>
        ) : null}
      </div>

      {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
    </div>
  );
}

export function Card({
  title,
  description,
  actions,
  children,
  tone = "default",
  className,
}: {
  title?: string;
  description?: string;
  actions?: React.ReactNode;
  children?: React.ReactNode;
  tone?: "default" | "danger";
  className?: string;
}) {
  return (
    <section
      className={cn(
        "bg-card flex flex-col rounded-xl border shadow-xs",
        tone === "danger" ? "border-destructive/30" : "border-border",
        className,
      )}
    >
      {title ? (
        <header className="flex flex-wrap items-start justify-between gap-3 px-5 pt-5 pb-4">
          <div className="flex min-w-0 flex-col gap-0.5">
            <h2 className={cn("text-sm font-medium", tone === "danger" && "text-destructive")}>
              {title}
            </h2>
            {description ? (
              <p className="text-muted-foreground max-w-prose text-xs text-pretty">{description}</p>
            ) : null}
          </div>
          {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
        </header>
      ) : null}

      <div className={cn("flex flex-col gap-4 px-5 pb-5", !title && "pt-5")}>{children}</div>
    </section>
  );
}

/**
 * An empty list is a moment to explain what goes here, not a blank rectangle — it is often
 * the first thing someone sees after signing up.
 */
export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
}: {
  icon: React.ComponentType<{ className?: string }>;
  title: string;
  description: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-10 text-center">
      <span className="bg-muted text-muted-foreground flex size-11 items-center justify-center rounded-full">
        <Icon className="size-5" />
      </span>
      <div className="flex flex-col gap-1">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-muted-foreground mx-auto max-w-sm text-sm text-pretty">{description}</p>
      </div>
      {action ? <div className="mt-1">{action}</div> : null}
    </div>
  );
}
