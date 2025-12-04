#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NCCS="${NCCS:-nccs}"
NCCS_VERSION="${NCCS_VERSION:-3.7.4}"
CONFIG="${CONFIG:-Release}"

if ! command -v "$NCCS" >/dev/null 2>&1; then
  echo "nccs compiler not found. Install Neo.Compiler.CSharp (dotnet tool) e.g.:" >&2
  echo "  dotnet tool install Neo.Compiler.CSharp -g --version $NCCS_VERSION" >&2
  echo "or set NCCS=/path/to/nccs" >&2
  exit 1
fi

DEX_ROOT="$ROOT/examples/DexProject"
OUT="$DEX_ROOT/out"
mkdir -p "$OUT"

echo "Building FungibleToken (DexProject) -> $OUT ..."
"$NCCS" "$DEX_ROOT/src/FungibleToken.cs" --manifest "$OUT/FungibleToken.manifest.json" --nef "$OUT/FungibleToken.nef" --debug --assembly >/dev/null

echo "Building LiquidityPool (DexProject) -> $OUT ..."
"$NCCS" "$DEX_ROOT/src/LiquidityPool.cs" --manifest "$OUT/LiquidityPool.manifest.json" --nef "$OUT/LiquidityPool.nef" --debug --assembly >/dev/null

echo "Building Router (DexProject) -> $OUT ..."
"$NCCS" "$DEX_ROOT/src/Router.cs" --manifest "$OUT/Router.manifest.json" --nef "$OUT/Router.nef" --debug --assembly >/dev/null

echo "Building Deploy (DexProject) -> $OUT ..."
"$NCCS" "$DEX_ROOT/src/Deploy.cs" --manifest "$OUT/Deploy.manifest.json" --nef "$OUT/Deploy.nef" --debug --assembly >/dev/null

echo "Done. Artifacts:"
ls -1 "$OUT"
