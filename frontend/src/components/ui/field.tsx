"use client";

import { useId, useState } from "react";
import { AlertCircle, Eye, EyeOff } from "lucide-react";
import { cn } from "@/lib/utils";

interface FieldProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, "id"> {
  label: string;
  /** Shown beneath the control and announced, because colour alone is not an error message. */
  error?: string | undefined;
  hint?: string | undefined;
  /** Leading affordance — an envelope on an email field, say. */
  icon?: React.ComponentType<{ className?: string }>;
}

export function Field({ label, error, hint, icon: Icon, className, type, ...props }: FieldProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;

  const [revealed, setRevealed] = useState(false);
  const isPassword = type === "password";

  // Typos in a masked field are invisible, and the alternative — a second "confirm" box —
  // is more work for the same reassurance.
  const resolvedType = isPassword && revealed ? "text" : type;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-foreground text-sm font-medium">
        {label}
      </label>

      <div className="relative">
        {Icon ? (
          <Icon className="text-muted-foreground pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2" />
        ) : null}

        <input
          id={id}
          type={resolvedType}
          aria-invalid={error ? true : undefined}
          // Points at whichever descriptions exist, so a screen reader hears the error rather
          // than just a red outline.
          aria-describedby={cn(error && errorId, hint && hintId) || undefined}
          className={cn(
            "border-input bg-background text-foreground h-10 w-full rounded-md border px-3 text-sm shadow-xs",
            "transition-[border-color,box-shadow] duration-150",
            "placeholder:text-muted-foreground/70",
            "focus-visible:border-ring focus-visible:ring-ring/25 focus-visible:ring-[3px] focus-visible:outline-none",
            "disabled:bg-muted/50 disabled:cursor-not-allowed disabled:opacity-70",
            Icon && "pl-9",
            isPassword && "pr-11",
            error &&
              "border-destructive focus-visible:border-destructive focus-visible:ring-destructive/25",
            className,
          )}
          {...props}
        />

        {isPassword ? (
          <button
            type="button"
            // The label says what it does rather than relying on the icon alone.
            aria-label={revealed ? "Hide password" : "Show password"}
            aria-pressed={revealed}
            disabled={props.disabled}
            onClick={() => setRevealed((value) => !value)}
            className={cn(
              "text-muted-foreground hover:text-foreground absolute inset-y-0 right-0 flex w-11 items-center justify-center",
              "focus-visible:ring-ring rounded-r-md focus-visible:ring-2 focus-visible:outline-none",
              "disabled:pointer-events-none disabled:opacity-60",
            )}
          >
            {revealed ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        ) : null}
      </div>

      {hint && !error ? (
        <p id={hintId} className="text-muted-foreground text-xs">
          {hint}
        </p>
      ) : null}

      {error ? (
        <p id={errorId} className="text-destructive flex items-center gap-1.5 text-xs">
          <AlertCircle className="size-3.5 shrink-0" />
          {error}
        </p>
      ) : null}
    </div>
  );
}
