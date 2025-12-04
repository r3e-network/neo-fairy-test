Param(
    [string] $Configuration = "Release",
    [string] $Output
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outDir = Join-Path $root "src/Fairy.Plugin/bin/$Configuration/net10.0"

if (-not $Output -or $Output -eq "") {
    $Output = Join-Path $root "../neo/neo-cli/bin/$Configuration/net10.0/Plugins/Fairy"
}

Write-Host "Building Fairy plugin ($Configuration)..."
dotnet build (Join-Path $root "src/Fairy.Plugin/Fairy.csproj") -c $Configuration --nologo

Write-Host "Copying artifacts to $Output"
New-Item -ItemType Directory -Force -Path $Output | Out-Null
Copy-Item -Path (Join-Path $outDir "*") -Destination $Output -Recurse -Force

Write-Host "Done. Fairy plugin is ready in $Output"
