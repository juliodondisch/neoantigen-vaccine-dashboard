import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
  {
    rules: {
      // eslint-plugin-react-hooks v7 ships React Compiler-era rules that
      // flag the standard "setIsLoading(true) then fetch" effect pattern
      // and ref-mutation-on-every-render (used deliberately in
      // hooks/useStepPolling.ts to keep latest callbacks without
      // re-subscribing). Both patterns are intentional here ,  this app has
      // no React Compiler in its build ,  so they're downgraded to warnings
      // rather than rewritten around a stricter model the rest of the repo
      // doesn't opt into.
      "react-hooks/set-state-in-effect": "warn",
      "react-hooks/refs": "warn",
    },
  },
]);

export default eslintConfig;
