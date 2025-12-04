#!/usr/bin/env bash
set -euo pipefail

# End-to-end smoke:
# 1) Build and package the plugin into a temp neo-cli
# 2) Start neo-cli with Fairy plugin
# 3) Run HelloFairy via HTTP
# 4) Clean up the process

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIG:-Release}"
RPC_URL="${RPC_URL:-http://127.0.0.1:16868}"
LOG_PATH="${LOG_PATH:-/tmp/neo-cli.log}"

NEO_ROOT="${NEOROOT:-${NeoRoot:-$ROOT/../neo}}"
NEO_CLI="$NEO_ROOT/neo-cli/bin/$CONFIG/net10.0/neo-cli.dll"

if [[ ! -f "$NEO_CLI" ]]; then
  echo "neo-cli not found at $NEO_CLI. Build neo-cli first." >&2
  exit 1
fi

TMP_PLUGINS="$(mktemp -d)"
cleanup() {
  if [[ -n "${CLI_PID:-}" ]]; then
    kill "$CLI_PID" >/dev/null 2>&1 || true
  fi
  rm -rf "$TMP_PLUGINS"
}
trap cleanup EXIT

echo "Packaging Fairy plugin into $TMP_PLUGINS/Fairy ..."
"$ROOT/scripts/package-plugin.sh" -c "$CONFIG" -o "$TMP_PLUGINS/Fairy" >/dev/null

echo "Starting neo-cli with Fairy (log: $LOG_PATH)..."
dotnet "$NEO_CLI" --pluginspath "$TMP_PLUGINS" >"$LOG_PATH" 2>&1 &
CLI_PID=$!

# Wait a bit for RpcServer/Fairy to start
sleep 5

echo "Running HelloFairy smoke against $RPC_URL ..."
if ! "$ROOT/scripts/smoke-http.sh" "$RPC_URL"; then
  echo "Smoke failed. neo-cli log:"
  sed -n '1,200p' "$LOG_PATH"
  exit 1
fi

echo "Smoke succeeded."
