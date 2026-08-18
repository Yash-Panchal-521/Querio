import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Joins conditional class names, then drops any Tailwind utility that a later
 * one overrides. Plain `clsx` would leave both `p-2` and `p-4` on the element
 * and let CSS source order decide the winner, which makes a caller's override
 * silently depend on where the utility landed in the generated sheet.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
