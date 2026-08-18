"use client";

import { useState } from "react";
import { Mail, UserPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field } from "@/components/ui/field";
import type { TenantRole } from "@/lib/api/me";
import { RoleSelect } from "@/components/members/role-select";

/**
 * Controls shown beside the things they sit next to in the product.
 *
 * Alignment bugs are invisible in isolation and obvious in a row, and the rows that matter
 * live behind authentication and an organization — which makes them awkward to inspect. This
 * puts the same pairings somewhere always reachable.
 */
export function ControlsPreview() {
  const [inviteRole, setInviteRole] = useState<TenantRole>("Member");
  const [memberRole, setMemberRole] = useState<TenantRole>("Owner");

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-col gap-3">
        <p className="text-muted-foreground text-xs">
          Invite row — medium select, medium button, aligned to the input above them.
        </p>
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(event) => event.preventDefault()}
        >
          <div className="min-w-[15rem] flex-1">
            <Field
              label="Email address"
              type="email"
              icon={Mail}
              placeholder="teammate@company.com"
            />
          </div>
          <RoleSelect value={inviteRole} onChange={setInviteRole} label="Role" />
          <Button type="submit">
            <UserPlus />
            Invite
          </Button>
        </form>
      </div>

      <div className="flex flex-col gap-3">
        <p className="text-muted-foreground text-xs">
          Member row — small select beside a small icon button.
        </p>
        <div className="border-border flex items-center gap-3 rounded-md border px-3 py-3">
          <div className="flex flex-1 flex-col">
            <span className="text-sm font-medium">Ada Lovelace</span>
            <span className="text-muted-foreground text-xs">ada@example.com</span>
          </div>
          <RoleSelect value={memberRole} onChange={setMemberRole} size="sm" label="Role" />
          <Button variant="ghost" size="sm" aria-label="Remove member">
            <UserPlus />
          </Button>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <p className="text-muted-foreground w-full text-xs">Button variants and sizes.</p>
        <Button size="sm">Small</Button>
        <Button>Medium</Button>
        <Button size="lg">Large</Button>
        <Button variant="secondary">Secondary</Button>
        <Button variant="ghost">Ghost</Button>
        <Button variant="destructive">Destructive</Button>
        <Button loading>Loading</Button>
      </div>
    </div>
  );
}
