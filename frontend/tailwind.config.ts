import type { Config } from "tailwindcss";

// Appendix C.8 of docs/TECHNICAL_SPEC.md — binding token set.
export default {
  content: [
    "./src/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        ink: "#12181B",
        slate: { DEFAULT: "#5A666B" },
        paper: "#F6F7F6",
        surface: "#FFFFFF",
        rule: { DEFAULT: "#DDE2E1", strong: "#C3CBCA" },
        accent: { DEFAULT: "#403A7E", hover: "#4E4794", muted: "#E8E7F2" },
        state: {
          idle: "#8C9599",
          blocked: "#B07D2B",
          ready: "#403A7E",
          complete: "#2F6B4F",
          failed: "#A33A3A",
          skipped: "#A8AFB2",
        },
        data: {
          1: "#0072B2",
          2: "#D55E00",
          3: "#009E73",
          4: "#CC79A7",
          5: "#E69F00",
          6: "#56B4E9",
        },
        feedback: {
          successBg: "#EDF4F0",
          errorBg: "#F7EDED",
          warningBg: "#F7F1E4",
          infoBg: "#EDF0F2",
        },
        score: {
          0: "#DDE2E1",
          25: "#B5B7CF",
          50: "#8D8BB4",
          75: "#66609A",
          100: "#403A7E",
        },
      },
      fontFamily: {
        sans: ["IBM Plex Sans", "system-ui", "sans-serif"],
        mono: ["IBM Plex Mono", "ui-monospace", "monospace"],
      },
      fontSize: {
        display: ["32px", { lineHeight: "1.15", fontWeight: "600", letterSpacing: "-0.02em" }],
        h1: ["24px", { lineHeight: "1.25", fontWeight: "600", letterSpacing: "-0.01em" }],
        h2: ["18px", { lineHeight: "1.35", fontWeight: "600" }],
        body: ["15px", { lineHeight: "1.55", fontWeight: "400" }],
        ui: ["14px", { lineHeight: "1.45", fontWeight: "450" }],
        small: ["13px", { lineHeight: "1.4", fontWeight: "400" }],
        micro: ["11px", { lineHeight: "1.3", fontWeight: "500", letterSpacing: "0.06em" }],
      },
      borderRadius: {
        DEFAULT: "3px",
        sm: "2px",
        md: "3px",
        lg: "4px",
        // Deliberately omitted: xl, 2xl, 3xl. Do not re-add them.
      },
      boxShadow: {
        DEFAULT: "none",
        overlay: "0 2px 8px rgba(18,24,27,0.12)",
      },
      spacing: {
        1: "4px",
        2: "8px",
        3: "12px",
        4: "16px",
        6: "24px",
        8: "32px",
        12: "48px",
        16: "64px",
      },
    },
  },
} satisfies Config;
