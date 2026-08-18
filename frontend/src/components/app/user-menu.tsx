"use client";

import Link from "next/link";
import { LogOut, Monitor, Moon, Sun, User } from "lucide-react";
import { useTheme } from "next-themes";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { signOutOfQuerio } from "@/lib/auth/auth-actions";
import { useSession } from "@/lib/auth/session-context";

/**
 * Account, theme and sign-out in one place. Previously these were three separate controls
 * competing with the organization switcher for the same corner.
 */
export function UserMenu() {
  const { session } = useSession();
  const { theme, setTheme } = useTheme();

  if (session.status !== "ready") {
    return null;
  }

  const { profile } = session;
  const name = profile.displayName ?? profile.email;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label="Account menu"
          className="focus-visible:ring-ring hover:bg-accent flex items-center gap-2 rounded-md p-1 focus-visible:ring-2 focus-visible:outline-none"
        >
          <Avatar className="size-7">
            <AvatarFallback className="bg-primary/10 text-primary text-xs font-medium">
              {initials(name)}
            </AvatarFallback>
          </Avatar>
        </button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel className="flex flex-col gap-0.5">
          <span className="truncate text-sm font-medium">{name}</span>
          <span className="text-muted-foreground truncate text-xs font-normal">
            {profile.email}
          </span>
        </DropdownMenuLabel>

        <DropdownMenuSeparator />

        <DropdownMenuItem asChild>
          <Link href="/account">
            <User />
            Your account
            {profile.emailVerified ? null : (
              <span className="bg-warning/15 text-warning ml-auto rounded px-1.5 py-0.5 text-[10px] font-medium">
                Verify
              </span>
            )}
          </Link>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">
          Theme
        </DropdownMenuLabel>
        <DropdownMenuRadioGroup value={theme ?? "system"} onValueChange={setTheme}>
          <DropdownMenuRadioItem value="light">
            <Sun />
            Light
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="dark">
            <Moon />
            Dark
          </DropdownMenuRadioItem>
          <DropdownMenuRadioItem value="system">
            <Monitor />
            System
          </DropdownMenuRadioItem>
        </DropdownMenuRadioGroup>

        <DropdownMenuSeparator />

        <DropdownMenuItem variant="destructive" onSelect={() => void signOutOfQuerio()}>
          <LogOut />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Two letters at most — more turns an avatar into a word. */
function initials(value: string): string {
  const parts = value
    .trim()
    .split(/[\s@.]+/)
    .filter(Boolean);
  const letters = parts.slice(0, 2).map((part) => part[0] ?? "");

  return letters.join("").toUpperCase() || "?";
}
