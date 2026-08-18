import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /*
   * A build normally writes to .next — the same directory `next dev` serves from. Verifying
   * a build while someone has the dev server running therefore replaces its output and
   * leaves them with a server that 404s routes which plainly exist. Setting NEXT_DIST_DIR
   * sends a verification build somewhere harmless instead.
   */
  distDir: process.env.NEXT_DIST_DIR ?? ".next",
};

export default nextConfig;
