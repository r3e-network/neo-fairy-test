// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Core.Configuration;

/// <summary>
/// Resolves network names (mainnet, testnet, neo-express, or custom RPC URLs) to RPC endpoints.
/// </summary>
public static class NetworkResolver
{
    private const string DefaultMainnet = "https://mainnet1.neo.org:10331";
    private const string DefaultTestnet = "https://testnet1.neo.org:10331";
    private const string DefaultNeoExpress = "http://localhost:5001";

    /// <summary>
    /// Resolve the target network and RPC URL from CLI/config/env.
    /// </summary>
    /// <param name="networkOption">Network option from CLI (can be a name or RPC URL).</param>
    /// <param name="config">Project runtime config.</param>
    public static (string Name, string RpcUrl) Resolve(string? networkOption, FairyRuntimeConfig config)
    {
        // If the user passed a full URL, prefer that directly.
        if (!string.IsNullOrWhiteSpace(networkOption) && IsHttpUrl(networkOption))
        {
            return (networkOption, networkOption);
        }

        // If config.Network is a URL, treat it as an override.
        if (string.IsNullOrWhiteSpace(networkOption) && IsHttpUrl(config.Network))
        {
            return (config.Network, config.Network);
        }

        var normalized = (networkOption ?? config.Network ?? "mainnet").Trim().ToLowerInvariant();

        // Environment variable overrides take precedence over config and defaults.
        var mainnetRpc = EnvOrDefault("FAIRY_MAINNET_RPC", config.MainnetRpcUrl, DefaultMainnet);
        var testnetRpc = EnvOrDefault("FAIRY_TESTNET_RPC", config.TestnetRpcUrl, DefaultTestnet);
        var expressRpc = EnvOrDefault("FAIRY_EXPRESS_RPC", config.NeoExpressRpcUrl, DefaultNeoExpress);

        return normalized switch
        {
            "mainnet" => ("mainnet", mainnetRpc),
            "testnet" => ("testnet", testnetRpc),
            "neo-express" => ("neo-express", expressRpc),
            "neoexpress" => ("neo-express", expressRpc),
            "express" => ("neo-express", expressRpc),
            _ => (normalized, config.RpcUrl) // fall back to configured RPC (or default local Fairy)
        };
    }

    private static bool IsHttpUrl(string? value)
    {
        return value != null && (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                                 || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    private static string EnvOrDefault(string envKey, string? configValue, string @default)
    {
        var envVal = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envVal))
        {
            return envVal!;
        }

        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return configValue!;
        }

        return @default;
    }
}
