// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;

namespace Neo.Fairy.Core.Models;

/// <summary>
/// Represents a compiled smart contract artifact.
/// Contains all necessary data for deployment and testing.
/// </summary>
public sealed record ContractArtifact
{
    /// <summary>
    /// Gets or sets the contract alias (friendly name for workspace reference).
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets or sets the contract name from manifest.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the NEF file bytes.
    /// </summary>
    public required byte[] NefBytes { get; init; }

    /// <summary>
    /// Gets or sets the manifest JSON string.
    /// </summary>
    public required string ManifestJson { get; init; }

    /// <summary>
    /// Gets or sets the debug info bytes (optional, for debugging).
    /// </summary>
    public byte[]? DebugInfoBytes { get; init; }

    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Gets or sets the contract dependencies (other contract aliases).
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the initialization data JSON (for _deploy method).
    /// </summary>
    public string? InitializationDataJson { get; init; }

    /// <summary>
    /// Gets or sets the default signers for this contract.
    /// </summary>
    public IReadOnlyList<SignerInfo>? DefaultSigners { get; init; }

    /// <summary>
    /// Gets the NEF checksum.
    /// </summary>
    public uint NefChecksum => CalculateChecksum(NefBytes);

    /// <summary>
    /// Gets the predicted contract hash based on sender, checksum, and name.
    /// </summary>
    /// <param name="senderHash">The deployer's script hash.</param>
    /// <returns>The predicted contract hash.</returns>
    public string GetPredictedHash(string senderHash)
    {
        // This would use Neo's Helper.GetContractHash algorithm
        // For now, return placeholder - actual implementation needs Neo references
        throw new NotImplementedException("Requires Neo.SmartContract.Helper reference");
    }

    /// <summary>
    /// Loads a contract artifact from file paths.
    /// </summary>
    /// <param name="alias">The contract alias.</param>
    /// <param name="nefPath">Path to the .nef file.</param>
    /// <param name="manifestPath">Path to the .manifest.json file.</param>
    /// <param name="debugInfoPath">Optional path to the .nefdbgnfo file.</param>
    /// <returns>The loaded contract artifact.</returns>
    public static async Task<ContractArtifact> LoadFromFilesAsync(
        string alias,
        string nefPath,
        string manifestPath,
        string? debugInfoPath = null)
    {
        var nefBytes = await File.ReadAllBytesAsync(nefPath);
        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        byte[]? debugInfoBytes = null;

        if (!string.IsNullOrEmpty(debugInfoPath) && File.Exists(debugInfoPath))
        {
            debugInfoBytes = await File.ReadAllBytesAsync(debugInfoPath);
        }

        // Extract name from manifest
        var name = ExtractNameFromManifest(manifestJson);

        return new ContractArtifact
        {
            Alias = alias,
            Name = name,
            NefBytes = nefBytes,
            ManifestJson = manifestJson,
            DebugInfoBytes = debugInfoBytes,
            SourcePath = Path.GetDirectoryName(nefPath)
        };
    }

    /// <summary>
    /// Loads a contract artifact from base64 encoded strings.
    /// </summary>
    public static ContractArtifact FromBase64(
        string alias,
        string nefBase64,
        string manifestJson,
        string? debugInfoBase64 = null)
    {
        var name = ExtractNameFromManifest(manifestJson);

        return new ContractArtifact
        {
            Alias = alias,
            Name = name,
            NefBytes = Convert.FromBase64String(nefBase64),
            ManifestJson = manifestJson,
            DebugInfoBytes = string.IsNullOrEmpty(debugInfoBase64)
                ? null
                : Convert.FromBase64String(debugInfoBase64)
        };
    }

    private static string ExtractNameFromManifest(string manifestJson)
    {
        // Simple JSON parsing for name field
        // In production, use System.Text.Json
        var nameStart = manifestJson.IndexOf("\"name\"", StringComparison.Ordinal);
        if (nameStart < 0) return "Unknown";

        var colonPos = manifestJson.IndexOf(':', nameStart);
        var quoteStart = manifestJson.IndexOf('"', colonPos + 1);
        var quoteEnd = manifestJson.IndexOf('"', quoteStart + 1);

        return manifestJson.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
    }

    private static uint CalculateChecksum(byte[] data)
    {
        // Simplified checksum - actual implementation uses Neo's checksum algorithm
        uint checksum = 0;
        foreach (var b in data)
        {
            checksum = (checksum << 1) | (checksum >> 31);
            checksum ^= b;
        }
        return checksum;
    }
}
