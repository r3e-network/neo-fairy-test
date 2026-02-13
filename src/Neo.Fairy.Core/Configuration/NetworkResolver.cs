// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Core.Configuration;

/// <summary>
/// Resolves network names (mainnet, testnet, neo-express, or custom RPC URLs) to RPC endpoints.
/// Intended for selecting between Fairy-enabled nodes (not public RPC nodes).
/// </summary>
public static class NetworkResolver
{
    private const string DefaultNeoExpress = "http://localhost:5001";

    /// <summary>
    /// Resolve the target network and RPC URL from CLI/config/env.
    /// </summary>
    /// <param name="networkOption">Network option from CLI (can be a name or RPC URL).</param>
    /// <param name="config">Project runtime config.</param>
    public static (string Name, string RpcUrl) Resolve(string? networkOption, FairyRuntimeConfig config)
    {
        // Primary fallback: FAIRY_RPC_URL -> fairy.toml rpc_url -> local default.
        var baseRpc = Environment.GetEnvironmentVariable("FAIRY_RPC_URL");
        if (string.IsNullOrWhiteSpace(baseRpc))
            baseRpc = config.RpcUrl;
        if (string.IsNullOrWhiteSpace(baseRpc))
            baseRpc = "http://localhost:16868";

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
        // For Fairy-based workflows, mainnet/testnet defaults should not silently point to public RPC nodes
        // that do not run the Fairy plugin. Fall back to the configured Fairy RPC instead.
        var mainnetRpc = EnvOrDefault("FAIRY_MAINNET_RPC", config.MainnetRpcUrl, baseRpc);
        var testnetRpc = EnvOrDefault("FAIRY_TESTNET_RPC", config.TestnetRpcUrl, baseRpc);
        var expressRpc = EnvOrDefault("FAIRY_EXPRESS_RPC", config.NeoExpressRpcUrl, DefaultNeoExpress);

        return normalized switch
        {
            "mainnet" => ("mainnet", mainnetRpc),
            "testnet" => ("testnet", testnetRpc),
            "neo-express" => ("neo-express", expressRpc),
            "neoexpress" => ("neo-express", expressRpc),
            "express" => ("neo-express", expressRpc),
            _ => (normalized, baseRpc) // fall back to configured Fairy RPC
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
