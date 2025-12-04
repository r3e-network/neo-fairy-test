#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NEO_ROOT="${NEOROOT:-${NeoRoot:-$ROOT/../neo}}"

echo "Checking Neo root at: $NEO_ROOT"

NEO_CSProj="$NEO_ROOT/src/Neo/Neo.csproj"
NEO_CONSOLE="$NEO_ROOT/src/Neo.ConsoleService/Neo.ConsoleService.csproj"
RPC_SERVER="$NEO_ROOT/src/Plugins/RpcServer/RpcServer.csproj"

missing=0
for p in "$NEO_CSProj" "$NEO_CONSOLE" "$RPC_SERVER"; do
  if [[ ! -f "$p" ]]; then
    echo "Missing: $p" >&2
    missing=$((missing + 1))
  fi
done

if [[ $missing -ne 0 ]]; then
  echo "Neo root check failed. Set NEOROOT or NeoRoot to your neo checkout." >&2
  exit 1
fi

echo "Neo root looks good."
