"use client";

import Link from "next/link";
import { ArrowRight, Quote, ShieldCheck, Sparkles } from "lucide-react";
import { buttonClasses } from "@/components/ui/button";
import { QuerioMark } from "@/components/brand/querio-mark";
import { ThemeToggle } from "@/components/theme-toggle";
import { useSession } from "@/lib/auth/session-context";

export function LandingHero() {
  const { session } = useSession();
  const signedIn = session.status === "ready" || session.status === "provisioning";

  return (
    <div className="relative flex min-h-full flex-1 flex-col overflow-hidden">
      {/* Same two washes as the auth panel, so the product looks like one thing across the
          signed-out surfaces. */}
      <div
        aria-hidden="true"
        className="bg-primary/10 pointer-events-none absolute -top-40 -left-32 size-[32rem] rounded-full blur-3xl"
      />
      <div
        aria-hidden="true"
        className="bg-citation/10 pointer-events-none absolute -right-32 -bottom-40 size-[32rem] rounded-full blur-3xl"
      />

      <header className="relative flex items-center justify-between gap-4 px-6 py-5">
        <span className="flex items-center gap-2">
          <QuerioMark className="size-6" />
          <span className="font-semibold tracking-tight">Querio</span>
        </span>
        <div className="flex items-center gap-2">
          <ThemeToggle />
        </div>
      </header>

      <main className="relative mx-auto flex w-full max-w-3xl flex-1 flex-col justify-center gap-10 px-6 py-16">
        <div className="flex flex-col gap-5">
          <span className="border-border bg-background/60 text-muted-foreground w-fit rounded-full border px-3 py-1 text-xs backdrop-blur">
            Retrieval-augmented answers for teams
          </span>

          <h1 className="text-4xl font-semibold tracking-tight text-balance sm:text-5xl">
            Answers grounded in your own documents
          </h1>

          <p className="text-muted-foreground max-w-prose text-lg text-pretty">
            Upload what your team already knows, then ask in plain language. Every answer cites the
            passage it came from, so you can check it rather than trust it.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-3">
          {/* Rendered from session state rather than a redirect, so someone already signed in
              is offered the app instead of being asked to sign in again. */}
          {signedIn ? (
            <Link href="/orgs" className={buttonClasses({ size: "lg" })}>
              Open Querio
              <ArrowRight />
            </Link>
          ) : (
            <>
              <Link href="/sign-up" className={buttonClasses({ size: "lg" })}>
                Get started
                <ArrowRight />
              </Link>
              <Link href="/sign-in" className={buttonClasses({ variant: "secondary", size: "lg" })}>
                Sign in
              </Link>
            </>
          )}
        </div>

        <dl className="border-border grid gap-8 border-t pt-10 sm:grid-cols-3">
          <Feature
            icon={Quote}
            term="Cited, not asserted"
            description="Each answer links back to the exact passage, so nothing has to be taken on faith."
          />
          <Feature
            icon={Sparkles}
            term="Your team's knowledge"
            description="Answers come from the documents you upload, never from the open web."
          />
          <Feature
            icon={ShieldCheck}
            term="Separate by design"
            description="Every organization's content is isolated at the database, not by convention."
          />
        </dl>
      </main>
    </div>
  );
}

function Feature({
  icon: Icon,
  term,
  description,
}: {
  icon: React.ComponentType<{ className?: string }>;
  term: string;
  description: string;
}) {
  return (
    <div className="flex flex-col gap-2">
      <span className="bg-primary/10 text-primary flex size-8 items-center justify-center rounded-lg">
        <Icon className="size-4" />
      </span>
      <dt className="text-sm font-medium">{term}</dt>
      <dd className="text-muted-foreground text-sm text-pretty">{description}</dd>
    </div>
  );
}
