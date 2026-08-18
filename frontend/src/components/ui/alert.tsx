import { cn } from "@/lib/utils";

type Tone = "error" | "success" | "info";

const TONES: Record<Tone, string> = {
  error: "border-destructive/40 bg-destructive/10 text-foreground",
  success: "border-success/40 bg-success/10 text-foreground",
  info: "border-border bg-muted text-foreground",
};

export function Alert({
  tone = "info",
  title,
  children,
  className,
}: {
  tone?: Tone;
  title?: string;
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      // Errors interrupt; confirmations wait for a pause. Announcing everything assertively
      // would talk over someone mid-form.
      role={tone === "error" ? "alert" : "status"}
      aria-live={tone === "error" ? "assertive" : "polite"}
      className={cn("rounded-md border px-3 py-2.5 text-sm", TONES[tone], className)}
    >
      {title ? <p className="font-medium">{title}</p> : null}
      {children ? <div className={cn(title && "mt-1")}>{children}</div> : null}
    </div>
  );
}
