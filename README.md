Fairy is a Neo N3 RpcServer plugin for fast, repeatable contract testing, simulation, and debugging. It forks chain state into per-session snapshots so you can deploy, invoke, and debug contracts without touching mainnet/testnet until you choose to relay transactions.

## Quick start (dev)
- Ensure .NET 10 SDK is installed (`global.json` pins 10.0.x).
- Clone the Neo mono-repo alongside this repo (default `../neo`), or set `NEOROOT=/path/to/neo`.
- Build & test everything: `dotnet test Fairy.Full.sln` (or `scripts/test-all.sh`).
- Package the plugin into your neo-cli: `scripts/package-plugin.sh -c Release` (override target with `-o /path/to/neo-cli/bin/Release/net10.0/Plugins/Fairy`).
- Run neo-cli and hit `HelloFairy`: `scripts/smoke-http.sh http://127.0.0.1:16868` (or `scripts/smoke-e2e.sh` to start neo-cli in-process if it’s built).
- Build example NEF/manifest (needs `nccs`): `scripts/build-examples.sh` (outputs to `examples/DexProject/out`).

## Repository layout
- `src/Neo.Fairy.Core` – shared abstractions and models.
- `src/Neo.Fairy.Engine` – execution helpers.
- `src/Neo.Fairy.Testing` – test harness utilities.
- `src/Neo.Fairy.Cli` – lightweight CLI surface.
- `src/Fairy.Plugin` – RpcServer plugin (DLL + configs).
- `tests/*` – unit tests for core, engine, testing.
- `examples/` – sample workspace (`examples/DexProject`).
- `scripts/test-all.{sh,ps1}` – convenience wrappers.
- `scripts/package-plugin.{sh,ps1}` – build + copy the plugin into a neo-cli Plugins/Fairy folder.
- `scripts/check-neoroot.{sh,ps1}` – verify the Neo checkout path for local/CI builds.
- `scripts/smoke-http.sh` – minimal HelloFairy RPC check against a running neo-cli+Fairy instance.
- `scripts/smoke-e2e.{sh,ps1}` – boots neo-cli with packaged Fairy plugin and runs the HelloFairy smoke.
- `scripts/build-examples.{sh,ps1}` – compile the sample contracts (requires `nccs`) into `examples/**/out/`.
- Network targeting: `--network mainnet|testnet|neo-express|<rpc-url>` on deploy; env overrides `FAIRY_MAINNET_RPC`, `FAIRY_TESTNET_RPC`, `FAIRY_EXPRESS_RPC`; `fairy.toml` supports `mainnet_rpc`, `testnet_rpc`, `neoexpress_rpc`.
- `Fairy.Full.sln` – single entry-point solution for all projects and tests.
- `examples/` – ready-to-adapt sample workspace (see “Examples” below).

## Prerequisites
- .NET SDK 10.0.x (pinned via `global.json`; roll-forward allowed within 10.x).
- Neo mono-repo checked out (defaults to `../neo` relative to this repo). Override with MSBuild property `NeoRoot=/path/to/neo` or environment variable `NEOROOT`.

`Directory.Build.props` wires `NeoRoot` for all projects. `Fairy.Full.sln` already references the Neo projects from that root.

## Build & test
- Full build + tests: `dotnet test Fairy.Full.sln` (or `scripts/test-all.sh` / `scripts/test-all.ps1`).
- Validate your Neo checkout location (optional): `scripts/check-neoroot.sh` (or `.ps1`).
- Plugin only: `dotnet build src/Fairy.Plugin/Fairy.csproj`.

## Running in neo-cli
1. Build the plugin: `dotnet build src/Fairy.Plugin/Fairy.csproj -c Release`.
2. Copy `src/Fairy.Plugin/bin/Release/net10.0/{Fairy.dll,fairy.json,RpcServer.json}` into `neo-cli/bin/Release/net10.0/Plugins/Fairy/` (use `Debug` if you built Debug).
3. Start `neo-cli`; Fairy RPC endpoints and the WebSocket listener (RPC port + 1) will be available automatically.

`fairy.json` ships with a default testing wallet; adjust if you need different keys. `RpcServer.json` mirrors the usual RpcServer options.

Tip: use `scripts/package-plugin.sh -c Release` (or `.ps1`) to build and copy everything from `src/Fairy.Plugin/bin/<config>/net10.0` to a neo-cli Plugins/Fairy folder (defaults to `../neo/neo-cli/...`; override with `-o`).

Quick smoke (requires Fairy running): `scripts/smoke-http.sh http://127.0.0.1:16868` (or set `FAIRY_RPC_URL`). This sends `HelloFairy` and prints the response (pretty-prints if `jq` is installed).

End-to-end smoke (requires built neo-cli at NeoRoot): `CONFIG=Release RPC_URL=http://127.0.0.1:16868 scripts/smoke-e2e.sh` (or `.ps1`). It packages the plugin into a temp Plugins/Fairy, starts neo-cli with `--pluginspath`, runs the HelloFairy smoke, and cleans up.

## Examples
- `examples/DexProject` is a fully wired workspace:
  - Contracts: NEP-17 `FungibleToken`, `LiquidityPool`, `Router`, and a stub `Deploy` script; dependencies declared in `fairy.toml`.
  - Tests: rich `FungibleToken.Test.cs` exercising transfers, mint/burn, fuzzing, cheatcodes (warp/snapshot/revert), event assertions, and gas checks.
  - Scripts: deployment/interaction snippets under `examples/DexProject/script`.
  - Workspace metadata: `fairy.toml` shows how to lay out `src/`, `test/`, and contract aliases.
- Build the sample NEF/manifest (requires `nccs`): `scripts/build-examples.sh` (or `.ps1`) produces NEF/manifest for all three contracts in `examples/DexProject/out/`. Point `scripts/workspace_client.py` at those outputs for quick deploy/invoke.
- Run the workspace client (accepts paths or base64):
  - Virtual deploy + invoke: `python scripts/workspace_client.py --workspace dex --alias token --nef out/FungibleToken.nef --manifest out/FungibleToken.manifest.json --operation symbol`
  - Batch invoke: add `--batch '[["token","mint",[{"type":"Integer","value":"100"}]],["router","addLiquidity",[]]]'`
  - Relay to chain: add `--relay-deploy` / `--relay-invoke`.
  - Fund GAS for a session up front: `--set-gas <account-hash>`.
- Build your own NEFs: compile with `nccs` (or your compiler) to produce `.nef` + `manifest.json` into `examples/DexProject/out/`, then point the client at those files.
- Prebuilt NEF/manifest files are not checked in; build locally with the scripts above to match your compiler/version.

### Example workflow: DexProject (virtual)
1) Install the compiler (align with your target Neo/neo-cli; defaults assume `Neo.Compiler.CSharp` 3.7.4): for example `dotnet tool install Neo.Compiler.CSharp -g --version 3.7.4` (or set `NCCS=/path/to/nccs` for the scripts; override version with `NCCS_VERSION`/`NccsVersion`).
2) Build contracts: `scripts/build-examples.sh` (outputs to `examples/DexProject/out/`).
3) Start your Fairy-enabled neo-cli (or use `scripts/smoke-e2e.sh` to boot a temp instance). Pick networks with `--network mainnet|testnet|neo-express|<rpc-url>`; defaults can be overridden via `FAIRY_MAINNET_RPC` / `FAIRY_TESTNET_RPC` / `FAIRY_EXPRESS_RPC` or `fairy.toml` (`mainnet_rpc`/`testnet_rpc`/`neoexpress_rpc`).
4) Virtual deploy the token and call a method:
   ```
   python scripts/workspace_client.py --workspace dex --alias token \
     --nef examples/DexProject/out/FungibleToken.nef \
     --manifest examples/DexProject/out/FungibleToken.manifest.json \
     --operation symbol
   ```
5) Try a batch including router/pool once you’ve built them:
   ```
   python scripts/workspace_client.py --workspace dex --alias router \
     --nef examples/DexProject/out/Router.nef \
     --manifest examples/DexProject/out/Router.manifest.json \
     --batch '[["token","mint",[{"type":"Integer","value":"100"}]],["router","AddLiquidity",[]]]'
   ```
Adjust RPC target with `--rpc-url` or `FAIRY_RPC_URL` as needed.

## Quick RPC workflow
Use any HTTP client against your RpcServer (e.g., `http://localhost:10332`):

- Health: `{"jsonrpc":"2.0","method":"HelloFairy","params":[],"id":1}`
- Seed GAS for a session: `SetGasBalance("mysession","<account-hash>",10000000000)`
- Virtual deploy (no on-chain writes): `VirtualDeploy("mysession","<nef-base64>","<manifest-json>",[])`
- Invoke against the snapshot: `InvokeFunctionWithSession("mysession",true,"<contract-hash>","methodName",[{"type":"String","value":"hi"}],[{"account":"<hash>","scopes":"CalledByEntry"}])`
- Relay to chain when ready: `RelayDeployContract` / `RelayInvokeFunction` (requires a wallet for signing/fee calc).

## Debugging
- Register debug metadata: `SetDebugInfo(contractHash, nefdbgnfoBase64Zip, dumpnefTxt)`.
- Set breakpoints: `SetSourceCodeBreakpoints([contractHash,"File.cs","42",...])`.
- Run + step: `DebugFunctionWithSession`, `DebugStepOver`, `DebugStepInto`, `DebugStepOut`.
- Inspect: `GetLocalVariables`, `GetArguments`, `GetEvaluationStack`, `GetVariableValueByName`.
Debug sessions fork the snapshot and never mutate it.

## Workspaces (bundle contracts)
Register multiple contracts by alias with `UpsertWorkspaceContract`, then:
- Virtual deploy bundle: `VirtualDeployWorkspace(workspace, session, aliasFilter?)`
- Relay bundle: `RelayDeployWorkspace(workspace, session|null, aliasFilter?)`
- Invoke by alias: `InvokeWorkspaceFunctionWithSession(workspace, alias, session, writeSnapshot, op, args?, signers?)`
- Batch invoke: `InvokeWorkspaceManyWithSession(workspace, session, writeSnapshot, [[alias, op, args?], ...], signers?)`

See `examples/DexProject` and `scripts/workspace_client.py` for a minimal workspace client (requires Python `requests` and `websockets`).

## Tips & troubleshooting
- Snapshots are keyed by session; reuse a session to accumulate state or clone it to branch tests.
- `SetSnapshotTimestamp` and `SetSnapshotRandom` let you control `Runtime.Time` and randomness per session.
- Network fee estimation and relaying require a wallet (default fairy wallet or `SetSessionFairyWalletWithNep2/Wif`).
- WebSocket subscribe example: connect to `ws://<rpc-host>:<rpc-port+1>` and send `{"jsonrpc":"2.0","method":"subscribecommittedblock","params":[],"needresponse":true,"id":1}`.

## Development notes
- Solution already references the upstream Neo projects; no extra NuGet packages are needed when building against `$(NeoRoot)`.
- CI (`.github/workflows/dotnet.yml`) restores/builds/tests `Fairy.Full.sln` on push/PR. An optional `smoke-e2e` job can be triggered via the workflow dispatch input `run-e2e=true`; it builds neo-cli, packages the plugin, runs the HelloFairy smoke, and uploads the neo-cli log on failure.
