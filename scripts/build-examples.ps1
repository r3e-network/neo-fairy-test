Param(
    [string] $Configuration = "Release",
    [string] $Nccs = "nccs",
    [string] $NccsVersion = "3.7.4"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command $Nccs -ErrorAction SilentlyContinue)) {
    Write-Error "nccs compiler not found. Install Neo.Compiler.CSharp (dotnet tool), e.g.: `dotnet tool install Neo.Compiler.CSharp -g --version $NccsVersion` or pass -Nccs /path/to/nccs"
    exit 1
}

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dexRoot = Join-Path $root "examples/DexProject"
$outDir = Join-Path $dexRoot "out"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Building FungibleToken (DexProject) -> $outDir ..."
& $Nccs (Join-Path $dexRoot "src/FungibleToken.cs") --manifest (Join-Path $outDir "FungibleToken.manifest.json") --nef (Join-Path $outDir "FungibleToken.nef") --debug --assembly | Out-Null

Write-Host "Building LiquidityPool (DexProject) -> $outDir ..."
& $Nccs (Join-Path $dexRoot "src/LiquidityPool.cs") --manifest (Join-Path $outDir "LiquidityPool.manifest.json") --nef (Join-Path $outDir "LiquidityPool.nef") --debug --assembly | Out-Null

Write-Host "Building Router (DexProject) -> $outDir ..."
& $Nccs (Join-Path $dexRoot "src/Router.cs") --manifest (Join-Path $outDir "Router.manifest.json") --nef (Join-Path $outDir "Router.nef") --debug --assembly | Out-Null

Write-Host "Building Deploy (DexProject) -> $outDir ..."
& $Nccs (Join-Path $dexRoot "src/Deploy.cs") --manifest (Join-Path $outDir "Deploy.manifest.json") --nef (Join-Path $outDir "Deploy.nef") --debug --assembly | Out-Null

Write-Host "Done. Artifacts:"
Get-ChildItem -Path $outDir | ForEach-Object { Write-Host $_.Name }
