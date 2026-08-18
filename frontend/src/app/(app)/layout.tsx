import { AppShell } from "@/components/app/app-shell";
import { RequireSession } from "@/components/auth/require-session";

export default function AppLayout({ children }: LayoutProps<"/">) {
  return (
    <RequireSession>
      <AppShell>{children}</AppShell>
    </RequireSession>
  );
}
