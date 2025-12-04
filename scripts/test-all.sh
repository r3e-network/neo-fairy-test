#!/usr/bin/env bash
set -euo pipefail

# Run all tests for the repo. Accepts extra args passed to dotnet test.
dotnet test Fairy.Full.sln "$@"
