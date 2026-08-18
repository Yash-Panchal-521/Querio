"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { FileText, Home, Menu, Settings, Users } from "lucide-react";
import { Button } from "@/components/ui/button";
import { OrganizationSwitcher } from "@/components/app/organization-switcher";
import { QuerioMark } from "@/components/brand/querio-mark";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { UserMenu } from "@/components/app/user-menu";
import { useOrganizations } from "@/lib/auth/use-organizations";
import { cn } from "@/lib/utils";

/**
 * Sidebar for navigation, top bar for what is true everywhere.
 *
 * Vertical navigation scales as the product grows in a way a row of top-level tabs does not
 * — documents, conversations and usage are all still to come. The top bar is reserved for
 * the organization switcher and the account menu, which belong to no single page.
 */
export function AppShell({ children }: { children: React.ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false);
  const { organizations } = useOrganizations();

  // With no organization there is nowhere to navigate, so the sidebar would be a column of
  // disabled links. Strip it back to what is still true — the mark and the account menu —
  // and let the page own the screen.
  if (organizations.length === 0) {
    return (
      <div className="flex min-h-full flex-1 flex-col">
        <header className="flex h-14 items-center justify-between gap-3 px-4">
          <Link href="/orgs" className="flex items-center gap-2">
            <QuerioMark className="size-6" />
            <span className="font-semibold tracking-tight">Querio</span>
          </Link>
          <UserMenu />
        </header>

        <div className="flex flex-1 flex-col">{children}</div>
      </div>
    );
  }

  return (
    <div className="flex min-h-full flex-1">
      <aside className="border-border bg-muted/30 hidden w-60 shrink-0 flex-col border-r md:flex">
        <SidebarContents />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="border-border bg-background/80 sticky top-0 z-30 flex h-14 items-center gap-3 border-b px-4 backdrop-blur">
          <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="sm" className="md:hidden" aria-label="Open navigation">
                <Menu />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-64 p-0">
              <SheetTitle className="sr-only">Navigation</SheetTitle>
              <SidebarContents onNavigate={() => setMobileOpen(false)} />
            </SheetContent>
          </Sheet>

          <OrganizationSwitcher />

          <div className="ml-auto flex items-center gap-1">
            <UserMenu />
          </div>
        </header>

        <div className="flex flex-1 flex-col">{children}</div>
      </div>
    </div>
  );
}

function SidebarContents({ onNavigate }: { onNavigate?: () => void }) {
  const { active } = useOrganizations();
  const pathname = usePathname();

  const base = active ? `/orgs/${active.id}` : null;
  const isOwner = active?.role === "Owner";

  return (
    <div className="flex h-full flex-col gap-1 p-3">
      <Link href="/orgs" onClick={onNavigate} className="mb-3 flex items-center gap-2 px-2 py-1.5">
        <QuerioMark className="size-6" />
        <span className="font-semibold tracking-tight">Querio</span>
      </Link>

      <nav className="flex flex-col gap-0.5">
        <NavItem
          href={base ?? "/orgs"}
          icon={Home}
          label="Overview"
          active={pathname === base}
          onNavigate={onNavigate}
        />
        <NavItem
          href={base ? `${base}/documents` : "/orgs"}
          icon={FileText}
          label="Documents"
          active={pathname === `${base}/documents`}
          disabled={!base}
          onNavigate={onNavigate}
          badge="Soon"
        />
        <NavItem
          href={base ? `${base}/members` : "/orgs"}
          icon={Users}
          label="Members"
          active={pathname === `${base}/members`}
          disabled={!base}
          onNavigate={onNavigate}
        />
        {isOwner ? (
          <NavItem
            href={`${base}/settings`}
            icon={Settings}
            label="Settings"
            active={pathname === `${base}/settings`}
            onNavigate={onNavigate}
          />
        ) : null}
      </nav>
    </div>
  );
}

function NavItem({
  href,
  icon: Icon,
  label,
  active,
  disabled,
  badge,
  onNavigate,
}: {
  href: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  active?: boolean;
  disabled?: boolean;
  badge?: string;
  onNavigate?: () => void;
}) {
  const className = cn(
    "flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm transition-colors",
    active
      ? "bg-background text-foreground border-border shadow-xs border font-medium"
      : "text-muted-foreground hover:text-foreground hover:bg-accent",
    disabled && "pointer-events-none opacity-45",
  );

  const content = (
    <>
      <Icon className="size-4 shrink-0" />
      <span className="truncate">{label}</span>
      {badge ? (
        <span className="border-border text-muted-foreground ml-auto rounded border px-1.5 py-0.5 text-[10px] tracking-wide uppercase">
          {badge}
        </span>
      ) : null}
    </>
  );

  if (disabled) {
    return (
      <span aria-disabled="true" className={className}>
        {content}
      </span>
    );
  }

  return (
    <Link
      href={href}
      onClick={onNavigate}
      aria-current={active ? "page" : undefined}
      className={className}
    >
      {content}
    </Link>
  );
}
