#!/usr/bin/env bash
set -euo pipefail

CONFIG="Release"
DEST=""

usage() {
  echo "Usage: $0 [-c Debug|Release] [-o /path/to/neo-cli/bin/<cfg>/net10.0/Plugins/Fairy]" >&2
  exit 1
}

while getopts "c:o:h" opt; do
  case "$opt" in
    c) CONFIG="$OPTARG" ;;
    o) DEST="$OPTARG" ;;
    h) usage ;;
    *) usage ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT_DIR="$ROOT/src/Fairy.Plugin/bin/$CONFIG/net10.0"
NEO_ROOT="${NEOROOT:-${NeoRoot:-}}"
if [[ -z "$NEO_ROOT" ]]; then
  if [[ -d "$ROOT/neo_csharp" ]]; then
    NEO_ROOT="$ROOT/neo_csharp"
  else
    NEO_ROOT="$ROOT/../neo"
  fi
fi

if [[ -z "$DEST" ]]; then
  # Neo repo layout changed over time:
  # - Old: <neo>/neo-cli/bin/<cfg>/net10.0
  # - New: <neo>/bin/Neo.CLI/net10.0
  # - Split: <neo_csharp>/node/src/Neo.CLI/bin/<cfg>/net10.0
  if [[ -d "$NEO_ROOT/bin/Neo.CLI/net10.0" ]]; then
    DEST="$NEO_ROOT/bin/Neo.CLI/net10.0/Plugins/Fairy"
  elif [[ -d "$NEO_ROOT/neo-cli/bin/$CONFIG/net10.0" ]]; then
    DEST="$NEO_ROOT/neo-cli/bin/$CONFIG/net10.0/Plugins/Fairy"
  elif [[ -d "$NEO_ROOT/node/bin/Neo.CLI/net10.0" ]]; then
    DEST="$NEO_ROOT/node/bin/Neo.CLI/net10.0/Plugins/Fairy"
  else
    DEST="$NEO_ROOT/node/src/Neo.CLI/bin/$CONFIG/net10.0/Plugins/Fairy"
  fi
fi

echo "Building Fairy plugin ($CONFIG)..."
dotnet build "$ROOT/src/Fairy.Plugin/Fairy.csproj" -c "$CONFIG" --nologo -p:NeoRoot="$NEO_ROOT"

echo "Copying artifacts to: $DEST"
mkdir -p "$DEST"
cp -a "$OUT_DIR/." "$DEST/"

echo "Done. Fairy plugin is ready in $DEST"
