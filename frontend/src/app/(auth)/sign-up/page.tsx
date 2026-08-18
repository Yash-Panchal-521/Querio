import { Suspense } from "react";
import type { Metadata } from "next";
import Link from "next/link";
import { SignUpForm } from "./sign-up-form";
import { AuthCard } from "@/components/auth/auth-card";

export const metadata: Metadata = { title: "Create an account" };

export default function SignUpPage() {
  return (
    <AuthCard
      title="Create your account"
      subtitle="Start asking questions of your team's documents."
      footer={
        <>
          Already have an account?{" "}
          <Link href="/sign-in" className="text-primary font-medium hover:underline">
            Sign in
          </Link>
        </>
      }
    >
      <Suspense fallback={<div className="text-muted-foreground text-sm">Loading…</div>}>
        <SignUpForm />
      </Suspense>
    </AuthCard>
  );
}
