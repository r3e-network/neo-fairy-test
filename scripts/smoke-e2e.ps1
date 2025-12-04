Param(
    [string] $Configuration = "Release",
    [string] $RpcUrl = "http://127.0.0.1:16868",
    [string] $LogPath
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$neoRoot = $env:NEOROOT
if (-not $neoRoot -or $neoRoot -eq "") { $neoRoot = $env:NeoRoot }
if (-not $neoRoot -or $neoRoot -eq "") { $neoRoot = Join-Path $root "..\\neo" }

$neoCli = Join-Path $neoRoot "neo-cli/bin/$Configuration/net10.0/neo-cli.dll"
if (-not (Test-Path $neoCli)) {
    Write-Error "neo-cli not found at $neoCli. Build neo-cli first."
    exit 1
}

$tmpPlugins = New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetTempPath() + [System.IO.Path]::GetRandomFileName())

if (-not $LogPath -or $LogPath -eq "") {
    $LogPath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "neo-cli.log")
}

function Cleanup {
    if ($process -and -not $process.HasExited) {
        $process.Kill()
    }
    Remove-Item -Recurse -Force $tmpPlugins -ErrorAction SilentlyContinue
}
Register-EngineEvent PowerShell.Exiting -Action { Cleanup }

Write-Host "Packaging Fairy plugin into $tmpPlugins/Fairy ..."
& (Join-Path $root "scripts/package-plugin.ps1") -Configuration $Configuration -Output (Join-Path $tmpPlugins "Fairy") | Out-Null

Write-Host "Starting neo-cli with Fairy (log: $LogPath)..."
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = "dotnet"
$startInfo.Arguments = "`"$neoCli`" --pluginspath `"$tmpPlugins`""
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.UseShellExecute = $false
$startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8
$process = New-Object System.Diagnostics.Process
$process.StartInfo = $startInfo
$process.Start() | Out-Null

# Stream output to file asynchronously
$outStream = New-Object System.IO.StreamWriter($LogPath, $false, [System.Text.Encoding]::UTF8)
$process.BeginOutputReadLine()
$process.BeginErrorReadLine()
$process.add_OutputDataReceived({ param($sender,$args) if ($args.Data) { $outStream.WriteLine($args.Data) } })
$process.add_ErrorDataReceived({ param($sender,$args) if ($args.Data) { $outStream.WriteLine($args.Data) } })

Start-Sleep -Seconds 5

Write-Host "Running HelloFairy smoke against $RpcUrl ..."
try {
    & (Join-Path $root "scripts/smoke-http.sh") $RpcUrl
    Write-Host "Smoke succeeded."
} catch {
    Write-Error "Smoke failed."
    Write-Host "neo-cli log (partial):"
    Get-Content -Path $LogPath -TotalCount 200 | Write-Host
    Cleanup
    exit 1
}

Cleanup
if (Test-Path $LogPath) {
    Remove-Item $LogPath -ErrorAction SilentlyContinue
}
