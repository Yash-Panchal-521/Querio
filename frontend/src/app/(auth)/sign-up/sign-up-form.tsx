"use client";

import { useState } from "react";
import Link from "next/link";
import { FirebaseError } from "firebase/app";
import { Alert } from "@/components/ui/alert";
import { AuthDivider } from "@/components/auth/auth-divider";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { GoogleButton } from "@/components/auth/google-button";
import { signInWithGoogle, signUpWithPassword } from "@/lib/auth/auth-actions";
import { isCancelled, toAuthMessage } from "@/lib/auth/auth-errors";
import { useAuthRedirect } from "@/lib/auth/use-auth-redirect";

const MINIMUM_PASSWORD_LENGTH = 8;

export function SignUpForm() {
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [emailTaken, setEmailTaken] = useState(false);
  const [pending, setPending] = useState<"password" | "google" | null>(null);

  useAuthRedirect();

  function validatePassword(value: string): boolean {
    if (value.length < MINIMUM_PASSWORD_LENGTH) {
      setPasswordError(`Use at least ${MINIMUM_PASSWORD_LENGTH} characters.`);

      return false;
    }

    setPasswordError(null);

    return true;
  }

  async function run(kind: "password" | "google", action: () => Promise<void>) {
    setError(null);
    setEmailTaken(false);
    setPending(kind);

    try {
      await action();
    } catch (caught) {
      if (!isCancelled(caught)) {
        // Worth its own treatment: the answer is a link to sign in, not a red message.
        setEmailTaken(
          caught instanceof FirebaseError && caught.code === "auth/email-already-in-use",
        );
        setError(toAuthMessage(caught));
      }

      setPending(null);
    }
  }

  return (
    <>
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();

          if (!validatePassword(password)) {
            return;
          }

          void run("password", () => signUpWithPassword(email, password, displayName));
        }}
      >
        {error ? (
          <Alert tone="error">
            {error}
            {emailTaken ? (
              <>
                {" "}
                <Link href="/sign-in" className="font-medium underline">
                  Sign in instead
                </Link>
              </>
            ) : null}
          </Alert>
        ) : null}

        <Field
          label="Your name"
          name="name"
          autoComplete="name"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
        />

        <Field
          label="Email"
          type="email"
          name="email"
          autoComplete="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />

        <Field
          label="Password"
          type="password"
          name="password"
          autoComplete="new-password"
          required
          hint={`At least ${MINIMUM_PASSWORD_LENGTH} characters.`}
          error={passwordError ?? undefined}
          value={password}
          onChange={(event) => {
            setPassword(event.target.value);

            // Only re-validates once it has already complained, so the message does not
            // appear while someone is still typing their first character.
            if (passwordError) {
              validatePassword(event.target.value);
            }
          }}
        />

        <Button
          type="submit"
          loading={pending === "password"}
          disabled={pending !== null}
          className="w-full"
        >
          Create account
        </Button>
      </form>

      <AuthDivider />

      <GoogleButton
        label="Continue with Google"
        loading={pending === "google"}
        onClick={() => void run("google", signInWithGoogle)}
      />
    </>
  );
}
