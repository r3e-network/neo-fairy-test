// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        if (string.IsNullOrWhiteSpace(senderHash))
            throw new ArgumentException("Sender hash is required.", nameof(senderHash));

        var senderBytes = ParseUInt160(senderHash);

        var script = new List<byte>(1 + 1 + 20 + 1 + 32);
        script.Add(0x38); // ABORT

        EmitPush(script, senderBytes);
        EmitPush(script, new BigInteger(NefChecksum));
        EmitPush(script, Name);

        var hash160 = Hash160(script.ToArray());
        Array.Reverse(hash160); // UInt160 string form is big-endian
        return "0x" + Convert.ToHexString(hash160).ToLowerInvariant();
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
        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            if (doc.RootElement.TryGetProperty("name", out var nameProp))
            {
                return nameProp.GetString() ?? "Unknown";
            }
        }
        catch (JsonException)
        {
            // Malformed manifest; fall through to default.
        }

        return "Unknown";
    }

    private static uint CalculateChecksum(byte[] data)
    {
        if (data.Length <= sizeof(uint))
            return 0;

        var span = data.AsSpan(0, data.Length - sizeof(uint));
        var hash = Hash256(span);
        return BinaryPrimitives.ReadUInt32LittleEndian(hash);
    }

    private static byte[] ParseUInt160(string value)
    {
        var trimmed = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        if (trimmed.Length != 40)
            throw new FormatException($"Invalid UInt160 length: {value}");

        var bytes = Convert.FromHexString(trimmed);
        Array.Reverse(bytes); // Neo internal UInt160 is little-endian
        return bytes;
    }

    private static void EmitPush(List<byte> script, ReadOnlySpan<byte> data)
    {
        if (data.Length < 0x100)
        {
            script.Add(0x0C); // PUSHDATA1
            script.Add((byte)data.Length);
        }
        else if (data.Length < 0x10000)
        {
            script.Add(0x0D); // PUSHDATA2
            script.AddRange(BitConverter.GetBytes((ushort)data.Length));
        }
        else
        {
            script.Add(0x0E); // PUSHDATA4
            script.AddRange(BitConverter.GetBytes(data.Length));
        }

        script.AddRange(data.ToArray());
    }

    private static void EmitPush(List<byte> script, BigInteger value)
    {
        if (value >= -1 && value <= 16)
        {
            script.Add((byte)(0x10 + (int)value)); // PUSH0 + value
            return;
        }

        Span<byte> buffer = stackalloc byte[32];
        if (!value.TryWriteBytes(buffer, out var bytesWritten, isUnsigned: false, isBigEndian: false))
            throw new ArgumentOutOfRangeException(nameof(value));

        byte[] operand;
        byte opcode;
        bool negative = value.Sign < 0;

        switch (bytesWritten)
        {
            case 1:
                opcode = 0x00; // PUSHINT8
                operand = PadRight(buffer, bytesWritten, 1, negative);
                break;
            case 2:
                opcode = 0x01; // PUSHINT16
                operand = PadRight(buffer, bytesWritten, 2, negative);
                break;
            case <= 4:
                opcode = 0x02; // PUSHINT32
                operand = PadRight(buffer, bytesWritten, 4, negative);
                break;
            case <= 8:
                opcode = 0x03; // PUSHINT64
                operand = PadRight(buffer, bytesWritten, 8, negative);
                break;
            case <= 16:
                opcode = 0x04; // PUSHINT128
                operand = PadRight(buffer, bytesWritten, 16, negative);
                break;
            case <= 32:
                opcode = 0x05; // PUSHINT256
                operand = PadRight(buffer, bytesWritten, 32, negative);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), "Invalid BigInteger size.");
        }

        script.Add(opcode);
        script.AddRange(operand);
    }

    private static void EmitPush(List<byte> script, string value)
    {
        EmitPush(script, Encoding.UTF8.GetBytes(value));
    }

    private static byte[] PadRight(Span<byte> buffer, int dataLength, int padLength, bool negative)
    {
        byte pad = negative ? (byte)0xff : (byte)0;
        var result = new byte[padLength];
        buffer[..dataLength].CopyTo(result);
        for (int x = dataLength; x < padLength; x++)
            result[x] = pad;
        return result;
    }

    private static byte[] Hash256(ReadOnlySpan<byte> data)
    {
        using var sha = SHA256.Create();
        var first = sha.ComputeHash(data.ToArray());
        var second = sha.ComputeHash(first);
        return second;
    }

    private static byte[] Hash160(ReadOnlySpan<byte> data)
    {
        using var sha = SHA256.Create();
        var hash256 = sha.ComputeHash(data.ToArray());
#pragma warning disable SYSLIB0045
        using var ripemd = HashAlgorithm.Create("RIPEMD160");
#pragma warning restore SYSLIB0045
        if (ripemd == null)
            throw new PlatformNotSupportedException("RIPEMD160 is not available on this platform.");
        return ripemd.ComputeHash(hash256);
    }
}
