// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Collections.Concurrent;

namespace Neo.Fairy.Testing.Coverage;

/// <summary>
/// Tracks contracts deployed during a test run when coverage is enabled.
/// Used by the CLI to query coverage after tests complete.
/// </summary>
public static class CoverageRegistry
{
    private static readonly ConcurrentDictionary<string, string?> ContractsByHash =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a deployed contract for later coverage retrieval.
    /// </summary>
    public static void Register(string contractHash, string? contractName = null)
    {
        ContractsByHash.AddOrUpdate(contractHash, contractName, (_, _) => contractName);
    }

    /// <summary>
    /// Gets all registered contracts by hash.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> Contracts => ContractsByHash;

    /// <summary>
    /// Clears all registrations.
    /// </summary>
    public static void Clear()
    {
        ContractsByHash.Clear();
    }
}

