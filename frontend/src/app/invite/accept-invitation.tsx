"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { toApiMessage } from "@/lib/api/api-messages";
import { acceptInvitation, previewInvitation, type InvitationPreview } from "@/lib/api/invitations";
import { Alert } from "@/components/ui/alert";
import { AuthCard } from "@/components/auth/auth-card";
import { Button } from "@/components/ui/button";
import { signOutOfQuerio } from "@/lib/auth/auth-actions";
import { useSession } from "@/lib/auth/session-context";
import { useToast } from "@/components/ui/toast";

/**
 * Signing in navigates away, and a URL fragment does not survive that round trip — so the
 * token is parked here for the detour. sessionStorage rather than the `next` query string,
 * because a query string is sent to the server and would put the token straight into the
 * access log the fragment exists to avoid.
 */
const PARKED_TOKEN_KEY = "querio.invitation";

/**
 * The fragment has to be read reactively, not once.
 *
 * Changing only the hash is not a route change, so Next re-uses the rendered page and a
 * plain read during render never runs again — arriving at /invite#token from /invite would
 * show "link incomplete" forever. Subscribing to hashchange is what makes the value live.
 */
function subscribeToHash(onChange: () => void) {
  window.addEventListener("hashchange", onChange);

  return () => window.removeEventListener("hashchange", onChange);
}

const subscribeToNothing = () => () => {};

type PreviewState =
  | { status: "idle" }
  | { status: "loaded"; preview: InvitationPreview }
  | { status: "failed"; message: string };

export function AcceptInvitation() {
  const { session, refresh } = useSession();
  const { showToast } = useToast();
  const router = useRouter();

  const [previewState, setPreviewState] = useState<PreviewState>({ status: "idle" });
  const [accepting, setAccepting] = useState(false);

  // The token can only be read in the browser, and asking "am I hydrated" as a store read
  // keeps that out of an effect — where setting state would cause a cascading render.
  const hydrated = useSyncExternalStore(
    subscribeToNothing,
    () => true,
    () => false,
  );

  const hash = useSyncExternalStore(
    subscribeToHash,
    () => window.location.hash,
    () => "",
  );

  const fromHash = hydrated ? decodeURIComponent(hash.replace(/^#/, "")).trim() : "";

  // The fragment when this is the original link; otherwise whatever was parked before the
  // detour through sign-in.
  const token =
    fromHash.length > 0
      ? fromHash
      : hydrated
        ? window.sessionStorage.getItem(PARKED_TOKEN_KEY)
        : null;

  const signedIn = session.status === "ready";

  // Persisting is a side effect, not state, so it belongs in an effect and causes no render.
  useEffect(() => {
    if (token) {
      window.sessionStorage.setItem(PARKED_TOKEN_KEY, token);
    }
  }, [token]);

  useEffect(() => {
    // Preview requires an authenticated caller, so it waits for sign-in rather than failing
    // and blaming the link.
    if (!token || !signedIn) {
      return;
    }

    let current = true;

    previewInvitation(token)
      .then((preview) => {
        if (current) {
          setPreviewState({ status: "loaded", preview });
        }
      })
      .catch((caught: unknown) => {
        if (current) {
          setPreviewState({
            status: "failed",
            message: toApiMessage(caught, "This invitation link is not valid."),
          });
        }
      });

    return () => {
      current = false;
    };
  }, [token, signedIn]);

  async function accept(value: string) {
    setAccepting(true);

    try {
      const organization = await acceptInvitation(value);

      window.sessionStorage.removeItem(PARKED_TOKEN_KEY);

      await refresh();
      showToast(`You have joined ${organization.name}.`, "success");
      router.replace(`/orgs/${organization.id}`);
    } catch (caught) {
      showToast(toApiMessage(caught), "error");
      setAccepting(false);
    }
  }

  if (!hydrated) {
    return <Waiting title="Checking your invitation" />;
  }

  if (token === null) {
    return (
      <AuthCard title="This link is incomplete">
        <Alert tone="error">
          The invitation link seems to have been cut short. Ask whoever invited you to send it
          again.
        </Alert>
      </AuthCard>
    );
  }

  if (!signedIn) {
    return (
      <AuthCard
        title="You have been invited to Querio"
        subtitle="Sign in or create an account to accept. Use the address the invitation was sent to."
      >
        <div className="flex flex-col gap-2">
          <Link
            href="/sign-up?next=%2Finvite"
            className="bg-primary text-primary-foreground hover:bg-primary/90 inline-flex h-10 items-center justify-center rounded-md px-4 text-sm font-medium"
          >
            Create an account
          </Link>
          <Link
            href="/sign-in?next=%2Finvite"
            className="border-border hover:bg-accent inline-flex h-10 items-center justify-center rounded-md border px-4 text-sm font-medium"
          >
            I already have an account
          </Link>
        </div>
      </AuthCard>
    );
  }

  if (previewState.status === "idle") {
    return <Waiting title="Checking your invitation" />;
  }

  if (previewState.status === "failed") {
    return (
      <AuthCard
        title="This invitation cannot be used"
        footer={
          <Link href="/orgs" className="text-primary font-medium hover:underline">
            Go to Querio
          </Link>
        }
      >
        <Alert tone="error">{previewState.message}</Alert>
      </AuthCard>
    );
  }

  const { preview } = previewState;
  const signedInEmail = session.profile.email;
  const invitedEmail = preview.email.toLowerCase();

  return (
    <AuthCard
      title={`Join ${preview.organizationName}`}
      subtitle={`You have been invited as a ${preview.role.toLowerCase()}.`}
    >
      {signedInEmail === invitedEmail ? (
        <Button loading={accepting} className="w-full" onClick={() => void accept(token)}>
          Accept invitation
        </Button>
      ) : (
        <>
          <Alert tone="error" title="Signed in as a different address">
            This invitation is for <span className="font-medium">{invitedEmail}</span>, but you are
            signed in as <span className="font-medium">{signedInEmail}</span>.
          </Alert>
          <Button variant="secondary" className="w-full" onClick={() => void signOutOfQuerio()}>
            Sign out and switch account
          </Button>
        </>
      )}
    </AuthCard>
  );
}

function Waiting({ title }: { title: string }) {
  return (
    <AuthCard title={title}>
      <p className="text-muted-foreground text-sm" role="status">
        One moment…
      </p>
    </AuthCard>
  );
}
