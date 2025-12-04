// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Engine;
using System.Numerics;

namespace Neo.Fairy.Testing.Cheatcodes;

/// <summary>
/// Implementation of Foundry-style cheatcodes for Neo Fairy tests.
/// Provides vm.* style test manipulation capabilities.
/// </summary>
public sealed class FairyCheatcodes : ICheatcodes
{
    private readonly FairySessionAdapter _session;
    private readonly FairyRpcClient _rpcClient;
    private readonly Dictionary<string, string> _labels = new();
    private readonly List<StorageAccess> _recordedAccesses = new();

    private string? _prankAccount;
    private bool _isPranking;
    private bool _expectingRevert;
    private string? _expectedRevertMessage;
    private string? _expectedEvent;
    private bool _isRecording;

    public FairyCheatcodes(FairySessionAdapter session, FairyRpcClient rpcClient)
    {
        _session = session;
        _rpcClient = rpcClient;
    }

    #region Caller Manipulation

    /// <inheritdoc/>
    public void Prank(string account)
    {
        _prankAccount = account;
        _isPranking = false; // Single call only
    }

    /// <inheritdoc/>
    public void StartPrank(string account)
    {
        _prankAccount = account;
        _isPranking = true;
    }

    /// <inheritdoc/>
    public void StopPrank()
    {
        _prankAccount = null;
        _isPranking = false;
    }

    /// <summary>
    /// Gets the current prank account if set.
    /// </summary>
    public string? GetPrankAccount()
    {
        var account = _prankAccount;
        if (!_isPranking)
        {
            _prankAccount = null; // Clear after single use
        }
        return account;
    }

    #endregion

    #region Balance Manipulation

    /// <inheritdoc/>
    public void Deal(string account, long amount)
    {
        _session.SetGasBalance(account, amount);
    }

    /// <inheritdoc/>
    public void DealNeo(string account, long amount)
    {
        _session.SetNeoBalance(account, amount);
    }

    /// <inheritdoc/>
    public void DealToken(string token, string account, long amount)
    {
        _rpcClient.SetNep17BalanceAsync(_session.Id, token, account, amount).GetAwaiter().GetResult();
    }

    #endregion

    #region Time Manipulation

    /// <inheritdoc/>
    public void Warp(ulong timestamp)
    {
        _session.Timestamp = timestamp;
    }

    /// <inheritdoc/>
    public void Skip(ulong seconds)
    {
        var current = _session.Timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _session.Timestamp = current + (seconds * 1000); // Convert to milliseconds
    }

    /// <inheritdoc/>
    public void Rewind(ulong seconds)
    {
        var current = _session.Timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rewindMs = seconds * 1000;
        _session.Timestamp = current > rewindMs ? current - rewindMs : 0;
    }

    #endregion

    #region Block Manipulation

    /// <inheritdoc/>
    public void Roll(uint blockNumber)
    {
        // Block number manipulation is not yet supported in Fairy RPC.
        // The Fairy.Engine.cs has commented-out code for blockIndex override.
        // For now, we store the desired block number locally for potential future use.
        // Tests that depend on specific block numbers should use Vm.Warp() for timestamp-based logic instead.
        _desiredBlockNumber = blockNumber;

        // Log warning for test authors
        Console.WriteLine($"[FairyCheatcodes] Warning: Roll({blockNumber}) called but block index override is not yet supported in Fairy RPC. Consider using Vm.Warp() for timestamp-based logic.");
    }

    private uint? _desiredBlockNumber;

    #endregion

    #region Random Manipulation

    /// <inheritdoc/>
    public void SetRandom(ulong value)
    {
        _session.DesignatedRandom = value;
    }

    #endregion

    #region Witness Manipulation

    /// <inheritdoc/>
    public void AssumeWitness()
    {
        _session.CheckWitnessReturnTrue = true;
    }

    /// <inheritdoc/>
    public void RestoreWitness()
    {
        _session.CheckWitnessReturnTrue = false;
    }

    #endregion

    #region Expectation Cheatcodes

    /// <inheritdoc/>
    public void ExpectRevert()
    {
        _expectingRevert = true;
        _expectedRevertMessage = null;
    }

    /// <inheritdoc/>
    public void ExpectRevert(string message)
    {
        _expectingRevert = true;
        _expectedRevertMessage = message;
    }

    /// <inheritdoc/>
    public void ExpectEmit(string eventName, bool checkTopics = true, bool checkData = true)
    {
        _expectedEvent = eventName;
    }

    /// <summary>
    /// Checks if a revert was expected and validates the result.
    /// </summary>
    public void ValidateExpectations(Core.Models.ExecutionResult result)
    {
        if (_expectingRevert)
        {
            _expectingRevert = false;

            if (result.IsSuccess)
            {
                throw new AssertionFailedException(
                    "Expected revert but execution succeeded",
                    "FAULT",
                    "HALT");
            }

            if (_expectedRevertMessage != null &&
                (result.Exception == null || !result.Exception.Contains(_expectedRevertMessage)))
            {
                throw new AssertionFailedException(
                    $"Expected revert with '{_expectedRevertMessage}' but got '{result.Exception}'",
                    _expectedRevertMessage,
                    result.Exception);
            }

            _expectedRevertMessage = null;
        }

        if (_expectedEvent != null)
        {
            var found = result.Notifications.Any(n =>
                string.Equals(n.EventName, _expectedEvent, StringComparison.OrdinalIgnoreCase));

            if (!found)
            {
                var emitted = string.Join(", ", result.Notifications.Select(n => n.EventName));
                throw new AssertionFailedException(
                    $"Expected event '{_expectedEvent}' but got: [{emitted}]",
                    _expectedEvent,
                    emitted);
            }

            _expectedEvent = null;
        }
    }

    /// <summary>
    /// Returns true if currently expecting a revert.
    /// </summary>
    public bool IsExpectingRevert => _expectingRevert;

    #endregion

    #region Snapshot Management

    /// <inheritdoc/>
    public string Snapshot()
    {
        return _session.CreateSnapshot();
    }

    /// <inheritdoc/>
    public bool RevertTo(string snapshotId)
    {
        return _session.RevertToSnapshot(snapshotId);
    }

    #endregion

    #region Fuzz Testing

    /// <inheritdoc/>
    public void Assume(bool condition)
    {
        if (!condition)
        {
            throw new AssumeViolationException();
        }
    }

    /// <inheritdoc/>
    public T Bound<T>(T value, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0) return min;
        if (value.CompareTo(max) > 0) return max;
        return value;
    }

    #endregion

    #region Debugging

    /// <inheritdoc/>
    public void Label(string account, string label)
    {
        _labels[account] = label;
    }

    /// <summary>
    /// Gets the label for an account if set.
    /// </summary>
    public string? GetLabel(string account)
    {
        return _labels.TryGetValue(account, out var label) ? label : null;
    }

    /// <inheritdoc/>
    public void StartRecording()
    {
        _isRecording = true;
        _recordedAccesses.Clear();
    }

    /// <inheritdoc/>
    public StorageAccess[] StopRecording()
    {
        _isRecording = false;
        return _recordedAccesses.ToArray();
    }

    /// <summary>
    /// Records a storage access if recording is enabled.
    /// </summary>
    public void RecordAccess(string contractHash, byte[] key, bool isRead, byte[]? value = null)
    {
        if (_isRecording)
        {
            _recordedAccesses.Add(new StorageAccess
            {
                ContractHash = contractHash,
                Key = key,
                IsRead = isRead,
                Value = value
            });
        }
    }

    #endregion
}
