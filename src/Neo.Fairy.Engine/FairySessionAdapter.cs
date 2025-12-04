// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using System.Numerics;

namespace Neo.Fairy.Engine;

/// <summary>
/// Adapter that implements IFairySession interface and bridges to the existing Fairy RPC.
/// </summary>
public sealed class FairySessionAdapter : IFairySession
{
    private readonly FairyRpcClient _rpcClient;
    private readonly Dictionary<string, string> _contractAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _snapshots = new();
    private bool _disposed;

    private ulong? _timestamp;
    private ulong? _designatedRandom;
    private bool _checkWitnessReturnTrue;

    /// <summary>
    /// Creates a new session adapter.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="rpcClient">The RPC client to use.</param>
    public FairySessionAdapter(string sessionId, FairyRpcClient rpcClient)
    {
        Id = sessionId;
        _rpcClient = rpcClient;
        CreatedAt = DateTime.UtcNow;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; }

    /// <inheritdoc/>
    public DateTime LastActivityAt { get; private set; }

    /// <inheritdoc/>
    public ulong? Timestamp
    {
        get => _timestamp;
        set
        {
            _timestamp = value;
            _rpcClient.SetTimestampAsync(Id, value).GetAwaiter().GetResult();
            Touch();
        }
    }

    /// <inheritdoc/>
    public ulong? DesignatedRandom
    {
        get => _designatedRandom;
        set
        {
            _designatedRandom = value;
            var bigInt = value.HasValue ? new BigInteger(value.Value) : (BigInteger?)null;
            _rpcClient.SetRandomAsync(Id, bigInt).GetAwaiter().GetResult();
            Touch();
        }
    }

    /// <inheritdoc/>
    public bool CheckWitnessReturnTrue
    {
        get => _checkWitnessReturnTrue;
        set
        {
            _checkWitnessReturnTrue = value;
            _rpcClient.SetCheckWitnessAsync(Id, value).GetAwaiter().GetResult();
            Touch();
        }
    }

    /// <inheritdoc/>
    public string CreateSnapshot()
    {
        // Fairy doesn't have explicit snapshot creation via RPC
        // We simulate by tracking the current state
        var snapshotId = $"{Id}_snap_{_snapshots.Count}";
        _snapshots.Add(snapshotId);
        Touch();
        return snapshotId;
    }

    /// <inheritdoc/>
    public bool RevertToSnapshot(string snapshotId)
    {
        // Fairy doesn't have explicit snapshot revert via RPC
        // This would need to be implemented in the Fairy plugin
        Touch();
        return _snapshots.Contains(snapshotId);
    }

    /// <inheritdoc/>
    public IFairySession Clone(string newSessionId)
    {
        // Create a new session - Fairy will clone from existing if same base
        var newSession = new FairySessionAdapter(newSessionId, _rpcClient);

        // Copy settings
        if (_timestamp.HasValue)
            newSession.Timestamp = _timestamp;
        if (_designatedRandom.HasValue)
            newSession.DesignatedRandom = _designatedRandom;
        newSession.CheckWitnessReturnTrue = _checkWitnessReturnTrue;

        // Copy contract aliases
        foreach (var kvp in _contractAliases)
        {
            newSession.RegisterContract(kvp.Key, kvp.Value);
        }

        return newSession;
    }

    /// <inheritdoc/>
    public string? GetContractHash(string alias)
    {
        return _contractAliases.TryGetValue(alias, out var hash) ? hash : null;
    }

    /// <inheritdoc/>
    public void RegisterContract(string alias, string contractHash)
    {
        _contractAliases[alias] = contractHash;
        Touch();
    }

    /// <inheritdoc/>
    public void Touch()
    {
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the GAS balance for an account.
    /// </summary>
    public void SetGasBalance(string account, long balance)
    {
        _rpcClient.SetGasBalanceAsync(Id, account, balance).GetAwaiter().GetResult();
        Touch();
    }

    /// <summary>
    /// Sets the NEO balance for an account.
    /// </summary>
    public void SetNeoBalance(string account, long balance)
    {
        _rpcClient.SetNeoBalanceAsync(Id, account, balance).GetAwaiter().GetResult();
        Touch();
    }

    /// <summary>
    /// Gets all registered contract aliases.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllContracts()
    {
        return _contractAliases;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _contractAliases.Clear();
            _snapshots.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Factory for creating Fairy sessions.
/// </summary>
public sealed class FairySessionFactory
{
    private readonly FairyRpcClient _rpcClient;
    private int _sessionCounter;

    public FairySessionFactory(string rpcUrl = "http://localhost:16868")
    {
        _rpcClient = new FairyRpcClient(rpcUrl);
    }

    /// <summary>
    /// Creates a new session with an auto-generated ID.
    /// </summary>
    public FairySessionAdapter CreateSession()
    {
        var sessionId = $"fairy_session_{Interlocked.Increment(ref _sessionCounter)}_{DateTime.UtcNow.Ticks}";
        return new FairySessionAdapter(sessionId, _rpcClient);
    }

    /// <summary>
    /// Creates a new session with a specific ID.
    /// </summary>
    public FairySessionAdapter CreateSession(string sessionId)
    {
        return new FairySessionAdapter(sessionId, _rpcClient);
    }

    /// <summary>
    /// Gets the underlying RPC client.
    /// </summary>
    public FairyRpcClient RpcClient => _rpcClient;

    /// <summary>
    /// Checks if the Fairy RPC is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync()
    {
        return await _rpcClient.PingAsync();
    }
}
