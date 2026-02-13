Param(
    [string] $RpcUrl = $env:FAIRY_RPC_URL
)

$ErrorActionPreference = "Stop"

if (-not $RpcUrl -or $RpcUrl -eq "") {
    $RpcUrl = "http://127.0.0.1:16868"
}

function Invoke-Rpc([string] $Method) {
    $payload = @{ jsonrpc = "2.0"; method = $Method; params = @(); id = 1 } | ConvertTo-Json -Compress
    Write-Host "POST $RpcUrl"
    Write-Host "Payload: $payload"

    $response = Invoke-WebRequest -Uri $RpcUrl -Method Post -ContentType "application/json" -Body $payload
    $content = $response.Content

    Write-Host "Response: $content"
    return $content | ConvertFrom-Json
}

$result = Invoke-Rpc "helloFairy"

if ($result.error -and $result.error.code -eq -32601) {
    $result = Invoke-Rpc "hellofairy"
}

if ($result.error) {
    throw ("RPC Error: " + ($result.error | ConvertTo-Json -Compress))
}

