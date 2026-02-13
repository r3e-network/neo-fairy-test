#!/usr/bin/env python3
"""
Example workspace client for Fairy RPC.

Flow (all over HTTP RPC):
  1) UpsertWorkspaceContract (alias -> NEF/manifest, optional data/signers)
  2) VirtualDeployWorkspace for the alias (into a session snapshot)
  3) Optional InvokeWorkspaceFunctionWithSession using the alias

Prereqs:
  - Fairy plugin running locally (default RPC http://127.0.0.1:16868)
  - NEF+manifest for the contract you want to deploy

Notes:
  - You must provide NEF (base64 or path) and manifest (JSON string or path).
  - Invocation is optional; skip by omitting --operation.
  - For relay deployments/invokes, swap the methods to Relay* in the code below.
"""

import argparse
import base64
import json
import os
import sys
from pathlib import Path

import requests


def load_base64_or_file(value: str) -> str:
    path = Path(value)
    if path.exists():
        raw = path.read_bytes()
        return base64.b64encode(raw).decode()
    # treat as already-base64
    base64.b64decode(value)  # raises if invalid
    return value


def load_json_or_file(value: str):
    path = Path(value)
    if path.exists():
        return json.loads(path.read_text())
    return json.loads(value)


def rpc(rpc_url: str, method: str, params):
    candidates = [method]
    lower = method.lower()
    if lower != method:
        candidates.append(lower)

    last_error = None
    for m in candidates:
        payload = {"jsonrpc": "2.0", "method": m, "params": params, "id": 1}
        resp = requests.post(rpc_url, json=payload, timeout=10)
        resp.raise_for_status()
        data = resp.json()
        if "error" in data:
            last_error = data["error"]
            if last_error.get("code") == -32601 and m != lower:
                continue
            raise RuntimeError(f"RPC error {m}: {last_error}")
        return data["result"]

    raise RuntimeError(f"RPC error {method}: {last_error}")


def main():
    parser = argparse.ArgumentParser(description="Workspace deploy/invoke helper for Fairy RPC")
    parser.add_argument("--rpc-url", default=os.environ.get("FAIRY_RPC_URL", "http://127.0.0.1:16868"))
    parser.add_argument("--workspace", default="workspace", help="Workspace name/namespace")
    parser.add_argument("--alias", required=True, help="Alias inside the workspace")
    parser.add_argument("--session", default="workspace-session", help="Fairy session name for virtual deploy/invoke")
    parser.add_argument("--relay-deploy", action="store_true", help="Use RelayDeployWorkspace instead of virtual deploy")
    parser.add_argument("--relay-invoke", action="store_true", help="Use RelayInvokeWorkspaceFunction/Many instead of virtual invoke")
    parser.add_argument("--nef", required=True, help="NEF base64 string or path to .nef file")
    parser.add_argument("--manifest", required=True, help="Manifest JSON string or path to manifest.json")
    parser.add_argument("--data", help="Optional deploy data (ContractParameter JSON or path)")
    parser.add_argument("--signers", help="Default signers JSON array for deploy (or path)")
    parser.add_argument("--operation", help="Method name to invoke after deploy (optional)")
    parser.add_argument("--args", help="Invocation args as JSON array (or path)")
    parser.add_argument(
        "--batch",
        nargs="+",
        help="Batch calls as JSON strings or paths, each like '[\"alias\",\"op\",[args...]]'. Overrides --operation/--args.",
    )
    parser.add_argument("--invoke-signers", help="Invocation signers JSON array (or path)")
    parser.add_argument("--set-gas", help="Optional account to fund via SetGasBalance before deploy")
    args = parser.parse_args()

    rpc_url = args.rpc_url
    workspace = args.workspace
    alias = args.alias
    session = args.session

    nef_b64 = load_base64_or_file(args.nef)
    manifest_json = load_json_or_file(args.manifest)
    data_param = load_json_or_file(args.data) if args.data else None
    signers = load_json_or_file(args.signers) if args.signers else None
    invoke_signers = load_json_or_file(args.invoke_signers) if args.invoke_signers else None
    invoke_args = load_json_or_file(args.args) if args.args else []
    batch_calls = [load_json_or_file(item) for item in args.batch] if args.batch else None

    if args.set_gas:
        print("SetGasBalance:", rpc(rpc_url, "setGasBalance", [session, args.set_gas, 10_0000_0000]))

    print("UpsertWorkspaceContract...")
    upsert_params = [workspace, alias, nef_b64, json.dumps(manifest_json)]
    if data_param is not None:
        upsert_params.append(data_param)
    if signers is not None:
        # ensure the data slot exists even if null
        if data_param is None:
            upsert_params.append(None)
        upsert_params.append(signers)
    print(rpc(rpc_url, "upsertWorkspaceContract", upsert_params))

    if args.relay_deploy:
        print("RelayDeployWorkspace...")
        deploy_params = [workspace, None, [alias]]
        if signers is not None:
            deploy_params.append(signers)
        print(rpc(rpc_url, "relayDeployWorkspace", deploy_params))
    else:
        print("VirtualDeployWorkspace...")
        deploy_params = [workspace, session, [alias]]
        print(rpc(rpc_url, "virtualDeployWorkspace", deploy_params))

    if batch_calls:
        if args.relay_invoke:
            print("RelayInvokeWorkspaceMany...")
            invoke_params = [workspace, None, batch_calls]
            if invoke_signers is not None:
                invoke_params.append(invoke_signers)
            print(rpc(rpc_url, "relayInvokeWorkspaceMany", invoke_params))
        else:
            print("InvokeWorkspaceManyWithSession...")
            invoke_params = [workspace, session, True, batch_calls]
            if invoke_signers is not None:
                invoke_params.append(invoke_signers)
            print(rpc(rpc_url, "invokeWorkspaceManyWithSession", invoke_params))
    elif args.operation:
        if args.relay_invoke:
            print("RelayInvokeWorkspaceFunction...")
            invoke_params = [workspace, alias, None, args.operation, invoke_args]
            if invoke_signers is not None:
                invoke_params.append(invoke_signers)
            print(rpc(rpc_url, "relayInvokeWorkspaceFunction", invoke_params))
        else:
            print("InvokeWorkspaceFunctionWithSession...")
            invoke_params = [workspace, alias, session, True, args.operation, invoke_args]
            if invoke_signers is not None:
                invoke_params.append(invoke_signers)
            print(rpc(rpc_url, "invokeWorkspaceFunctionWithSession", invoke_params))
    else:
        print("Invocation skipped (no --operation provided).")


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print("Workspace client failed:", e, file=sys.stderr)
        sys.exit(1)
