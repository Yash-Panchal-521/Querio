"use client";

import { useCallback, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { Check, FileText, MailCheck, PartyPopper, Users, X } from "lucide-react";
import { listInvitations } from "@/lib/api/invitations";
import type { Organization } from "@/lib/api/me";
import { Button } from "@/components/ui/button";
import { dismissOnboarding, isOnboardingPending } from "@/lib/onboarding";
import { useAsyncData } from "@/lib/use-async-data";
import { useSession } from "@/lib/auth/session-context";
import { cn } from "@/lib/utils";

interface Step {
  id: string;
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  done: boolean;
  href?: string;
  action?: string;
  soon?: boolean;
}

/**
 * Shown once, after someone creates their first organization.
 *
 * Every step reports its own state rather than being ticked as the person walks through, so
 * anything already done by another path — verifying an email before creating the
 * organization, say — arrives already complete. Being told to do something you have already
 * done is worse than no checklist at all.
 */
const subscribeToNothing = () => () => {};

export function SetupChecklist({ organization }: { organization: Organization }) {
  const { session } = useSession();
  const [dismissed, setDismissed] = useState(false);

  const load = useCallback(() => listInvitations(organization.id), [organization.id]);
  const { data: invitations } = useAsyncData(load);

  // localStorage cannot be read while rendering on the server, and reading it during the
  // first client render would disagree with the server's markup. Asking "am I hydrated" as
  // a store read settles that without an effect.
  const hydrated = useSyncExternalStore(
    subscribeToNothing,
    () => true,
    () => false,
  );

  if (!hydrated || session.status !== "ready" || dismissed) {
    return null;
  }

  if (!isOnboardingPending(organization.id)) {
    return null;
  }

  const emailVerified = session.profile.emailVerified;

  // Either an invitation is outstanding or somebody has already joined — both mean the
  // person has done the thing this step is asking for.
  const invitedSomeone = (invitations?.length ?? 0) > 0 || organization.memberCount > 1;

  const steps: Step[] = [
    {
      id: "create",
      label: "Create your organization",
      description: `${organization.name} is ready.`,
      icon: Check,
      done: true,
    },
    {
      id: "verify",
      label: "Verify your email address",
      description: "Confirms the address invitations are matched against.",
      icon: MailCheck,
      done: emailVerified,
      href: "/account",
      action: "Verify",
    },
    {
      id: "invite",
      label: "Invite a teammate",
      description: "Everyone you invite can ask questions of the same documents.",
      icon: Users,
      done: invitedSomeone,
      href: `/orgs/${organization.id}/members`,
      action: "Invite",
    },
    {
      id: "upload",
      label: "Upload a document",
      description: "Answers are drawn only from what you upload.",
      icon: FileText,
      done: false,
      soon: true,
    },
  ];

  const actionable = steps.filter((step) => !step.soon);
  const completed = actionable.filter((step) => step.done).length;
  const allDone = completed === actionable.length;

  function dismiss() {
    dismissOnboarding(organization.id);
    setDismissed(true);
  }

  return (
    <section className="border-border bg-card relative flex flex-col gap-4 rounded-xl border p-5 shadow-xs">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <h2 className="flex items-center gap-2 text-sm font-medium">
            {allDone ? <PartyPopper className="text-success size-4" /> : null}
            {allDone ? "You're all set" : "Get set up"}
          </h2>
          <p className="text-muted-foreground text-xs">
            {allDone
              ? "Nothing left to do here. Uploading documents arrives with the next release."
              : `${completed} of ${actionable.length} done — a couple of minutes at most.`}
          </p>
        </div>

        <Button variant="ghost" size="sm" onClick={dismiss} aria-label="Dismiss setup checklist">
          <X />
        </Button>
      </div>

      {/* A plain bar rather than badges or confetti: this is a work tool, and progress is
          the only thing the bar needs to say. */}
      <div className="bg-muted h-1.5 overflow-hidden rounded-full" aria-hidden="true">
        <div
          className="bg-primary h-full rounded-full transition-[width] duration-500"
          style={{ width: `${(completed / actionable.length) * 100}%` }}
        />
      </div>

      <ul className="flex flex-col gap-1">
        {steps.map((step) => (
          <li
            key={step.id}
            className={cn(
              "flex flex-wrap items-center gap-3 rounded-md px-2 py-2",
              step.soon && "opacity-55",
            )}
          >
            <span
              className={cn(
                "flex size-6 shrink-0 items-center justify-center rounded-full border",
                step.done
                  ? "border-success/40 bg-success/15 text-success"
                  : "border-border text-muted-foreground",
              )}
            >
              {step.done ? <Check className="size-3.5" /> : <step.icon className="size-3.5" />}
            </span>

            <div className="flex min-w-0 flex-1 flex-col">
              <span
                className={cn(
                  "text-sm",
                  step.done ? "text-muted-foreground line-through" : "font-medium",
                )}
              >
                {step.label}
              </span>
              <span className="text-muted-foreground text-xs text-pretty">{step.description}</span>
            </div>

            {step.soon ? (
              <span className="border-border text-muted-foreground rounded border px-1.5 py-0.5 text-[10px] tracking-wide uppercase">
                Soon
              </span>
            ) : null}

            {!step.done && step.href && step.action ? (
              <Link href={step.href} className="text-primary text-sm font-medium hover:underline">
                {step.action}
              </Link>
            ) : null}
          </li>
        ))}
      </ul>

      {allDone ? (
        <Button variant="secondary" size="sm" onClick={dismiss} className="self-start">
          Hide this
        </Button>
      ) : null}
    </section>
  );
}
