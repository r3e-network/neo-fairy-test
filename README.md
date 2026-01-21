<p align="center">
  <img src="assets/logo.svg" alt="Neo Fairy" width="200"/>
</p>

<h1 align="center">Neo Fairy</h1>

<p align="center">
  <strong>A professional smart contract development, testing, and deployment framework for Neo N3</strong>
</p>

<p align="center">
  <a href="https://github.com/r3e-network/neo-fairy-test/actions"><img src="https://github.com/r3e-network/neo-fairy-test/actions/workflows/dotnet.yml/badge.svg" alt="Build Status"></a>
  <a href="https://github.com/r3e-network/neo-fairy-test/blob/master/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10"></a>
</p>

---

Neo Fairy is a Neo N3 RpcServer plugin for fast, repeatable contract testing, simulation, and debugging. Inspired by [Foundry](https://github.com/foundry-rs/foundry), it provides a professional-grade development experience for Neo N3 C# smart contracts.

## Key Features

- **Session-based Snapshots** - Fork chain state into isolated per-session snapshots for safe testing
- **Foundry-style Testing** - Write tests with familiar patterns: `Deploy()`, `Call()`, `Vm.Prank()`, `Vm.Warp()`
- **Contract Interface Generation** - Auto-generate type-safe C# wrappers from contract manifests
- **Integrated Debugging** - Set breakpoints, step through code, inspect variables in real-time
- **Workspace Management** - Bundle and deploy multiple contracts with dependency tracking

## Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        Neo Fairy Framework                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
│   │  fairy init  │───▶│  fairy build │───▶│  fairy test  │     │
│   └──────────────┘    └──────────────┘    └──────────────┘     │
│          │                   │                   │              │
│          ▼                   ▼                   ▼              │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
│   │  fairy.toml  │    │  Generated/  │    │  TestRunner  │     │
│   │  Project     │    │  *.g.cs      │    │  + Cheatcodes│     │
│   └──────────────┘    └──────────────┘    └──────────────┘     │
│                                                                  │
│   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
│   │ fairy deploy │───▶│  fairy call  │───▶│  fairy debug │     │
│   └──────────────┘    └──────────────┘    └──────────────┘     │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Architecture

| Module | Description |
|--------|-------------|
| `Neo.Fairy.Core` | Shared abstractions, models, and configuration |
| `Neo.Fairy.Engine` | RPC client and execution adapters |
| `Neo.Fairy.Testing` | Test framework with assertions and cheatcodes |
| `Neo.Fairy.Deployment` | Deployment scripting and contract management |
| `Neo.Fairy.Cli` | Command-line interface (`fairy` commands) |
| `Fairy.Plugin` | Neo RpcServer plugin for neo-cli |

## Quickstart

### Prerequisites

- .NET SDK 10.0.x (pinned via `global.json`)
- Neo source checkout (defaults to `./neo_csharp` if present, or `../neo`; override with `NEOROOT`)

### Installation

```bash
# Clone the repository
git clone https://github.com/r3e-network/neo-fairy-test.git
cd neo-fairy-test

# Build everything
dotnet build Fairy.Full.sln

# Run tests
dotnet test Fairy.Full.sln
```

### Quick Start (Plugin)

```bash
# Start neo-cli with Fairy (auto-builds + installs the plugin)
# Requires a Neo repo checkout (set NEOROOT if not ./neo_csharp or ../neo)
fairy node start --network mainnet --host 127.0.0.1 --port 16868

# Verify it's working (in another terminal)
scripts/smoke-http.sh http://127.0.0.1:16868

# (Optional) Install the plugin without starting the node:
fairy plugin install --network mainnet --host 127.0.0.1 --port 16868

# Check install status:
fairy plugin status
```

## Documentation

### Writing Tests

```csharp
public class CounterTest : FairyTest
{
    public void TestIncrement()
    {
        // Deploy contract and get typed wrapper
        var counter = DeployAndBind<Counter>("counter");

        // Verify initial state
        Assert.Equal(0, counter.GetCount());

        // Call contract method
        counter.Increment();

        // Verify state changed
        Assert.Equal(1, counter.GetCount());
    }

    public void TestWithCheatcodes()
    {
        var counter = DeployAndBind<Counter>("counter");

        // Manipulate caller
        Vm.Prank(MakeAccount());

        // Manipulate time
        Vm.Warp(1700000000000); // milliseconds

        // Expect revert
        Vm.ExpectRevert("Unauthorized");
        counter.AdminOnly();
    }
}
```

### Cheatcodes Reference

| Cheatcode | Description |
|-----------|-------------|
| `Vm.Prank(account)` | Set caller for next call |
| `Vm.StartPrank(account)` | Set caller for all subsequent calls |
| `Vm.StopPrank()` | Stop ongoing prank |
| `Vm.Deal(account, amount)` | Set GAS balance |
| `Vm.DealNeo(account, amount)` | Set NEO balance |
| `Vm.DealToken(token, account, amount)` | Set NEP-17 token balance |
| `Vm.Warp(timestamp)` | Set block timestamp (milliseconds) |
| `Vm.Skip(seconds)` | Skip forward in time |
| `Vm.Rewind(seconds)` | Rewind time backward |
| `Vm.Roll(blockNumber)` | Set block number |
| `Vm.SetRandom(value)` | Set Runtime.GetRandom value |
| `Vm.AssumeWitness()` | Make all witness checks pass |
| `Vm.RestoreWitness()` | Restore normal witness checking |
| `Vm.ExpectRevert()` | Expect next call to revert |
| `Vm.ExpectRevert(message)` | Expect revert with specific message |
| `Vm.ExpectEmit(eventName)` | Expect event to be emitted |
| `Vm.Snapshot()` | Create state snapshot |
| `Vm.RevertTo(id)` | Revert to snapshot |
| `Vm.Assume(condition)` | Skip fuzz run if condition is false |
| `Vm.Bound(value, min, max)` | Bound fuzz value to range |
| `Vm.Label(account, label)` | Label address for trace output |

### CLI Usage

Neo Fairy includes a Foundry-style `fairy` CLI that talks to a running Fairy node.

```bash
# Build all contracts in the current project
fairy build

# Clean build artifacts (out/, Generated/, cache/)
fairy clean

# Run tests (compiles test/**/*.Test.cs if no .csproj)
fairy test --gas-report

# Run tests with real coverage (writes reports to out/coverage)
fairy test --coverage

# Write coverage reports to a custom directory
fairy test --coverage --coverage-out ./reports/coverage

# Generate coverage reports from the node (no test run)
# Uses contract deployments recorded in the workspace
fairy coverage --workspace neo-dex

# Virtual deploy project workspace into a session
fairy deploy --session dev

# Inspect and manage sessions (snapshots)
fairy session list --details

# Read-only call by alias (requires workspace deploy)
fairy call token symbol --session dev

# State-changing invocation inside a session
fairy send token transfer hash160:0x... int:1 --session dev

# Interactive debugger
fairy debug token::transfer hash160:0x... int:1 --session dev

# Inspect a compiled contract
fairy inspect token --deployer 0x...

# Workspace inspection
fairy workspace list
fairy workspace contracts --workspace neo-dex --details
fairy workspace hashes --workspace neo-dex

# Remove an alias (or clear the whole workspace)
fairy workspace clear neo-dex token --yes

# Start a local neo-cli + Fairy RPC (anvil-style)
# Requires a Neo repo checkout (set NEOROOT if not ./neo_csharp or ../neo)
fairy node start --network mainnet --host 127.0.0.1 --port 16868

# If you already have neo-cli built somewhere, you can point directly at it:
fairy node start --neo-cli /path/to/neo-cli.dll --network mainnet

# Restart on the same port
fairy node start --network mainnet --host 127.0.0.1 --port 16868 --force

# Stop the node (from another terminal)
fairy node stop --port 16868

# Validate your environment (compiler, neo-cli, RPC connectivity)
fairy doctor
```

Most RPC-backed commands accept `--rpc-url`. You can also set `FAIRY_RPC_URL`
to override the default endpoint globally.

Source-level stepping needs debug info registration on the node. Fairy will
auto-generate DumpNef text from `.nefdbgnfo` + your source files when available.
If you already have a dump file (from `dumpnef` or similar), you can pass it
explicitly:

```bash
fairy debug token::transfer hash160:0x... int:1 \
  --session dev \
  --dumpnef out/FungibleToken.nef.txt
```

On-chain deployment and invocation (relay/broadcast) requires a wallet:

```bash
fairy deploy --session mainnet --broadcast \
  --wallet ./wallet.json --password "$FAIRY_WALLET_PASSWORD" --wait

fairy send token transfer hash160:0x... int:1 \
  --session mainnet --broadcast --wait \
  --wallet 6P... --password "$FAIRY_WALLET_PASSWORD"
```

### RPC Methods

Note: Some Neo RpcServer builds lower-case custom RPC methods. If you get
`-32601 Method not found`, retry with the fully lower-cased method name (for
example `hellofairy`).

```bash
# Health check
curl -X POST http://localhost:16868 \
  -d '{"jsonrpc":"2.0","method":"helloFairy","params":[],"id":1}'

# Virtual deploy (no on-chain writes)
curl -X POST http://localhost:16868 \
  -d '{"jsonrpc":"2.0","method":"virtualDeploy","params":["session1","<nef-base64>","<manifest>"],"id":1}'

# Invoke with session
curl -X POST http://localhost:16868 \
  -d '{"jsonrpc":"2.0","method":"invokeFunctionWithSession","params":["session1",true,"<hash>","method",[]],"id":1}'
```

## Examples

### Hello World

The `examples/DexProject` directory contains a complete workspace with:

- **Contracts**: NEP-17 FungibleToken, LiquidityPool, Router
- **Tests**: Comprehensive test suite with fuzzing and cheatcodes
- **Scripts**: Deployment and interaction examples

```bash
# Build example contracts (requires nccs)
scripts/build-examples.sh

# Run workspace client
python scripts/workspace_client.py --workspace dex --alias token \
  --nef examples/DexProject/out/FungibleToken.nef \
  --manifest examples/DexProject/out/FungibleToken.manifest.json \
  --operation symbol
```

### Advanced Usage

- **Debugging**: Use `fairy debug` or the RPCs `setSourceCodeBreakpoints`, `debugStep*`, `getVariableNamesAndValues`
- **Workspaces**: Bundle contracts with `upsertWorkspaceContract`, deploy with `virtualDeployWorkspace` or `relayDeployWorkspace`
- **Coverage (experimental)**: Requires `.nefdbgnfo` debug info; DumpNef text is auto-generated when sources are available, then `getContractSourceCodeCoverage` is queried

## Community & Help

- **Issues**: [GitHub Issues](https://github.com/r3e-network/neo-fairy-test/issues)
- **Discussions**: [GitHub Discussions](https://github.com/r3e-network/neo-fairy-test/discussions)
- **Neo Community**: [Discord](https://discord.gg/neo) | [Reddit](https://reddit.com/r/neo)

## Contributing

Contributions are welcome! Please read our contributing guidelines before submitting PRs.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by [Foundry](https://github.com/foundry-rs/foundry) - the blazing fast Ethereum development toolkit
- Built on [Neo N3](https://neo.org/) - the open network for the smart economy
