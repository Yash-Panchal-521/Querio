"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowRight, Building2, FileText, Mail, MailCheck, MessageSquareQuote } from "lucide-react";
import { toApiMessage, toFieldErrors } from "@/lib/api/api-messages";
import { createOrganization } from "@/lib/api/tenants";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { QuerioMark } from "@/components/brand/querio-mark";
import { markOnboardingPending } from "@/lib/onboarding";
import { useOrganizations } from "@/lib/auth/use-organizations";
import { useSession } from "@/lib/auth/session-context";
import { useToast } from "@/components/ui/toast";

/**
 * Deliberately outside the app shell when this is someone's first organization.
 *
 * With no organization there is nothing to navigate to, and the sidebar would be a column of
 * disabled links — navigation chrome pointing nowhere. An empty state is the most valuable
 * onboarding surface a product has, so this one carries a single obvious action and a
 * preview of what follows, rather than reading as a void.
 */
export function CreateOrganizationForm() {
  const { session, refresh } = useSession();
  const { organizations } = useOrganizations();
  const { showToast } = useToast();
  const router = useRouter();

  const [name, setName] = useState("");
  const [fieldError, setFieldError] = useState<string | undefined>(undefined);
  const [pending, setPending] = useState(false);

  if (session.status !== "ready") {
    return null;
  }

  const { profile } = session;
  const emailVerified = profile.emailVerified;
  const isFirst = organizations.length === 0;
  const firstName = profile.displayName?.split(" ")[0];

  async function submit() {
    setFieldError(undefined);
    setPending(true);

    try {
      const organization = await createOrganization(name);

      // Only a first organization earns the setup checklist; someone creating their third
      // does not need to be onboarded again.
      if (isFirst) {
        markOnboardingPending(organization.id);
      }

      // The switcher reads from the session, so it has to know about the new organization
      // before we navigate into it.
      await refresh();

      router.replace(`/orgs/${organization.id}`);
    } catch (caught) {
      setFieldError(toFieldErrors(caught).name);
      showToast(toApiMessage(caught), "error");
      setPending(false);
    }
  }

  return (
    <main className="relative flex flex-1 flex-col items-center justify-center overflow-hidden px-6 py-12">
      <div
        aria-hidden="true"
        className="bg-primary/10 pointer-events-none absolute -top-40 left-1/2 size-[36rem] -translate-x-1/2 rounded-full blur-3xl"
      />

      <div className="relative flex w-full max-w-md flex-col gap-8">
        <div className="flex flex-col items-center gap-4 text-center">
          <QuerioMark className="size-9" />
          <div className="flex flex-col gap-2">
            <h1 className="text-3xl font-semibold tracking-tight text-balance">
              {isFirst
                ? firstName
                  ? `Welcome, ${firstName}`
                  : "Welcome to Querio"
                : "Create another organization"}
            </h1>
            <p className="text-muted-foreground text-sm text-pretty">
              {isFirst
                ? "Start by creating an organization. It holds your team's documents and keeps them separate from everyone else's."
                : "A second organization keeps its documents entirely separate from your first."}
            </p>
          </div>
        </div>

        {emailVerified ? null : (
          <Alert tone="info" title="Verify your email first">
            <p>
              Invitations are matched by email address, so an organization can only be created from
              a confirmed one.
            </p>
            <Link
              href="/account"
              className="text-primary mt-2 inline-flex items-center gap-1 font-medium"
            >
              <MailCheck className="size-3.5" />
              Go to your account
            </Link>
          </Alert>
        )}

        <form
          noValidate
          className="border-border bg-card flex flex-col gap-4 rounded-xl border p-6 shadow-xs"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <Field
            label="Organization name"
            name="name"
            icon={Building2}
            required
            autoFocus
            placeholder="Acme Inc."
            // One field, deliberately. Every extra field measurably costs completions, and
            // nothing else — industry, size, country — is needed until billing exists.
            hint="Usually your company or team name."
            error={fieldError}
            value={name}
            onChange={(event) => setName(event.target.value)}
            disabled={!emailVerified}
          />

          <Button type="submit" size="lg" loading={pending} disabled={!emailVerified}>
            {isFirst ? "Create and continue" : "Create organization"}
            <ArrowRight />
          </Button>
        </form>

        {isFirst ? (
          <>
            <ol className="text-muted-foreground flex flex-col gap-2.5 text-sm">
              <Step index={1} icon={Building2}>
                Create your organization
              </Step>
              <Step index={2} icon={FileText}>
                Upload the documents your team relies on
              </Step>
              <Step index={3} icon={MessageSquareQuote}>
                Ask questions and get answers with citations
              </Step>
            </ol>

            {/* Someone who arrived expecting an invitation must not hit a dead end here. */}
            <p className="text-muted-foreground border-border border-t pt-6 text-center text-sm">
              <Mail className="mr-1.5 inline size-3.5 align-[-2px]" />
              Waiting to be invited? Ask a teammate to send you an invitation link — you do not need
              your own organization to join theirs.
            </p>
          </>
        ) : (
          <Link href="/orgs" className="text-muted-foreground text-center text-sm hover:underline">
            Back
          </Link>
        )}
      </div>
    </main>
  );
}

function Step({
  index,
  icon: Icon,
  children,
}: {
  index: number;
  icon: React.ComponentType<{ className?: string }>;
  children: React.ReactNode;
}) {
  return (
    <li className="flex items-center gap-3">
      <span className="border-border text-muted-foreground flex size-6 shrink-0 items-center justify-center rounded-full border text-xs font-medium">
        {index}
      </span>
      <Icon className="size-4 shrink-0" />
      <span className="text-pretty">{children}</span>
    </li>
  );
}
