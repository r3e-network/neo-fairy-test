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
    private int _snapshotCounter;
    private bool _disposed;

    private ulong? _timestamp;
    private ulong? _designatedRandom;
    private bool _checkWitnessReturnTrue;
    private uint? _blockIndex;
    private uint? _networkMagic;

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FairySessionAdapter));
    }

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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            _checkWitnessReturnTrue = value;
            _rpcClient.SetCheckWitnessAsync(Id, value).GetAwaiter().GetResult();
            Touch();
        }
    }

    /// <summary>
    /// Gets or sets the block index for this session.
    /// Note: Block number override may not be fully supported by the RPC server.
    /// </summary>
    public uint? BlockIndex
    {
        get => _blockIndex;
        set
        {
            ThrowIfDisposed();
            _blockIndex = value;
            Touch();
        }
    }

    /// <summary>
    /// Gets or sets the network magic number for this session.
    /// </summary>
    public uint? NetworkMagic
    {
        get => _networkMagic;
        set
        {
            ThrowIfDisposed();
            _networkMagic = value;
            Touch();
        }
    }

    /// <inheritdoc/>
    public string CreateSnapshot()
    {
        ThrowIfDisposed();
        var snapshotId = $"{Id}_snap_{_snapshotCounter++}";
        EnsureSessionExists();

        // Fairy snapshots are represented as separate sessions.
        // Copy current session state into a new snapshot session.
        _rpcClient.CopySnapshotAsync(Id, snapshotId).GetAwaiter().GetResult();

        _snapshots.Add(snapshotId);
        Touch();
        return snapshotId;
    }

    /// <inheritdoc/>
    public bool RevertToSnapshot(string snapshotId)
    {
        ThrowIfDisposed();
        // Only allow reverting to snapshots created through this adapter.
        if (!_snapshots.Contains(snapshotId))
        {
            Touch();
            return false;
        }

        EnsureSessionExists();

        // Restore by copying snapshot state back onto this session id.
        _rpcClient.CopySnapshotAsync(snapshotId, Id).GetAwaiter().GetResult();
        RefreshRuntimeArgsFromServer();
        Touch();
        return true;
    }

    /// <inheritdoc/>
    public IFairySession Clone(string newSessionId)
    {
        ThrowIfDisposed();
        EnsureSessionExists();

        // Clone server-side state into the new session.
        _rpcClient.CopySnapshotAsync(Id, newSessionId).GetAwaiter().GetResult();

        var newSession = new FairySessionAdapter(newSessionId, _rpcClient);

        // Copy local alias map for wrapper convenience.
        foreach (var kvp in _contractAliases)
        {
            newSession.RegisterContract(kvp.Key, kvp.Value);
        }

        newSession.RefreshRuntimeArgsFromServer();
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
        ThrowIfDisposed();
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
        ThrowIfDisposed();
        _rpcClient.SetGasBalanceAsync(Id, account, balance).GetAwaiter().GetResult();
        Touch();
    }

    /// <summary>
    /// Sets the NEO balance for an account.
    /// </summary>
    public void SetNeoBalance(string account, long balance)
    {
        ThrowIfDisposed();
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

    private void EnsureSessionExists()
    {
        // Reading snapshot metadata forces server-side session creation without mutating state.
        _rpcClient.GetSnapshotTimestampAsync(Id).GetAwaiter().GetResult();
    }

    private void RefreshRuntimeArgsFromServer()
    {
        try
        {
            var timestamps = _rpcClient.GetSnapshotTimestampAsync(Id).GetAwaiter().GetResult();
            if (timestamps.TryGetValue(Id, out var ts))
            {
                _timestamp = ts;
            }

            var randoms = _rpcClient.GetSnapshotRandomAsync(Id).GetAwaiter().GetResult();
            if (randoms.TryGetValue(Id, out var rnd))
            {
                _designatedRandom = rnd;
            }

            var witnesses = _rpcClient.GetSnapshotCheckWitnessAsync(Id).GetAwaiter().GetResult();
            if (witnesses.TryGetValue(Id, out var cw))
            {
                _checkWitnessReturnTrue = cw;
            }
        }
        catch
        {
            // If the RPC doesn't support metadata queries, keep local values.
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Clean up server-side sessions and snapshots to prevent orphaned state.
            try
            {
                var toDelete = new List<string>(_snapshots) { Id };
                _rpcClient.DeleteSnapshotsAsync(toDelete.ToArray()).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Best-effort cleanup; don't fail tests if the server is unreachable.
            }

            _contractAliases.Clear();
            _snapshots.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Factory for creating Fairy sessions.
/// </summary>
public sealed class FairySessionFactory : IDisposable
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

    /// <summary>
    /// Disposes the underlying RPC client.
    /// </summary>
    public void Dispose()
    {
        _rpcClient.Dispose();
    }
}
