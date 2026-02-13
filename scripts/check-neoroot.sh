#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NEO_ROOT="${NEOROOT:-${NeoRoot:-}}"
if [[ -z "$NEO_ROOT" ]]; then
  NEO_ROOT="$ROOT/../neo"
fi

echo "Checking Neo root at: $NEO_ROOT"

NEO_CSProj="$NEO_ROOT/src/Neo/Neo.csproj"
NEO_CONSOLE="$NEO_ROOT/src/Neo.ConsoleService/Neo.ConsoleService.csproj"
RPC_SERVER="$NEO_ROOT/src/Plugins/RpcServer/RpcServer.csproj"
NEO_CORE="$NEO_ROOT/core/src/Neo/Neo.csproj"
NEO_CONSOLE_NODE="$NEO_ROOT/node/src/Neo.ConsoleService/Neo.ConsoleService.csproj"
RPC_SERVER_NODE="$NEO_ROOT/node/plugins/RpcServer/RpcServer.csproj"

if [[ -f "$NEO_CSProj" && -f "$NEO_CONSOLE" && -f "$RPC_SERVER" ]]; then
  echo "Neo sources look good."
  exit 0
fi

if [[ -f "$NEO_CORE" && -f "$NEO_CONSOLE_NODE" && -f "$RPC_SERVER_NODE" ]]; then
  echo "Neo sources look good."
  exit 0
fi

echo "Neo source projects missing; checking built binaries..."

NEO_CLI_DLL=""
if [[ -f "$NEO_ROOT/bin/Neo.CLI/net10.0/neo-cli.dll" ]]; then
  NEO_CLI_DLL="$NEO_ROOT/bin/Neo.CLI/net10.0/neo-cli.dll"
elif [[ -f "$NEO_ROOT/neo-cli/bin/Release/net10.0/neo-cli.dll" ]]; then
  NEO_CLI_DLL="$NEO_ROOT/neo-cli/bin/Release/net10.0/neo-cli.dll"
elif [[ -f "$NEO_ROOT/neo-cli/bin/Debug/net10.0/neo-cli.dll" ]]; then
  NEO_CLI_DLL="$NEO_ROOT/neo-cli/bin/Debug/net10.0/neo-cli.dll"
elif [[ -f "$NEO_ROOT/node/bin/Neo.CLI/net10.0/neo-cli.dll" ]]; then
  NEO_CLI_DLL="$NEO_ROOT/node/bin/Neo.CLI/net10.0/neo-cli.dll"
elif [[ -f "$NEO_ROOT/node/src/Neo.CLI/bin/Release/net10.0/neo-cli.dll" ]]; then
  NEO_CLI_DLL="$NEO_ROOT/node/src/Neo.CLI/bin/Release/net10.0/neo-cli.dll"
elif [[ -f "$NEO_ROOT/node/src/Neo.CLI/bin/Debug/net10.0/neo-cli.dll" ]]; then
  NEO_CLI_DLL="$NEO_ROOT/node/src/Neo.CLI/bin/Debug/net10.0/neo-cli.dll"
fi

RPC_SERVER_DLL=""
if [[ -f "$NEO_ROOT/bin/Neo.Plugins.RpcServer/net10.0/RpcServer.dll" ]]; then
  RPC_SERVER_DLL="$NEO_ROOT/bin/Neo.Plugins.RpcServer/net10.0/RpcServer.dll"
elif [[ -f "$NEO_ROOT/bin/Neo.Plugins.RpcServer/net9.0/RpcServer.dll" ]]; then
  RPC_SERVER_DLL="$NEO_ROOT/bin/Neo.Plugins.RpcServer/net9.0/RpcServer.dll"
elif [[ -f "$NEO_ROOT/node/bin/Neo.Plugins.RpcServer/net10.0/RpcServer.dll" ]]; then
  RPC_SERVER_DLL="$NEO_ROOT/node/bin/Neo.Plugins.RpcServer/net10.0/RpcServer.dll"
elif [[ -f "$NEO_ROOT/node/plugins/RpcServer/bin/Release/net10.0/RpcServer.dll" ]]; then
  RPC_SERVER_DLL="$NEO_ROOT/node/plugins/RpcServer/bin/Release/net10.0/RpcServer.dll"
elif [[ -f "$NEO_ROOT/node/plugins/RpcServer/bin/Debug/net10.0/RpcServer.dll" ]]; then
  RPC_SERVER_DLL="$NEO_ROOT/node/plugins/RpcServer/bin/Debug/net10.0/RpcServer.dll"
fi

missing=0
if [[ -z "$NEO_CLI_DLL" ]]; then
  echo "Missing neo-cli build output (neo-cli.dll). Expected under $NEO_ROOT/bin/Neo.CLI/net10.0, $NEO_ROOT/neo-cli/bin/<cfg>/net10.0, or $NEO_ROOT/node/src/Neo.CLI/bin/<cfg>/net10.0" >&2
  missing=$((missing + 1))
fi

if [[ -z "$RPC_SERVER_DLL" ]]; then
  echo "Missing RpcServer plugin build output (RpcServer.dll). Expected under $NEO_ROOT/bin/Neo.Plugins.RpcServer/net10.0 or $NEO_ROOT/node/plugins/RpcServer/bin/<cfg>/net10.0" >&2
  missing=$((missing + 1))
fi

if [[ $missing -ne 0 ]]; then
  echo "Neo root check failed. Set NEOROOT/NeoRoot to your neo checkout and build Neo.CLI + RpcServer." >&2
  exit 1
fi

echo "Neo binaries look good."
