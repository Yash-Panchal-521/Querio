"use client";

import { useState } from "react";
import Link from "next/link";
import { Alert } from "@/components/ui/alert";
import { AuthDivider } from "@/components/auth/auth-divider";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { GoogleButton } from "@/components/auth/google-button";
import { signInWithGoogle, signInWithPassword } from "@/lib/auth/auth-actions";
import { isCancelled, toAuthMessage } from "@/lib/auth/auth-errors";
import { useAuthRedirect } from "@/lib/auth/use-auth-redirect";

export function SignInForm() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState<"password" | "google" | null>(null);

  // The session provider redirects once provisioning completes, so neither handler navigates.
  useAuthRedirect();

  async function run(kind: "password" | "google", action: () => Promise<void>) {
    setError(null);
    setPending(kind);

    try {
      await action();
    } catch (caught) {
      // Dismissing the Google window is a decision, not a failure.
      if (!isCancelled(caught)) {
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
          void run("password", () => signInWithPassword(email, password));
        }}
      >
        {error ? <Alert tone="error">{error}</Alert> : null}

        <Field
          label="Email"
          type="email"
          name="email"
          autoComplete="email"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
        />

        <div className="flex flex-col gap-1.5">
          <Field
            label="Password"
            type="password"
            name="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
          <Link
            href="/forgot-password"
            className="text-muted-foreground self-end text-xs hover:underline"
          >
            Forgot your password?
          </Link>
        </div>

        <Button
          type="submit"
          loading={pending === "password"}
          disabled={pending !== null}
          className="w-full"
        >
          Sign in
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
