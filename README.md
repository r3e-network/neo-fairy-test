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
| `Neo.Fairy.Cli` | Command-line interface (`fairy` commands) |
| `Fairy.Plugin` | Neo RpcServer plugin for neo-cli |

## Quickstart

### Prerequisites

- .NET SDK 10.0.x (pinned via `global.json`)
- Neo mono-repo checked out (defaults to `../neo`, override with `NEOROOT`)

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
# Package the plugin
scripts/package-plugin.sh -c Release

# Start neo-cli with Fairy
# Fairy RPC endpoints available at http://localhost:16868

# Verify it's working
scripts/smoke-http.sh http://127.0.0.1:16868
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
| `Vm.Deal(account, amount)` | Set GAS balance |
| `Vm.DealNeo(account, amount)` | Set NEO balance |
| `Vm.DealToken(token, account, amount)` | Set NEP-17 token balance |
| `Vm.Warp(timestamp)` | Set block timestamp (milliseconds) |
| `Vm.Skip(seconds)` | Skip forward in time |
| `Vm.ExpectRevert()` | Expect next call to revert |
| `Vm.ExpectEmit(eventName)` | Expect event to be emitted |
| `Vm.Snapshot()` | Create state snapshot |
| `Vm.RevertTo(id)` | Revert to snapshot |

### RPC Methods

```bash
# Health check
curl -X POST http://localhost:16868 \
  -d '{"jsonrpc":"2.0","method":"HelloFairy","params":[],"id":1}'

# Virtual deploy (no on-chain writes)
curl -X POST http://localhost:16868 \
  -d '{"jsonrpc":"2.0","method":"VirtualDeploy","params":["session1","<nef-base64>","<manifest>"],"id":1}'

# Invoke with session
curl -X POST http://localhost:16868 \
  -d '{"jsonrpc":"2.0","method":"InvokeFunctionWithSession","params":["session1",true,"<hash>","method",[]],"id":1}'
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

- **Debugging**: Set breakpoints with `SetSourceCodeBreakpoints`, step with `DebugStepOver`/`DebugStepInto`
- **Workspaces**: Bundle contracts with `UpsertWorkspaceContract`, deploy with `VirtualDeployWorkspace`
- **Coverage**: Generate coverage reports in HTML, LCOV, and JSON formats

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
