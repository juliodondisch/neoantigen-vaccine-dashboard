import type { NextConfig } from "next";

// Origins the Next.js dev server accepts requests from (distinct from the backend's own
// AppConfig.AllowedOrigins CORS list). Only needed for direct-IP access to a remote dev
// server; an SSH tunnel (DEPLOY.md's recommended path) never needs this since the browser
// always sees localhost. Set via env rather than hardcoding a server's IP in source, which
// changes every stop/start unless an Elastic IP is allocated (see DEPLOY.md).
const devOrigin = process.env.NEXT_PUBLIC_DEV_ORIGIN;

const nextConfig: NextConfig = {
  ...(devOrigin ? { allowedDevOrigins: [devOrigin] } : {}),
};

export default nextConfig;
