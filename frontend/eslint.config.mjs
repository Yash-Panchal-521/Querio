import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import prettier from "eslint-config-prettier/flat";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,

  // Must stay last: switches off every rule Prettier already owns.
  prettier,

  // .next-verify is where NEXT_DIST_DIR sends a verification build (see next.config.ts).
  // It is gitignored, so CI never sees it — but locally it is thousands of generated
  // chunks that bury real findings under phantom ones.
  globalIgnores([".next/**", ".next-verify/**", "out/**", "build/**", "next-env.d.ts"]),
]);

export default eslintConfig;
