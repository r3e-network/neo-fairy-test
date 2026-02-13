#!/usr/bin/env bash
set -euo pipefail

RPC_URL="${1:-${FAIRY_RPC_URL:-http://127.0.0.1:16868}}"

request() {
  local method="$1"
  local payload
  payload="{\"jsonrpc\":\"2.0\",\"method\":\"$method\",\"params\":[],\"id\":1}"

  echo "POST $RPC_URL" >&2
  echo "Payload: $payload" >&2
  curl -s -X POST -H "Content-Type: application/json" --data "$payload" "$RPC_URL"
}

response="$(request "helloFairy")"
echo "Response: $response"

if command -v jq >/dev/null 2>&1; then
  echo "$response" | jq .
  if echo "$response" | jq -e '.error.code == -32601' >/dev/null; then
    response="$(request "hellofairy")"
    echo "Response: $response"
    echo "$response" | jq .
  fi
  if echo "$response" | jq -e '.error' >/dev/null; then exit 1; fi
else
  if [[ "$response" == *"\"error\""* ]]; then exit 1; fi
fi
