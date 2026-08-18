"use client";

import { ThemeProvider as NextThemesProvider } from "next-themes";
import type { ReactNode } from "react";

/**
 * next-themes reads localStorage and matchMedia, so it can only run on the
 * client. Keeping the boundary in this file rather than in the root layout lets
 * the layout stay a server component — otherwise every page under it would ship
 * to the browser as client code.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="system"
      enableSystem
      // The library animates nothing itself, but a global `transition-colors`
      // would make every token cross-fade on toggle. Suppressing transitions for
      // the duration of the swap keeps it instant.
      disableTransitionOnChange
    >
      {children}
    </NextThemesProvider>
  );
}
