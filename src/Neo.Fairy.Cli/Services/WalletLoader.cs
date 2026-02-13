// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Text.Json;
using Neo.Fairy.Core.Models;

namespace Neo.Fairy.Cli.Services;

/// <summary>
/// Helpers for loading wallet credentials for relay (on-chain) operations.
/// The Fairy RPC accepts either WIF strings or NEP2 keys + password.
/// </summary>
internal static class WalletLoader
{
    internal sealed record WalletSpec
    {
        public IReadOnlyList<string>? Wifs { get; init; }
        public IReadOnlyList<string>? Nep2Keys { get; init; }
        public required string Password { get; init; }
    }

    /// <summary>
    /// Loads wallet information from CLI options / config.
    /// </summary>
    /// <param name="walletArg">Wallet option value.</param>
    /// <param name="passwordArg">Password option value (optional).</param>
    /// <param name="project">Current Fairy project (optional).</param>
    public static WalletSpec Load(string? walletArg, string? passwordArg, FairyProject? project)
    {
        var resolvedWallet = walletArg;
        if (string.IsNullOrWhiteSpace(resolvedWallet) && project != null)
        {
            var defaultWallet = project.Config.Deploy.DefaultWallet;
            if (!string.IsNullOrWhiteSpace(defaultWallet))
            {
                var candidate = Path.Combine(project.RootDirectory, defaultWallet);
                if (File.Exists(candidate))
                {
                    resolvedWallet = candidate;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedWallet))
        {
            throw new InvalidOperationException("No wallet specified. Provide --wallet or configure deploy.default_wallet in fairy.toml.");
        }

        var password = passwordArg
                       ?? Environment.GetEnvironmentVariable("FAIRY_WALLET_PASSWORD")
                       ?? Environment.GetEnvironmentVariable("NEO_WALLET_PASSWORD")
                       ?? string.Empty;

        if (File.Exists(resolvedWallet))
        {
            var ext = Path.GetExtension(resolvedWallet);
            if (!ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported wallet file extension: {ext}. Only NEP6 .json wallets are supported.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("Wallet password required. Provide --password or FAIRY_WALLET_PASSWORD env.");
            }

            var nep2Keys = LoadNep2KeysFromNep6(resolvedWallet);
            return new WalletSpec
            {
                Nep2Keys = nep2Keys,
                Password = password
            };
        }

        // Raw string key.
        if (resolvedWallet.StartsWith("6P", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new InvalidOperationException("NEP2 password required. Provide --password or FAIRY_WALLET_PASSWORD env.");
            }

            return new WalletSpec
            {
                Nep2Keys = new[] { resolvedWallet },
                Password = password
            };
        }

        return new WalletSpec
        {
            Wifs = new[] { resolvedWallet },
            Password = password
        };
    }

    private static IReadOnlyList<string> LoadNep2KeysFromNep6(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("accounts", out var accountsEl) ||
            accountsEl.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid NEP6 wallet: missing accounts array.");
        }

        var nep2Keys = new List<string>();
        foreach (var accountEl in accountsEl.EnumerateArray())
        {
            if (accountEl.ValueKind != JsonValueKind.Object)
                continue;

            if (accountEl.TryGetProperty("key", out var keyEl) &&
                keyEl.ValueKind == JsonValueKind.String)
            {
                var key = keyEl.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    nep2Keys.Add(key);
                }
            }
        }

        if (nep2Keys.Count == 0)
        {
            throw new InvalidOperationException("No NEP2 keys found in wallet.");
        }

        return nep2Keys;
    }
}

