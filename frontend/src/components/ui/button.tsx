import { Loader2 } from "lucide-react";
import { cn } from "@/lib/utils";

type Variant = "primary" | "secondary" | "outline" | "ghost" | "destructive";
type Size = "sm" | "md" | "lg";

interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  /** Renders a busy state and blocks repeat submits. */
  loading?: boolean;
}

const VARIANTS: Record<Variant, string> = {
  // A hairline of the accent's own colour instead of a hard edge, so the button reads as
  // raised rather than pasted on.
  primary:
    "bg-primary text-primary-foreground shadow-sm ring-1 ring-primary/20 hover:bg-primary/90 active:bg-primary/95",
  secondary: "bg-background text-foreground border border-border shadow-xs hover:bg-accent",
  // Alias kept because shadcn-generated components ask for "outline" by name.
  outline: "bg-background text-foreground border border-border shadow-xs hover:bg-accent",
  ghost: "text-muted-foreground hover:text-foreground hover:bg-accent",
  destructive:
    "bg-destructive text-destructive-foreground shadow-sm ring-1 ring-destructive/20 hover:bg-destructive/90",
};

const SIZES: Record<Size, string> = {
  sm: "h-8 gap-1.5 rounded-md px-2.5 text-xs",
  md: "h-10 gap-2 rounded-md px-4 text-sm",
  lg: "h-11 gap-2 rounded-lg px-5 text-sm",
};

/**
 * Shared so a link can look like a button without a Slot indirection — navigation should
 * stay an anchor for middle-click, open-in-new-tab and the status bar preview.
 */
export function buttonClasses({
  variant = "primary",
  size = "md",
  className,
}: {
  variant?: Variant;
  size?: Size;
  className?: string;
} = {}): string {
  return cn(
    "inline-flex shrink-0 items-center justify-center font-medium whitespace-nowrap",
    "transition-[background-color,box-shadow,color] duration-150",
    "focus-visible:ring-ring focus-visible:ring-offset-background focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none",
    "disabled:pointer-events-none disabled:opacity-50",
    "[&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
    SIZES[size],
    VARIANTS[variant],
    className,
  );
}

export function Button({
  className,
  variant = "primary",
  size = "md",
  loading = false,
  disabled,
  children,
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      // Disabling while busy is what actually prevents a double submit; aria-busy only
      // announces it.
      disabled={disabled || loading}
      aria-busy={loading}
      className={buttonClasses({ variant, size, className })}
      {...props}
    >
      {loading ? <Loader2 className="animate-spin motion-reduce:animate-none" /> : null}
      {children}
    </button>
  );
}
