import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { ThemeProvider } from "@/components/theme-provider";
import { SessionProvider } from "@/lib/auth/session-context";
import { ToastProvider } from "@/components/ui/toast";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: {
    default: "Querio",
    template: "%s · Querio",
  },
  description:
    "Ask your team's documents in plain language. Every answer is grounded in your own content, with citations back to the source.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // next-themes adds the resolved `class` and `style` before paint, so the DOM
    // the client sees never matches the server HTML. That mismatch is expected
    // and only on <html>, which is exactly what suppressHydrationWarning covers.
    <html
      lang="en"
      suppressHydrationWarning
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <body className="flex min-h-full flex-col">
        {/* Toasts wrap the session provider, because provisioning failures are reported
            through them and retried automatically. */}
        <ThemeProvider>
          <ToastProvider>
            <SessionProvider>{children}</SessionProvider>
          </ToastProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
