import { Suspense } from "react";
import type { Metadata } from "next";
import Link from "next/link";
import { SignInForm } from "./sign-in-form";
import { AuthCard } from "@/components/auth/auth-card";

export const metadata: Metadata = { title: "Sign in" };

export default function SignInPage() {
  return (
    <AuthCard
      title="Sign in to Querio"
      subtitle="Ask your team's documents in plain language."
      footer={
        <>
          New here?{" "}
          <Link href="/sign-up" className="text-primary font-medium hover:underline">
            Create an account
          </Link>
        </>
      }
    >
      {/* The form reads ?next= to resume where the visitor was headed, and useSearchParams
          opts a route out of prerendering unless it sits behind a boundary. */}
      <Suspense fallback={<div className="text-muted-foreground text-sm">Loading…</div>}>
        <SignInForm />
      </Suspense>
    </AuthCard>
  );
}
