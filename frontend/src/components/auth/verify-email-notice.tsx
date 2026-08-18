"use client";

import { useEffect, useState } from "react";
import { Alert } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { refreshVerificationState, resendVerificationEmail } from "@/lib/auth/auth-actions";
import { toAuthMessage } from "@/lib/auth/auth-errors";
import { secondsUntilResendAllowed } from "@/lib/auth/verification-cooldown";
import { useToast } from "@/components/ui/toast";

export function VerifyEmailNotice({
  uid,
  email,
  onVerified,
}: {
  uid: string;
  email: string;
  onVerified: () => Promise<void>;
}) {
  const { showToast } = useToast();
  const [cooldown, setCooldown] = useState(0);
  const [pending, setPending] = useState<"resend" | "check" | null>(null);

  // Ticks the countdown down. Reading the stored timestamp each second rather than counting
  // locally means a reload, or a second tab, shows the same remaining time.
  useEffect(() => {
    const update = () => setCooldown(secondsUntilResendAllowed(uid));

    update();

    const timer = setInterval(update, 1000);

    return () => clearInterval(timer);
  }, [uid]);

  async function run(kind: "resend" | "check", action: () => Promise<void>) {
    setPending(kind);

    try {
      await action();
    } catch (caught) {
      showToast(toAuthMessage(caught), "error");
    } finally {
      setPending(null);
    }
  }

  return (
    <Alert tone="info" title="Verify your email address">
      <p>
        We sent a link to <span className="font-medium">{email}</span> when you signed up. Follow it
        to confirm the address, then choose “I&rsquo;ve verified”.
      </p>
      <p className="mt-2">
        Creating an organization needs a confirmed address, so invitations you send can be trusted.
        If the email has not arrived, check your spam folder.
      </p>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        <Button
          variant="secondary"
          loading={pending === "resend"}
          // Firebase refuses a second send to the same address within about a minute, so
          // waiting is better than spending the attempt and reporting a failure.
          disabled={cooldown > 0 || pending !== null}
          onClick={() =>
            void run("resend", async () => {
              await resendVerificationEmail();
              showToast("Verification email sent. Check your inbox.", "success");
            })
          }
        >
          {cooldown > 0 ? `Resend in ${cooldown}s` : "Resend email"}
        </Button>

        <Button
          variant="ghost"
          loading={pending === "check"}
          disabled={pending !== null}
          onClick={() =>
            void run("check", async () => {
              // Firebase caches emailVerified client-side, so following the link in another
              // tab changes nothing here until the user is reloaded and a token reminted.
              if (await refreshVerificationState()) {
                await onVerified();
                showToast("Email verified.", "success");
              } else {
                showToast("Not verified yet. Follow the link in the email, then check again.");
              }
            })
          }
        >
          I&rsquo;ve verified
        </Button>
      </div>
    </Alert>
  );
}
