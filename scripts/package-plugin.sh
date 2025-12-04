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

if [[ -z "$DEST" ]]; then
  DEST="$ROOT/../neo/neo-cli/bin/$CONFIG/net10.0/Plugins/Fairy"
fi

echo "Building Fairy plugin ($CONFIG)..."
dotnet build "$ROOT/src/Fairy.Plugin/Fairy.csproj" -c "$CONFIG" --nologo

echo "Copying artifacts to: $DEST"
mkdir -p "$DEST"
cp -a "$OUT_DIR/." "$DEST/"

echo "Done. Fairy plugin is ready in $DEST"
