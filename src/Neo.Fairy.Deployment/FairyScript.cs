// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Numerics;
using System.Text.Json;
using Neo.Fairy.Core.Configuration;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;

namespace Neo.Fairy.Deployment;

/// <summary>
/// Base class for Foundry-style deployment/migration scripts.
/// Scripts are executed through the Fairy CLI and use Fairy RPC under the hood.
/// </summary>
public abstract class FairyScript
{
    private bool _initialized;
    private readonly Dictionary<string, DeploymentResult> _deployments = new(StringComparer.OrdinalIgnoreCase);

    protected FairyProject Project { get; private set; } = null!;
    protected FairyRpcClient RpcClient { get; private set; } = null!;

    /// <summary>
    /// Gets the project runtime configuration.
    /// </summary>
    public FairyRuntimeConfig Config => Project.Config.Fairy;

    /// <summary>
    /// Gets the virtual session id used for script execution.
    /// </summary>
    public string SessionId { get; private set; } = string.Empty;

    /// <summary>
    /// Gets whether this script is intended to broadcast on-chain transactions.
    /// When true, DeployAsync/CallAsync will relay transactions to chain.
    /// </summary>
    public bool Broadcast { get; private set; }

    /// <summary>
    /// Gets the deployer account script hash (hex string).
    /// </summary>
    public string Deployer { get; private set; } = string.Empty;

    /// <summary>
    /// Called by the CLI before running the script.
    /// </summary>
    internal void Initialize(
        FairyProject project,
        FairyRpcClient rpcClient,
        string sessionId,
        bool broadcast,
        string? deployer)
    {
        if (_initialized) return;

        Project = project;
        RpcClient = rpcClient;
        SessionId = sessionId;
        Broadcast = broadcast;
        Deployer = deployer
                   ?? Environment.GetEnvironmentVariable("FAIRY_DEPLOYER")
                   ?? GenerateAccount();

        _initialized = true;
    }

    /// <summary>
    /// Entry point for a script.
    /// </summary>
    public abstract Task RunAsync();

    /// <summary>
    /// Deploys a configured contract by alias.
    /// </summary>
    public async Task<DeploymentResult> DeployAsync(string alias)
    {
        EnsureInitialized();

        if (_deployments.TryGetValue(alias, out var existing))
        {
            return existing;
        }

        await Project.LoadArtifactsAsync();
        var artifact = Project.GetArtifact(alias);
        if (artifact == null)
        {
            throw new InvalidOperationException($"Contract '{alias}' not found or not compiled. Run `fairy build` first.");
        }

        object? initData = artifact.InitializationDataJson;
        if (initData is string initJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(initJson);
                initData = doc.RootElement.Clone();
            }
            catch
            {
                initData = null;
            }
        }

        var result = Broadcast
            ? await RpcClient.RelayDeployContractAsync(
                SessionId,
                artifact.Alias,
                artifact.NefBytes,
                artifact.ManifestJson,
                initData: initData,
                signers: artifact.DefaultSigners)
            : await RpcClient.VirtualDeployAsync(
                SessionId,
                artifact.Alias,
                artifact.NefBytes,
                artifact.ManifestJson,
                initData: initData,
                signers: artifact.DefaultSigners);

        _deployments[alias] = result;
        return result;
    }

    /// <summary>
    /// Calls a contract method, persisting changes in the current session.
    /// </summary>
    public async Task<ExecutionResult> CallAsync(string contractHash, string method, params object[] args)
    {
        EnsureInitialized();

        if (Broadcast)
        {
            return await RpcClient.RelayInvokeFunctionAsync(
                SessionId,
                contractHash,
                method,
                args);
        }

        return await RpcClient.InvokeFunctionWithSessionAsync(
            SessionId,
            contractHash,
            method,
            args,
            writeSnapshot: true);
    }

    /// <summary>
    /// Calls a contract method without persisting changes.
    /// </summary>
    public async Task<ExecutionResult> StaticCallAsync(string contractHash, string method, params object[] args)
    {
        EnsureInitialized();

        return await RpcClient.InvokeFunctionWithSessionAsync(
            SessionId,
            contractHash,
            method,
            args,
            writeSnapshot: false);
    }

    /// <summary>
    /// Logs a script message.
    /// </summary>
    public void Log(string message)
    {
        Console.WriteLine($"[{GetType().Name}] {message}");
    }

    /// <summary>
    /// Reads an environment variable or returns a default.
    /// </summary>
    public string GetEnvOrDefault(string key, string defaultValue)
    {
        return Environment.GetEnvironmentVariable(key) ?? defaultValue;
    }

    /// <summary>
    /// Reads an environment variable or throws if missing.
    /// </summary>
    public string GetEnvOrFail(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required environment variable: {key}");
        }
        return value;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Script was not initialized. Run through `fairy script`.");
        }
    }

    private static string GenerateAccount()
    {
        var bytes = new byte[20];
        Random.Shared.NextBytes(bytes);
        return "0x" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
