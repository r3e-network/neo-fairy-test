# Changelog

All notable changes to the Neo Fairy Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/r3e-network/neo-fairy-test/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/r3e-network/neo-fairy-test/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/r3e-network/neo-fairy-test/releases/tag/v1.0.0
