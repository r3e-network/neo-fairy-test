Param()

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$neoRoot = $env:NEOROOT
if (-not $neoRoot -or $neoRoot -eq "") {
    $neoRoot = $env:NeoRoot
}
if (-not $neoRoot -or $neoRoot -eq "") {
    $candidate = Join-Path $root "neo_csharp"
    if (Test-Path $candidate) {
        $neoRoot = $candidate
    } else {
        $neoRoot = Join-Path $root "..\\neo"
    }
}

Write-Host "Checking Neo root at: $neoRoot"

if ((Test-Path (Join-Path $neoRoot "src/Neo/Neo.csproj")) -and
    (Test-Path (Join-Path $neoRoot "src/Neo.ConsoleService/Neo.ConsoleService.csproj")) -and
    (Test-Path (Join-Path $neoRoot "src/Plugins/RpcServer/RpcServer.csproj"))) {
    Write-Host "Neo sources look good."
    exit 0
}

if ((Test-Path (Join-Path $neoRoot "core/src/Neo/Neo.csproj")) -and
    (Test-Path (Join-Path $neoRoot "node/src/Neo.ConsoleService/Neo.ConsoleService.csproj")) -and
    (Test-Path (Join-Path $neoRoot "node/plugins/RpcServer/RpcServer.csproj"))) {
    Write-Host "Neo sources look good."
    exit 0
}

Write-Host "Neo source projects missing; checking built binaries..."

$neoCliCandidates = @(
    Join-Path $neoRoot "bin/Neo.CLI/net10.0/neo-cli.dll",
    Join-Path $neoRoot "neo-cli/bin/Release/net10.0/neo-cli.dll",
    Join-Path $neoRoot "neo-cli/bin/Debug/net10.0/neo-cli.dll",
    Join-Path $neoRoot "node/bin/Neo.CLI/net10.0/neo-cli.dll",
    Join-Path $neoRoot "node/src/Neo.CLI/bin/Release/net10.0/neo-cli.dll",
    Join-Path $neoRoot "node/src/Neo.CLI/bin/Debug/net10.0/neo-cli.dll"
)

$rpcServerCandidates = @(
    Join-Path $neoRoot "bin/Neo.Plugins.RpcServer/net10.0/RpcServer.dll",
    Join-Path $neoRoot "bin/Neo.Plugins.RpcServer/net9.0/RpcServer.dll",
    Join-Path $neoRoot "node/bin/Neo.Plugins.RpcServer/net10.0/RpcServer.dll",
    Join-Path $neoRoot "node/plugins/RpcServer/bin/Release/net10.0/RpcServer.dll",
    Join-Path $neoRoot "node/plugins/RpcServer/bin/Debug/net10.0/RpcServer.dll"
)

$neoCli = $neoCliCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$rpcServer = $rpcServerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

$missing = @()
if (-not $neoCli) {
    $missing += "neo-cli.dll not found under bin/Neo.CLI/net10.0, neo-cli/bin/<cfg>/net10.0, or node/src/Neo.CLI/bin/<cfg>/net10.0"
}
if (-not $rpcServer) {
    $missing += "RpcServer.dll not found under bin/Neo.Plugins.RpcServer/net10.0 or node/plugins/RpcServer/bin/<cfg>/net10.0"
}

if ($missing.Count -gt 0) {
    Write-Error ("Neo root check failed:`n" + ($missing -join "`n") + "`nSet NEOROOT/NeoRoot and build Neo.CLI + RpcServer.")
    exit 1
}

Write-Host "Neo binaries look good."
