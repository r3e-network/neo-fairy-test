#!/usr/bin/env python3
"""
Minimal smoke test client for Fairy RPC/WebSocket.

Prerequisites:
  - Fairy plugin running on localhost with RPC at http://127.0.0.1:16868
  - Neo-cli synced enough to invoke/read state

What it does:
  1) HelloFairy to confirm server is up
  2) Create a session and set GAS balance
  3) Invoke a dummy script (empty script) with session persistence
  4) Subscribe to committed blocks over WebSocket and read one message
"""

import asyncio
import base64
import json
import sys

try:
    import websockets  # type: ignore
except ImportError:
    websockets = None
import requests


RPC_URL = "http://127.0.0.1:16868"
WS_URL = "ws://127.0.0.1:16869"
SESSION = "smoke-session"


def rpc(method, params):
    candidates = [method]
    lower = method.lower()
    if lower != method:
        candidates.append(lower)

    last_error = None
    for m in candidates:
        payload = {"jsonrpc": "2.0", "method": m, "params": params, "id": 1}
        resp = requests.post(RPC_URL, json=payload, timeout=5)
        resp.raise_for_status()
        data = resp.json()

        if "error" in data:
            last_error = data["error"]
            # Retry with lowercase if server expects all-lowercase method names.
            if last_error.get("code") == -32601 and m != lower:
                continue
            raise RuntimeError(f"RPC error {m}: {last_error}")

        return data.get("result")

    raise RuntimeError(f"RPC error {method}: {last_error}")


async def ws_subscribe():
    if websockets is None:
        print("Skipping WebSocket step: install `websockets` to enable.")
        return
    async with websockets.connect(WS_URL) as ws:
        await ws.send(
            json.dumps(
                {
                    "jsonrpc": "2.0",
                    "method": "subscribecommittedblock",
                    "params": [],
                    "needresponse": True,
                    "id": 1,
                }
            )
        )
        # read subscription ack + first push
        for _ in range(2):
            msg = await ws.recv()
            print("WS message:", msg)


def main():
    print("HelloFairy:", rpc("helloFairy", []))
    dummy_hash = "0xd2a4cff31913016155e38e474a2c06d08be276cf"  # GAS hash, just for a harmless call
    account = "Nf2NECZk8ahGkq8zUzYoEtKFUfRyXnZot5"  # any valid address; not used for real signing
    print("SetGasBalance:", rpc("setGasBalance", [SESSION, account, 10_0000_0000]))

    # Empty script invoke (Base64 of 0x40 RET)
    script_b64 = base64.b64encode(bytes.fromhex("40")).decode()
    print(
        "InvokeScriptWithSession:",
        rpc("invokeScriptWithSession", [SESSION, True, script_b64, None, None]),
    )
    asyncio.run(ws_subscribe())


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("Smoke test failed:", e, file=sys.stderr)
        sys.exit(1)
