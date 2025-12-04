#!/usr/bin/env bash
set -euo pipefail

RPC_URL="${1:-${FAIRY_RPC_URL:-http://127.0.0.1:16868}}"

payload='{"jsonrpc":"2.0","method":"HelloFairy","params":[],"id":1}'

echo "POST $RPC_URL"
echo "Payload: $payload"

response="$(curl -s -X POST -H "Content-Type: application/json" --data "$payload" "$RPC_URL")"
echo "Response: $response"

if command -v jq >/dev/null 2>&1; then
  echo "$response" | jq .
fi
