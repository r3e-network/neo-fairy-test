#!/usr/bin/env bash
set -euo pipefail

# End-to-end smoke:
# 1) Build and package the plugin into neo-cli Plugins
# 2) Start neo-cli with Fairy plugin
# 3) Run HelloFairy via HTTP
# 4) Clean up the process

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG="${CONFIG:-Release}"
RPC_URL="${RPC_URL:-http://127.0.0.1:16868}"
LOG_PATH="${LOG_PATH:-/tmp/neo-cli.log}"

NEO_ROOT="${NEOROOT:-${NeoRoot:-}}"
if [[ -z "$NEO_ROOT" ]]; then
  if [[ -d "$ROOT/neo_csharp" ]]; then
    NEO_ROOT="$ROOT/neo_csharp"
  else
    NEO_ROOT="$ROOT/../neo"
  fi
fi
NEO_CLI=""

# Neo repo layout changed over time:
# - Old: <neo>/neo-cli/bin/<cfg>/net10.0/neo-cli.dll
# - New: <neo>/bin/Neo.CLI/net10.0/neo-cli.dll
# - Split: <neo_csharp>/node/src/Neo.CLI/bin/<cfg>/net10.0/neo-cli.dll
NEO_CLI_CANDIDATES=(
  "$NEO_ROOT/bin/Neo.CLI/net10.0/neo-cli.dll"
  "$NEO_ROOT/neo-cli/bin/$CONFIG/net10.0/neo-cli.dll"
  "$NEO_ROOT/node/bin/Neo.CLI/net10.0/neo-cli.dll"
  "$NEO_ROOT/node/src/Neo.CLI/bin/$CONFIG/net10.0/neo-cli.dll"
)

for candidate in "${NEO_CLI_CANDIDATES[@]}"; do
  if [[ -f "$candidate" ]]; then
    NEO_CLI="$candidate"
    break
  fi
done

if [[ ! -f "$NEO_CLI" ]]; then
  echo "neo-cli not found at $NEO_CLI. Build neo-cli first." >&2
  exit 1
fi

NEO_CLI_DIR="$(cd "$(dirname "$NEO_CLI")" && pwd)"
cleanup() {
  if [[ -n "${CLI_PID:-}" ]]; then
    kill "$CLI_PID" >/dev/null 2>&1 || true
    sleep 1
    kill -9 "$CLI_PID" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "Packaging Fairy plugin into $NEO_CLI_DIR/Plugins/Fairy ..."
"$ROOT/scripts/package-plugin.sh" -c "$CONFIG" -o "$NEO_CLI_DIR/Plugins/Fairy" >/dev/null

echo "Starting neo-cli with Fairy (log: $LOG_PATH)..."
pushd "$NEO_CLI_DIR" >/dev/null
dotnet "$NEO_CLI" --background --config "$NEO_CLI_DIR/config.json" >"$LOG_PATH" 2>&1 &
CLI_PID=$!
popd >/dev/null

# Wait a bit for RpcServer/Fairy to start
sleep 5

echo "Running HelloFairy smoke against $RPC_URL ..."
if ! "$ROOT/scripts/smoke-http.sh" "$RPC_URL"; then
  echo "Smoke failed. neo-cli log:"
  sed -n '1,200p' "$LOG_PATH"
  exit 1
fi

echo "Smoke succeeded."
