"use client";

import Link from "next/link";
import { Building2, Check, ChevronsUpDown, Plus } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useOrganizations } from "@/lib/auth/use-organizations";
import { cn } from "@/lib/utils";

export function OrganizationSwitcher() {
  const { organizations, active } = useOrganizations();

  if (organizations.length === 0) {
    return null;
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className={cn(
            "hover:bg-accent focus-visible:ring-ring flex h-9 max-w-[15rem] items-center gap-2 rounded-md px-2",
            "focus-visible:ring-2 focus-visible:outline-none",
          )}
        >
          <span className="bg-primary/10 text-primary flex size-6 shrink-0 items-center justify-center rounded">
            <Building2 className="size-3.5" />
          </span>
          <span className="truncate text-sm font-medium">
            {active?.name ?? "Choose an organization"}
          </span>
          <ChevronsUpDown className="text-muted-foreground size-3.5 shrink-0" />
        </button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel className="text-muted-foreground text-xs font-normal">
          Organizations
        </DropdownMenuLabel>

        {organizations.map((organization) => (
          <DropdownMenuItem key={organization.id} asChild>
            {/* Navigation, not state: the whole page re-reads from the URL, so switching
                cannot leave one organization's data on screen under another's name. */}
            <Link href={`/orgs/${organization.id}`}>
              <span className="truncate">{organization.name}</span>
              <span className="text-muted-foreground ml-auto flex items-center gap-1.5 text-xs">
                {organization.role}
                {organization.id === active?.id ? (
                  <Check className="text-primary size-3.5" />
                ) : null}
              </span>
            </Link>
          </DropdownMenuItem>
        ))}

        <DropdownMenuSeparator />

        <DropdownMenuItem asChild>
          <Link href="/orgs/new">
            <Plus />
            Create an organization
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
