"use client";

import type { TenantRole } from "@/lib/api/me";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

const ROLES: { role: TenantRole; description: string }[] = [
  { role: "Member", description: "Read documents and ask questions" },
  { role: "Admin", description: "Also invite and remove members" },
  { role: "Owner", description: "Full control, including deleting" },
];

/**
 * A styled dropdown rather than a native select.
 *
 * A native select renders its list through the operating system: it cannot be given our
 * typography or colours, it ignores the theme entirely, and its control metrics differ from
 * every other input we ship — which is what made it sit a pixel out from the button beside
 * it. Radix renders the list as ordinary DOM, so it matches, and there is room to say what
 * each role actually means.
 */
export function RoleSelect({
  value,
  onChange,
  disabled,
  /** Owners can only be granted by an owner, so the option is withheld where that applies. */
  allowOwner = true,
  label = "Role",
  size = "md",
}: {
  value: TenantRole;
  onChange: (role: TenantRole) => void;
  disabled?: boolean;
  allowOwner?: boolean;
  label?: string;
  /** Matched to whatever control sits next to it, so the row lines up. */
  size?: "sm" | "md";
}) {
  const options = allowOwner ? ROLES : ROLES.filter((option) => option.role !== "Owner");

  return (
    <Select
      value={value}
      onValueChange={(next) => onChange(next as TenantRole)}
      disabled={disabled}
    >
      <SelectTrigger
        aria-label={label}
        // The trigger ships h-8 for "sm", which already matches a small button. There is no
        // h-10 variant, so the medium case overrides it — and needs `!` because the built-in
        // height is applied through a data-attribute selector that outranks a plain class.
        size={size === "sm" ? "sm" : "default"}
        className={cn(size === "sm" ? "w-[7.5rem] text-xs" : "!h-10 w-[8.5rem] text-sm")}
      >
        {/* SelectValue must stay: it is the node Radix measures to anchor the popup, and
            without it the menu renders at the top-left of the viewport instead of under the
            trigger. Passing children overrides what it displays, so the trigger shows the
            role name alone while the descriptions stay in the list where they help someone
            choose. */}
        <SelectValue>
          <span className="truncate">{value}</span>
        </SelectValue>
      </SelectTrigger>

      {/* Explicitly popper-positioned. Radix's default aligns the selected item over the
          trigger, which covers the control you just clicked; dropping below keeps the
          current value visible while choosing. */}
      <SelectContent position="popper" side="bottom" align="end" sideOffset={6}>
        {options.map((option) => (
          <SelectItem key={option.role} value={option.role} className="items-start gap-2 py-2">
            <div className="flex flex-col gap-0.5">
              <span className="text-sm font-medium">{option.role}</span>
              <span className="text-muted-foreground text-xs">{option.description}</span>
            </div>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
