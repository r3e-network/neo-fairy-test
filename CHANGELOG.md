# Changelog

All notable changes to the Neo Fairy Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-02-13

### Added

- **New CLI Commands** - Expanded command-line interface:
  - `fairy clean` - Clean build artifacts and temporary files
  - `fairy coverage` - Standalone coverage reporting
  - `fairy inspect` - Contract inspection and analysis
  - `fairy plugin` - Plugin management
  - `fairy session` - Session management
  - `fairy workspace` - Workspace operations
- **Coverage Registry** - Centralized coverage data collection with `CoverageRegistry`
- **Test Defaults** - Configurable test defaults via `TestDefaults` class
- **Deployment Scripting** - New `Neo.Fairy.Deployment` module with `FairyScript` helpers
- **Debugging Infrastructure** - New `Neo.Fairy.Core.Debugging` namespace
- **Wallet Loader** - Dedicated `WalletLoader` service for CLI wallet operations
- **CLI Utilities** - `FairyPluginInstaller` and `CliArgumentParser` helpers
- **Fairy Error Codes** - Structured error codes via `FairyErrorCodes`

### Fixed

- **Engine: GasConsumed Decimal Parsing** - Fixed 7 locations where `long.Parse` failed on decimal RPC responses; now uses `decimal.TryParse` with `InvariantCulture` fallback
- **Engine: Large Integer Precision** - `GetJsonValue` now uses decimal intermediate for integers exceeding Int64 range
- **Engine: Snapshot ID Collision** - Replaced `_snapshots.Count` with monotonic `_snapshotCounter` to prevent ID reuse after revert
- **Engine: Dispose Safety** - Changed bare `catch` to `catch (Exception)` in session disposal
- **Testing: Double-Dispose** - Prevented session double-dispose between `TestRunner` and `FairyTest` via `ClearSession()`
- **Testing: Fuzz Revert Counting** - Fixed erroneous `revertCount++` in failure catch that conflated skips with failures
- **Testing: Fuzz Generator Range** - `int` and `long` generators now produce full range including negative values; `ulong` generator uses all 8 bytes
- **Testing: Static Cache Leak** - `DebugInfoRegistered` now cleared between assembly runs
- **Cheatcodes: Fork Counter Reset** - `_forkCounter` now properly reset in `Reset()`
- **Cheatcodes: Notification Null Guard** - `ValidateExpectations` null-coalesces `result.Notifications`
- **Cheatcodes: Log FormatException** - Guards `args.Length == 0` before `string.Format`
- **Cheatcodes: Expected Calls** - Changed from `List` to `Queue` for correct FIFO ordering
- **Coverage: Reset Order** - `_isCollecting` set to `false` before clearing contracts
- **Plugin: KeyNotFoundException** - `ListAssemblyBreakpoints` uses `TryGetValue` instead of direct indexer
- **Plugin: Input Validation** - All `uint.Parse` calls replaced with `TryParse` + descriptive errors
- **Plugin: CTS Race Condition** - `SyncControl` uses `Interlocked.Exchange` for atomic CTS swap
- **Plugin: Non-Atomic Dictionary** - Replaced `TryGetValue`-then-set with `GetOrAdd` on `ConcurrentDictionary`
- **Plugin: Null-Forgiving Removal** - Removed `!` operator from `GetCustomAttribute` calls in WebSocket handler
- **CLI: stdout/stderr Deadlock** - Sequential process reads replaced with concurrent `Task.WhenAll`
- **CLI: RpcClient Disposal** - Added `using` to `FairyRpcClient` in coverage helper
- **CLI: Hex Prefix Parsing** - Early return for `0x`/`0X`-prefixed strings before `BigInteger.TryParse`
- **Core: Data Shadow** - Renamed `FairyException.Data` to `ErrorData` to stop shadowing `Exception.Data`

### Changed

- Bumped version to 1.2.0 across all modules
- Improved `Interlocked` usage replaced with simpler `++` where thread safety not required
- Enhanced error messages throughout CLI and Plugin modules

## [1.1.0] - 2026-01-29

### Added

- **Neo v3.9.1 Support** - Upgraded core Neo dependency from v3.9.0 to v3.9.1
- **Modular Framework Architecture** - Restructured into separate modules:
  - `Neo.Fairy.Core` - Core abstractions, models, and configuration
  - `Neo.Fairy.Engine` - RPC client and execution adapters
  - `Neo.Fairy.Testing` - Test framework with assertions and cheatcodes
  - `Neo.Fairy.Deployment` - Deployment scripting and contract management
  - `Neo.Fairy.Cli` - Foundry-style CLI tool (`fairy` command)
- **Foundry-Style CLI** - New command-line interface with commands:
  - `fairy init` - Initialize new projects
  - `fairy build` - Compile contracts
  - `fairy test` - Run tests with coverage
  - `fairy deploy` - Deploy contracts
  - `fairy call` / `fairy send` - Contract interaction
  - `fairy debug` - Interactive debugger
  - `fairy node` - Local node management
  - `fairy doctor` - Environment validation
- **Test Framework** - Comprehensive testing support:
  - `FairyTest` base class for test contracts
  - Foundry-style cheatcodes (`Vm.Prank`, `Vm.Warp`, `Vm.Deal`, etc.)
  - Assertion library with event and revert checking
  - Fuzz testing support
  - Coverage collection
- **Contract Interface Generation** - Auto-generate type-safe C# wrappers from manifests
- **Workspace Management** - Multi-contract deployment and dependency tracking
- **Coverage Reporting** - Code coverage analysis with multiple output formats
- **Network Resolver** - Support for MainNet, TestNet, and private networks

### Changed

- Upgraded target framework to .NET 10.0
- Refactored Fairy.Plugin with improved null safety
- Enhanced debugging capabilities with source code breakpoints
- Improved session-based snapshot management
- Updated build system with Directory.Build.props

### Fixed

- **Policy Contract Bug** (Neo v3.9.1) - Fixed blocked account migration to properly mark entries as "Changed" during post-Faun hardfork blocking process
- **RecoverFund Bug** (Neo v3.9.1) - Prevented blocked account entry from being incorrectly marked as "Changed" during fund recovery processing
- Fixed various null reference issues in Fairy.Plugin
- Improved error handling in CLI commands

### Security

- Improved wallet handling and security checks
- Enhanced witness validation in test environment

## [1.0.0] - 2025-12-13

### Added

- Initial release of Neo Fairy Framework
- **Fairy Plugin** - RpcServer plugin for Neo CLI
- **Session-Based Testing** - Isolated test snapshots with `FairySession`
- **Virtual Deployment** - Deploy contracts without GAS fees
- **Interactive Debugger** - Full debugging support with:
  - Assembly and source code breakpoints
  - Step into/over/out operations
  - Variable inspection
  - Stack trace analysis
- **Runtime Overrides** - Manipulate blockchain state:
  - Timestamp manipulation (`SetSnapshotTimestamp`)
  - Random value control (`SetSnapshotRandom`)
  - Witness check bypass (`SetSnapshotCheckWitness`)
- **Oracle Simulation** - Test Oracle responses
- **Coverage Tracking** - Basic code coverage for contract execution
- **WebSocket Support** - Real-time blockchain event subscriptions
- **Workspace Management** - Multi-contract organization and deployment
- **Relay Operations** - On-chain deployment and invocation

### Infrastructure

- CI/CD pipeline with GitHub Actions
- Example projects (DexProject)
- Smoke testing scripts
- Documentation and README

[Unreleased]: https://github.com/r3e-network/neo-fairy-test/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/r3e-network/neo-fairy-test/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/r3e-network/neo-fairy-test/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/r3e-network/neo-fairy-test/releases/tag/v1.0.0
