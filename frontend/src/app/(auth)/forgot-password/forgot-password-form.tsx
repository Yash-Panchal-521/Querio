"use client";

import { useState } from "react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import { requestPasswordReset } from "@/lib/auth/auth-actions";
import { toAuthMessage } from "@/lib/auth/auth-errors";

export function ForgotPasswordForm() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  if (sent) {
    return (
      <Alert tone="success" title="Check your email">
        If an account exists for that address, a reset link is on its way. The link expires after a
        short while — request another if it does.
      </Alert>
    );
  }

  return (
    <form
      noValidate
      className="flex flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        setError(null);
        setPending(true);

        void requestPasswordReset(email)
          .then(() => {
            // Shown whether or not the address is registered. Differentiating would turn this
            // form into a way to discover who has a Querio account.
            setSent(true);
          })
          .catch((caught: unknown) => setError(toAuthMessage(caught)))
          .finally(() => setPending(false));
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

      <Button type="submit" loading={pending} className="w-full">
        Send reset link
      </Button>
    </form>
  );
}
