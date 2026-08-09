#!/usr/bin/env bash
# Rewrites every place a server's address needs to be known for direct (non-tunnel) access,
# in one command. Run this once after allocating an Elastic IP (recommended — plain instance
# public IPs change on every stop/start, which otherwise means redoing all three edits below
# each time; see DEPLOY.md).
#
# Usage: scripts/set-host.sh <ip-or-hostname> [frontend-port] [backend-port]
set -euo pipefail

HOST="${1:?Usage: scripts/set-host.sh <ip-or-hostname> [frontend-port] [backend-port]}"
FRONTEND_PORT="${2:-3000}"
BACKEND_PORT="${3:-5163}"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$REPO_ROOT/frontend/.env.local"

if [ ! -f "$ENV_FILE" ]; then
  cp "$REPO_ROOT/frontend/.env.local.example" "$ENV_FILE"
fi

set_env_var() {
  local key="$1" value="$2"
  if grep -q "^${key}=" "$ENV_FILE" 2>/dev/null; then
    sed -i.bak "s|^${key}=.*|${key}=${value}|" "$ENV_FILE" && rm -f "$ENV_FILE.bak"
  else
    echo "${key}=${value}" >> "$ENV_FILE"
  fi
}

set_env_var "NEXT_PUBLIC_API_BASE_URL" "http://${HOST}:${BACKEND_PORT}"
set_env_var "NEXT_PUBLIC_DEV_ORIGIN" "http://${HOST}:${FRONTEND_PORT}"

echo "Updated $ENV_FILE:"
echo "  NEXT_PUBLIC_API_BASE_URL=http://${HOST}:${BACKEND_PORT}"
echo "  NEXT_PUBLIC_DEV_ORIGIN=http://${HOST}:${FRONTEND_PORT}"
echo
echo "Remaining manual step (backend CORS — not a file this script can safely append to):"
echo "  export App__AllowedOrigins__0=\"http://${HOST}:${FRONTEND_PORT}\""
echo
echo "Then restart/rebuild both:"
echo "  frontend: npm run build && npm run start   (NEXT_PUBLIC_* vars are baked in at build time)"
echo "  backend:  restart the dotnet process so the new App__AllowedOrigins__0 takes effect"
