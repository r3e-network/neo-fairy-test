# Neo Fairy Framework - Refactoring Plan

## Executive Summary

Transform Neo Fairy Test from a testing-only RPC plugin into a **professional-grade Neo N3 C# smart contract development framework**, inspired by Foundry's architecture but tailored for the Neo ecosystem.

**Target Name**: `Neo.Fairy` (Framework) + `fairy` (CLI)

---

## 1. Architecture Comparison

### Foundry Architecture
```
foundry/
├── forge      → Build, test, deploy contracts (Solidity-first)
├── cast       → Interact with blockchain (call, send, balance)
├── anvil      → Local development node
└── chisel     → Solidity REPL
```

### Neo Fairy Framework Architecture (Proposed)
```
neo-fairy/
├── fairy build    → Compile C# contracts (nccs wrapper)
├── fairy test     → Run tests with assertions & coverage
├── fairy deploy   → Deploy to snapshot or chain
├── fairy call     → Invoke contract methods (read-only)
├── fairy send     → Invoke with state changes (relay)
├── fairy debug    → Interactive debugger
├── fairy script   → Run deployment/migration scripts
└── fairy node     → RPC server (current Fairy plugin)
```

---

## 2. Current State Analysis

### Strengths (Keep & Enhance)
- ✅ Session-based snapshot isolation (`FairySession`)
- ✅ Virtual execution without GAS fees
- ✅ Interactive debugging with breakpoints
- ✅ Code coverage tracking
- ✅ Workspace-based multi-contract management
- ✅ Runtime overrides (timestamp, random, witness)
- ✅ Oracle simulation
- ✅ WebSocket subscriptions

### Weaknesses (Address)
- ❌ No CLI tool - RPC-only interface
- ❌ No built-in test assertions framework
- ❌ No project scaffolding (`fairy init`)
- ❌ No contract compilation integration
- ❌ No dependency management for contracts
- ❌ No migration/script system
- ❌ Monolithic partial classes (4,777 lines)
- ❌ Tight coupling to Neo internals
- ❌ No configuration file standard (like `foundry.toml`)

---

## 3. Proposed Architecture

### 3.1 Project Structure (fairy init)

```
my-neo-project/
├── fairy.toml              # Project configuration
├── src/
│   ├── Token.cs            # Main contracts
│   ├── Router.cs
│   └── lib/                # Shared libraries
├── test/
│   ├── Token.Test.cs       # Test contracts (inherit FairyTest)
│   └── Router.Test.cs
├── script/
│   ├── Deploy.cs           # Deployment scripts
│   └── Migrate.cs
├── out/
│   ├── Token.nef           # Compiled artifacts
│   ├── Token.manifest.json
│   └── Token.nefdbgnfo
└── cache/
    └── snapshots/          # Saved test states
```

### 3.2 Configuration (fairy.toml)

```toml
[project]
name = "my-dex"
version = "1.0.0"
src = "src"
test = "test"
script = "script"
out = "out"

[compiler]
path = "nccs"               # Neo C# compiler
debug = true
assembly = true
optimize = false

[fairy]
rpc_url = "http://localhost:16868"
network = "mainnet"         # mainnet | testnet | private
gas_limit = 200
session_timeout = 86400

[deploy]
default_wallet = "fairy.json"
verify = true

[test]
verbosity = 2               # 0=minimal, 1=normal, 2=detailed, 3=trace
coverage = true
parallel = true
fail_fast = false

[[contracts]]
name = "Token"
path = "src/Token.cs"
alias = "token"

[[contracts]]
name = "Router"
path = "src/Router.cs"
alias = "router"
depends = ["token"]         # Deployment order
```

### 3.3 Module Decomposition

```
Neo.Fairy/
├── Neo.Fairy.Core/                 # Core abstractions
│   ├── Interfaces/
│   │   ├── IFairyEngine.cs
│   │   ├── IFairySession.cs
│   │   ├── IContractDeployer.cs
│   │   └── ITestRunner.cs
│   ├── Models/
│   │   ├── FairyProject.cs
│   │   ├── ContractArtifact.cs
│   │   ├── TestResult.cs
│   │   └── DeploymentResult.cs
│   └── Configuration/
│       └── FairyConfig.cs
│
├── Neo.Fairy.Engine/               # Execution engine (refactored)
│   ├── FairyEngine.cs
│   ├── FairySession.cs
│   ├── RuntimeOverrides.cs
│   └── SnapshotManager.cs
│
├── Neo.Fairy.Testing/              # Test framework
│   ├── FairyTest.cs                # Base test class
│   ├── Assertions/
│   │   ├── Assert.cs
│   │   ├── AssertEvent.cs
│   │   └── AssertRevert.cs
│   ├── Cheatcodes/                 # Foundry-style vm.*
│   │   ├── ICheatcodes.cs
│   │   ├── TimestampCheat.cs
│   │   ├── BalanceCheat.cs
│   │   ├── PrankCheat.cs
│   │   └── ExpectRevertCheat.cs
│   └── Runner/
│       ├── TestDiscovery.cs
│       └── TestExecutor.cs
│
├── Neo.Fairy.Deployment/           # Deployment system
│   ├── Deployer.cs
│   ├── ScriptRunner.cs
│   ├── DependencyResolver.cs
│   └── MigrationManager.cs
│
├── Neo.Fairy.Debugger/             # Debugger (refactored)
│   ├── DebugSession.cs
│   ├── BreakpointManager.cs
│   ├── VariableInspector.cs
│   └── DebugInfoRegistry.cs
│
├── Neo.Fairy.Coverage/             # Coverage analysis
│   ├── CoverageCollector.cs
│   ├── CoverageReport.cs
│   └── Reporters/
│       ├── ConsoleReporter.cs
│       ├── HtmlReporter.cs
│       └── LcovReporter.cs
│
├── Neo.Fairy.RpcServer/            # RPC plugin (current Fairy)
│   ├── FairyPlugin.cs
│   ├── RpcMethods/
│   │   ├── TestingRpc.cs
│   │   ├── DeploymentRpc.cs
│   │   ├── DebuggerRpc.cs
│   │   └── WorkspaceRpc.cs
│   └── WebSocket/
│       └── FairyWebSocket.cs
│
└── Neo.Fairy.Cli/                  # CLI tool
    ├── Program.cs
    ├── Commands/
    │   ├── InitCommand.cs
    │   ├── BuildCommand.cs
    │   ├── TestCommand.cs
    │   ├── DeployCommand.cs
    │   ├── CallCommand.cs
    │   ├── SendCommand.cs
    │   ├── DebugCommand.cs
    │   ├── ScriptCommand.cs
    │   └── NodeCommand.cs
    └── Templates/
        ├── project/
        ├── contract/
        └── test/
```

---

## 4. Test Framework Design

### 4.1 Base Test Class (FairyTest)

```csharp
using Neo.Fairy.Testing;

public class TokenTest : FairyTest
{
    private UInt160 tokenHash;
    private UInt160 alice;
    private UInt160 bob;

    // Setup runs before each test
    public override void SetUp()
    {
        // Deploy contract to test session
        tokenHash = Deploy("src/Token.cs");

        // Create test accounts
        alice = MakeAccount();
        bob = MakeAccount();

        // Fund accounts (cheatcode)
        Vm.Deal(alice, 1000_00000000); // 1000 GAS
        Vm.Deal(bob, 500_00000000);
    }

    // Test methods start with "Test"
    public void TestMint()
    {
        // Arrange
        var amount = 100_00000000;

        // Act - prank sets msg.sender for next call
        Vm.Prank(alice);
        var result = Call(tokenHash, "mint", alice, amount);

        // Assert
        Assert.Equal(VMState.HALT, result.State);
        Assert.Equal(amount, Call<BigInteger>(tokenHash, "balanceOf", alice));

        // Assert event emission
        Assert.EmittedEvent(result, "Transfer",
            ("from", UInt160.Zero),
            ("to", alice),
            ("amount", amount));
    }

    public void TestTransferInsufficientBalance()
    {
        // Expect revert with specific message
        Vm.ExpectRevert("Insufficient balance");

        Vm.Prank(alice);
        Call(tokenHash, "transfer", bob, 1000_00000000);
    }

    public void TestFuzz_Transfer(uint96 amount)
    {
        // Fuzz testing with random inputs
        Vm.Assume(amount > 0 && amount < 1000_00000000);

        // Mint first
        Vm.Prank(alice);
        Call(tokenHash, "mint", alice, amount);

        // Transfer
        Vm.Prank(alice);
        Call(tokenHash, "transfer", bob, amount);

        Assert.Equal(0, Call<BigInteger>(tokenHash, "balanceOf", alice));
        Assert.Equal(amount, Call<BigInteger>(tokenHash, "balanceOf", bob));
    }
}
```

### 4.2 Cheatcodes (Vm.*)

| Cheatcode | Description | Foundry Equivalent |
|-----------|-------------|-------------------|
| `Vm.Prank(address)` | Set caller for next call | `vm.prank()` |
| `Vm.StartPrank(address)` | Set caller until StopPrank | `vm.startPrank()` |
| `Vm.Deal(address, amount)` | Set GAS balance | `deal()` |
| `Vm.DealNeo(address, amount)` | Set NEO balance | - |
| `Vm.DealToken(token, address, amount)` | Set token balance | `deal()` |
| `Vm.Warp(timestamp)` | Set block timestamp | `vm.warp()` |
| `Vm.Roll(blockNumber)` | Set block number | `vm.roll()` |
| `Vm.SetRandom(value)` | Set Runtime.GetRandom | - |
| `Vm.ExpectRevert(message?)` | Expect next call reverts | `vm.expectRevert()` |
| `Vm.ExpectEmit(topics, data)` | Expect event emission | `vm.expectEmit()` |
| `Vm.Assume(condition)` | Skip fuzz input if false | `vm.assume()` |
| `Vm.Snapshot()` | Create state snapshot | `vm.snapshot()` |
| `Vm.RevertTo(id)` | Revert to snapshot | `vm.revertTo()` |
| `Vm.Label(address, name)` | Label address for traces | `vm.label()` |

### 4.3 Assertions

```csharp
// Value assertions
Assert.Equal(expected, actual);
Assert.NotEqual(expected, actual);
Assert.True(condition, message?);
Assert.False(condition, message?);
Assert.Greater(a, b);
Assert.Less(a, b);
Assert.InRange(value, min, max);

// State assertions
Assert.Halted(result);
Assert.Faulted(result);
Assert.GasUsed(result, expected, tolerance?);

// Event assertions
Assert.EmittedEvent(result, eventName, params...);
Assert.EmittedEventCount(result, eventName, count);
Assert.NoEvents(result);

// Storage assertions
Assert.StorageEqual(contract, key, expected);
Assert.BalanceEqual(address, expected);
Assert.BalanceChanged(address, delta);

// Revert assertions
Assert.Reverted(result);
Assert.RevertedWith(result, message);
Assert.RevertedWithPanic(result, panicCode);
```

---

## 5. CLI Commands

### 5.1 fairy init

```bash
$ fairy init my-project
Creating new Fairy project: my-project
  ✓ Created fairy.toml
  ✓ Created src/Counter.cs
  ✓ Created test/Counter.Test.cs
  ✓ Created script/Deploy.cs
  ✓ Initialized git repository

$ cd my-project && tree
my-project/
├── fairy.toml
├── src/
│   └── Counter.cs
├── test/
│   └── Counter.Test.cs
└── script/
    └── Deploy.cs
```

### 5.2 fairy build

```bash
$ fairy build
Compiling 2 contracts...
  [1/2] Token.cs → out/Token.nef (2.3kb)
  [2/2] Router.cs → out/Router.nef (4.1kb)
✓ Compiled successfully in 1.2s
```

### 5.3 fairy test

```bash
$ fairy test
Running 8 tests in 2 files...

  TokenTest
    ✓ TestMint (12ms, 0.5 GAS)
    ✓ TestTransfer (15ms, 0.8 GAS)
    ✓ TestTransferInsufficientBalance (8ms, 0.2 GAS)
    ✓ TestFuzz_Transfer (runs: 256, μ: 0.6 GAS)

  RouterTest
    ✓ TestAddLiquidity (45ms, 2.1 GAS)
    ✓ TestSwap (38ms, 1.8 GAS)
    ✗ TestRemoveLiquidity
      │ Expected: HALT
      │ Actual: FAULT
      │ Exception: "Insufficient liquidity"
      │ at Router.cs:142
    ✓ TestFuzz_Swap (runs: 256, μ: 1.9 GAS)

Test Summary: 7 passed, 1 failed (87.5%)
Coverage: 78.3% (src/Token.cs: 92%, src/Router.cs: 64%)
Total time: 2.4s
```

### 5.4 fairy deploy

```bash
# Deploy to virtual session (testing)
$ fairy deploy --session dev
Deploying to session 'dev'...
  [1/2] Token → 0x1234...abcd (1.2 GAS)
  [2/2] Router → 0x5678...efgh (2.1 GAS)
✓ Deployed 2 contracts in 0.8s

# Deploy to real network
$ fairy deploy --network testnet --wallet wallet.json
Deploying to TestNet...
  [1/2] Token → 0x1234...abcd
        TX: 0xabc123... (pending)
  [2/2] Router → 0x5678...efgh
        TX: 0xdef456... (pending)
Waiting for confirmations...
✓ Deployed 2 contracts (3 blocks confirmed)
```

### 5.5 fairy call / fairy send

```bash
# Read-only call (no state change)
$ fairy call 0x1234...abcd balanceOf 0xuser...
Result: 1000000000000 (BigInteger)

# State-changing send (relay to chain)
$ fairy send 0x1234...abcd transfer 0xto... 100 --wallet wallet.json
TX Hash: 0xabc123...
Status: Confirmed (block 12345)
GAS Used: 0.8
```

### 5.6 fairy script

```bash
$ fairy script script/Deploy.cs --network testnet
Running deployment script...
  → Deploying Token...
  → Deploying Router with Token dependency...
  → Initializing Router with Token address...
  → Setting initial parameters...
✓ Script completed successfully
```

### 5.7 fairy debug

```bash
$ fairy debug test/Token.Test.cs::TestMint
Starting debug session...
Breakpoint hit at Token.cs:42
  40 │     public static bool Mint(UInt160 to, BigInteger amount)
  41 │     {
→ 42 │         Assert(Runtime.CheckWitness(to), "Not authorized");
  43 │

(fairy) locals
  to = 0x1234...abcd
  amount = 10000000000

(fairy) step
  43 │
→ 44 │         var balance = BalanceOf(to);

(fairy) continue
Test passed: TestMint
```

---

## 6. Implementation Phases

### Phase 1: Core Refactoring (Week 1-2)
1. Extract interfaces from current monolithic code
2. Create `Neo.Fairy.Core` with abstractions
3. Refactor `FairyEngine` and `FairySession` into separate module
4. Implement `FairyConfig` for `fairy.toml` parsing
5. Maintain backward compatibility with existing RPC API

### Phase 2: Test Framework (Week 3-4)
1. Implement `FairyTest` base class
2. Create assertion library
3. Implement cheatcodes system
4. Build test discovery and execution
5. Add fuzz testing support

### Phase 3: CLI Tool (Week 5-6)
1. Create `Neo.Fairy.Cli` project
2. Implement `init`, `build`, `test` commands
3. Add project templates
4. Integrate with nccs compiler
5. Add colorful console output

### Phase 4: Deployment System (Week 7-8)
1. Implement dependency resolution
2. Create script runner
3. Add migration tracking
4. Implement `deploy`, `call`, `send` commands
5. Add verification support

### Phase 5: Polish & Documentation (Week 9-10)
1. Coverage reporting (HTML, LCOV)
2. Performance optimization
3. Comprehensive documentation
4. Example projects
5. CI/CD templates

---

## 7. Migration Path

### Existing API Compatibility

All current RPC methods will remain available:
- `VirtualDeploy` → `fairy deploy --session`
- `InvokeFunctionWithSession` → `fairy call --session`
- `RelayDeployContract` → `fairy deploy --network`
- `DebugFunctionWithSession` → `fairy debug`
- Workspace APIs → Enhanced with CLI support

### New Recommended Workflow

```
Old: RPC calls via HTTP client
     ↓
New: fairy CLI + Test framework + RPC (for advanced use)
```

---

## 8. Technical Decisions

### 8.1 Why C# Test Framework (not Solidity-style)?

Neo contracts are written in C#, so tests should be in C# too:
- Native language integration
- Full IDE support (IntelliSense, debugging)
- Access to .NET ecosystem
- Consistent with Neo development experience

### 8.2 Why Keep RPC Server?

- IDE integration (VS Code extensions)
- Remote testing capabilities
- Programmatic access from any language
- Backward compatibility

### 8.3 Dependency Injection

Use Microsoft.Extensions.DependencyInjection:
```csharp
services.AddSingleton<IFairyEngine, FairyEngine>();
services.AddScoped<IFairySession, FairySession>();
services.AddTransient<ITestRunner, TestRunner>();
```

---

## 9. Success Metrics

| Metric | Current | Target |
|--------|---------|--------|
| Lines of Code | 4,777 (monolithic) | ~8,000 (modular) |
| Test Coverage | Manual | 80%+ automated |
| CLI Commands | 0 | 10+ |
| Documentation | README only | Full docs site |
| Setup Time | Complex | `fairy init` in 10s |
| Test Execution | RPC calls | `fairy test` in <5s |

---

## 10. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing users | High | Maintain RPC API compatibility |
| nccs compiler changes | Medium | Abstract compiler interface |
| Neo version updates | Medium | Version-specific adapters |
| Scope creep | High | Strict phase boundaries |
| Performance regression | Medium | Benchmark suite |

---

## Appendix A: File Mapping (Current → New)

| Current File | New Location |
|--------------|--------------|
| Fairy.cs | Neo.Fairy.RpcServer/FairyPlugin.cs |
| Fairy.Engine.cs | Neo.Fairy.Engine/FairyEngine.cs |
| Fairy.Tester.cs | Neo.Fairy.Engine/Executor.cs |
| Fairy.Debugger.cs | Neo.Fairy.Debugger/DebugSession.cs |
| Fairy.Debugger.Breakpoint.cs | Neo.Fairy.Debugger/BreakpointManager.cs |
| Fairy.Debugger.DebugInfo.cs | Neo.Fairy.Debugger/DebugInfoRegistry.cs |
| Fairy.Coverage.cs | Neo.Fairy.Coverage/CoverageCollector.cs |
| Fairy.Wallet.cs | Neo.Fairy.Core/Wallet/FairyWallet.cs |
| Fairy.Utils.cs | Neo.Fairy.Deployment/Deployer.cs |
| Fairy.Workspace.cs | Neo.Fairy.Core/Workspace/WorkspaceManager.cs |
| Fairy.Oracle.cs | Neo.Fairy.Engine/OracleSimulator.cs |
| Fairy.WebSocket.cs | Neo.Fairy.RpcServer/WebSocket/ |

---

## Appendix B: Comparison with Foundry

| Feature | Foundry | Neo Fairy (Proposed) |
|---------|---------|---------------------|
| Language | Solidity | C# |
| Test Framework | forge-std | Neo.Fairy.Testing |
| CLI | forge/cast/anvil | fairy |
| Local Node | anvil | fairy node (RPC) |
| Cheatcodes | vm.* | Vm.* |
| Fuzz Testing | Built-in | Built-in |
| Coverage | Built-in | Built-in |
| Debugging | Limited | Full (breakpoints, stepping) |
| Scripting | Solidity scripts | C# scripts |
| Config | foundry.toml | fairy.toml |

---

*Document Version: 1.0*
*Created: 2025-12-04*
*Author: Claude (AI Assistant)*
