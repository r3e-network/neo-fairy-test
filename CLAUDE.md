# CLAUDE.md - Project Instructions for Claude Code

## Git Commit Rules

**MANDATORY**: All git commits MUST follow these rules:

1. **Author**: Always use `Jimmy <jimmy@r3e.network>` as the commit author
   ```bash
   git commit --author="Jimmy <jimmy@r3e.network>" -m "message"
   ```

2. **No Claude References**: Never mention Claude, AI, or any AI assistant in:
   - Commit messages
   - Commit descriptions
   - Co-authored-by lines
   - Any git metadata

3. **No Auto-generated Footers**: Do not add:
   - `🤖 Generated with Claude Code`
   - `Co-Authored-By: Claude <noreply@anthropic.com>`
   - Any similar AI attribution

## Project Overview

Neo Fairy is a Neo N3 RpcServer plugin for smart contract development, testing, and debugging. Inspired by Foundry.

### Architecture

| Module | Description |
|--------|-------------|
| `Neo.Fairy.Core` | Shared abstractions, models, configuration |
| `Neo.Fairy.Engine` | RPC client and execution adapters |
| `Neo.Fairy.Testing` | Test framework with assertions and cheatcodes |
| `Neo.Fairy.Cli` | Command-line interface (`fairy` commands) |
| `Fairy.Plugin` | Neo RpcServer plugin for neo-cli |

### Build Commands

```bash
# Build everything
dotnet build Fairy.Full.sln

# Run tests
dotnet test Fairy.Full.sln

# Package plugin
scripts/package-plugin.sh -c Release
```

### Key Directories

- `src/` - Source code for all modules
- `tests/` - Unit and integration tests
- `examples/` - Example contracts and projects
- `scripts/` - Build and utility scripts
- `assets/` - Logo and visual assets
