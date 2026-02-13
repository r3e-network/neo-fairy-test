Param(
    [string] $Configuration = "Release",
    [string] $Output
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outDir = Join-Path $root "src/Fairy.Plugin/bin/$Configuration/net10.0"
$neoRoot = $env:NEOROOT
if (-not $neoRoot -or $neoRoot -eq "") { $neoRoot = $env:NeoRoot }
if (-not $neoRoot -or $neoRoot -eq "") {
    $neoRoot = Join-Path $root "..\\neo"
}

if (-not $Output -or $Output -eq "") {
    # Neo repo layout changed over time:
    # - Old: <neo>/neo-cli/bin/<cfg>/net10.0
    # - New: <neo>/bin/Neo.CLI/net10.0
    $newLayout = Join-Path $neoRoot "bin/Neo.CLI/net10.0"
    $oldLayout = Join-Path $neoRoot "neo-cli/bin/$Configuration/net10.0"
    $splitLayout = Join-Path $neoRoot "node/src/Neo.CLI/bin/$Configuration/net10.0"
    $splitBinLayout = Join-Path $neoRoot "node/bin/Neo.CLI/net10.0"
    if (Test-Path $newLayout) {
        $Output = Join-Path $newLayout "Plugins/Fairy"
    } elseif (Test-Path $oldLayout) {
        $Output = Join-Path $oldLayout "Plugins/Fairy"
    } elseif (Test-Path $splitBinLayout) {
        $Output = Join-Path $splitBinLayout "Plugins/Fairy"
    } else {
        $Output = Join-Path $splitLayout "Plugins/Fairy"
    }
}

Write-Host "Building Fairy plugin ($Configuration)..."
dotnet build (Join-Path $root "src/Fairy.Plugin/Fairy.csproj") -c $Configuration --nologo -p:NeoRoot=$neoRoot

Write-Host "Copying artifacts to $Output"
New-Item -ItemType Directory -Force -Path $Output | Out-Null
Copy-Item -Path (Join-Path $outDir "*") -Destination $Output -Recurse -Force

Write-Host "Done. Fairy plugin is ready in $Output"
