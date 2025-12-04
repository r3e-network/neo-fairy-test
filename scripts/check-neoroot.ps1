Param()

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$neoRoot = $env:NEOROOT
if (-not $neoRoot -or $neoRoot -eq "") {
    $neoRoot = $env:NeoRoot
}
if (-not $neoRoot -or $neoRoot -eq "") {
    $neoRoot = Join-Path $root "..\\neo"
}

Write-Host "Checking Neo root at: $neoRoot"

$paths = @(
    Join-Path $neoRoot "src/Neo/Neo.csproj",
    Join-Path $neoRoot "src/Neo.ConsoleService/Neo.ConsoleService.csproj",
    Join-Path $neoRoot "src/Plugins/RpcServer/RpcServer.csproj"
)

$missing = @()
foreach ($p in $paths) {
    if (-not (Test-Path $p)) { $missing += $p }
}

if ($missing.Count -gt 0) {
    Write-Error ("Neo root check failed. Missing:`n" + ($missing -join "`n") + "`nSet NEOROOT or NeoRoot to your neo checkout.")
    exit 1
}

Write-Host "Neo root looks good."
