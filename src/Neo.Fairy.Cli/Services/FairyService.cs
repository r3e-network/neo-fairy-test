// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;

namespace Neo.Fairy.Cli.Services;

/// <summary>
/// Service layer for CLI commands to interact with Fairy.
/// Provides a unified interface for all Fairy operations.
/// </summary>
public sealed class FairyService : IDisposable
{
    private readonly FairySessionFactory _sessionFactory;
    private readonly FairyEngineAdapter _engine;
    private readonly Dictionary<string, FairySessionAdapter> _sessions = new();
    private bool _disposed;

    public FairyService(string rpcUrl = "http://localhost:16868")
    {
        RpcUrl = rpcUrl;
        _sessionFactory = new FairySessionFactory(rpcUrl);
        _engine = new FairyEngineAdapter(rpcUrl);
    }

    /// <summary>
    /// Gets the RPC URL.
    /// </summary>
    public string RpcUrl { get; }

    /// <summary>
    /// Checks if the Fairy RPC is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        return await _sessionFactory.IsAvailableAsync();
    }

    /// <summary>
    /// Gets or creates a session by name.
    /// </summary>
    public FairySessionAdapter GetOrCreateSession(string sessionName)
    {
        if (!_sessions.TryGetValue(sessionName, out var session))
        {
            session = _sessionFactory.CreateSession(sessionName);
            _sessions[sessionName] = session;
        }
        return session;
    }

    /// <summary>
    /// Deploys a contract to a session.
    /// </summary>
    public async Task<DeploymentResult> DeployAsync(
        string sessionName,
        ContractArtifact artifact,
        DeploymentOptions? options = null)
    {
        var session = GetOrCreateSession(sessionName);

        var result = await _sessionFactory.RpcClient.VirtualDeployAsync(
            sessionName,
            artifact.NefBytes,
            artifact.ManifestJson,
            null,
            options?.Signers);

        if (result.IsSuccess)
        {
            session.RegisterContract(artifact.Alias, result.ContractHash);
        }

        return result;
    }

    /// <summary>
    /// Deploys multiple contracts in dependency order.
    /// </summary>
    public async Task<IReadOnlyList<DeploymentResult>> DeployWorkspaceAsync(
        string sessionName,
        IReadOnlyList<ContractArtifact> artifacts,
        DeploymentOptions? options = null)
    {
        var results = new List<DeploymentResult>();
        var stopOnFailure = options?.StopOnFailure ?? true;

        foreach (var artifact in artifacts)
        {
            var result = await DeployAsync(sessionName, artifact, options);
            results.Add(result);

            if (stopOnFailure && !result.IsSuccess)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Invokes a contract method.
    /// </summary>
    public async Task<ExecutionResult> CallAsync(
        string sessionName,
        string contractHash,
        string method,
        object[]? args = null,
        bool persistChanges = false)
    {
        var session = GetOrCreateSession(sessionName);

        return await _sessionFactory.RpcClient.InvokeFunctionWithSessionAsync(
            sessionName,
            contractHash,
            method,
            args ?? Array.Empty<object>(),
            persistChanges,
            null);
    }

    /// <summary>
    /// Invokes a contract by alias.
    /// </summary>
    public async Task<ExecutionResult> CallByAliasAsync(
        string sessionName,
        string alias,
        string method,
        object[]? args = null,
        bool persistChanges = false)
    {
        var session = GetOrCreateSession(sessionName);
        var contractHash = session.GetContractHash(alias);

        if (string.IsNullOrEmpty(contractHash))
        {
            throw new InvalidOperationException($"Contract '{alias}' not found in session '{sessionName}'");
        }

        return await CallAsync(sessionName, contractHash, method, args, persistChanges);
    }

    /// <summary>
    /// Sets the GAS balance for an account.
    /// </summary>
    public async Task SetGasBalanceAsync(string sessionName, string account, long balance)
    {
        await _sessionFactory.RpcClient.SetGasBalanceAsync(sessionName, account, balance);
    }

    /// <summary>
    /// Sets the timestamp for a session.
    /// </summary>
    public async Task SetTimestampAsync(string sessionName, ulong timestamp)
    {
        await _sessionFactory.RpcClient.SetTimestampAsync(sessionName, timestamp);
    }

    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    public IReadOnlyDictionary<string, FairySessionAdapter> GetSessions()
    {
        return _sessions;
    }

    /// <summary>
    /// Disposes a specific session.
    /// </summary>
    public void DisposeSession(string sessionName)
    {
        if (_sessions.TryGetValue(sessionName, out var session))
        {
            session.Dispose();
            _sessions.Remove(sessionName);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }
            _sessions.Clear();
            _disposed = true;
        }
    }
}
