// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Engine;
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
    private readonly Dictionary<string, string> _envVars = new();
    private readonly Dictionary<(string, string), object> _mockedCalls = new();
    private readonly Dictionary<(string, string), string?> _mockedReverts = new();
    private readonly Queue<(string target, string method)> _expectedCalls = new();
    private readonly Dictionary<(string, string), uint> _expectedCallCounts = new();
    private readonly Dictionary<(string, string), uint> _actualCallCounts = new();
    private readonly List<string> _broadcastTxs = new();
    private readonly List<string> _logMessages = new();
    private readonly Dictionary<string, ForkInfo> _forks = new();
    private readonly HashSet<string> _persistentContracts = new();
    private readonly List<ExpectedEventInfo> _expectedEvents = new();

    private string? _prankAccount;
    private bool _isPranking;
    private bool _expectingRevert;
    private string? _expectedRevertMessage;
    private bool _isRecording;
    private bool _isBroadcasting;
    private string? _broadcastSender;
    private uint? _desiredBlockNumber;
    private string? _activeForkId;
    private int _forkCounter;

    public FairyCheatcodes(FairySessionAdapter session, FairyRpcClient rpcClient)
    {
        _session = session;
        _rpcClient = rpcClient;
    }

    /// <summary>
    /// Resets all cheatcode state. Called between tests if instance is reused.
    /// </summary>
    public void Reset()
    {
        _prankAccount = null;
        _isPranking = false;
        _expectingRevert = false;
        _expectedRevertMessage = null;
        _isRecording = false;
        _isBroadcasting = false;
        _broadcastSender = null;
        _desiredBlockNumber = null;
        _activeForkId = null;
        _forkCounter = 0;
        _labels.Clear();
        _recordedAccesses.Clear();
        _envVars.Clear();
        _mockedCalls.Clear();
        _mockedReverts.Clear();
        _expectedCalls.Clear();
        _expectedCallCounts.Clear();
        _actualCallCounts.Clear();
        _broadcastTxs.Clear();
        _logMessages.Clear();
        _forks.Clear();
        _persistentContracts.Clear();
        _expectedEvents.Clear();
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
        _session.Timestamp = current + (seconds * 1000);
    }

    /// <summary>
    /// Skips forward in time by the specified number of milliseconds.
    /// </summary>
    public void SkipMs(ulong milliseconds)
    {
        var current = _session.Timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _session.Timestamp = current + milliseconds;
    }

    /// <inheritdoc/>
    public void Rewind(ulong seconds)
    {
        var current = _session.Timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rewindMs = seconds * 1000;
        _session.Timestamp = current > rewindMs ? current - rewindMs : 0;
    }

    /// <summary>
    /// Rewinds time by the specified number of milliseconds.
    /// </summary>
    public void RewindMs(ulong milliseconds)
    {
        var current = _session.Timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _session.Timestamp = current > milliseconds ? current - milliseconds : 0;
    }

    #endregion

    #region Block Manipulation

    /// <inheritdoc/>
    public void Roll(uint blockNumber)
    {
        _desiredBlockNumber = blockNumber;
        // Attempt to set via session if supported
        _session.BlockIndex = blockNumber;
    }

    /// <inheritdoc/>
    public uint GetBlockNumber()
    {
        return _desiredBlockNumber ?? _session.BlockIndex ?? 0;
    }

    /// <inheritdoc/>
    public uint ChainId()
    {
        return _session.NetworkMagic ?? TestDefaults.PrivateNetMagic;
    }

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
        _expectedEvents.Add(new ExpectedEventInfo
        {
            EventName = eventName,
            CheckContract = checkTopics,
            CheckArgs = checkData
        });
    }

    /// <summary>
    /// Expects a specific event with contract hash and optional arguments.
    /// </summary>
    /// <param name="contractHash">The contract hash that should emit the event.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="expectedArgs">Optional expected arguments.</param>
    public void ExpectEmit(string contractHash, string eventName, object[]? expectedArgs = null)
    {
        _expectedEvents.Add(new ExpectedEventInfo
        {
            ContractHash = contractHash,
            EventName = eventName,
            ExpectedArgs = expectedArgs,
            CheckContract = true,
            CheckArgs = expectedArgs != null
        });
    }

    /// <inheritdoc/>
    public void ExpectCall(string target, string method)
    {
        _expectedCalls.Enqueue((target, method));
    }

    /// <inheritdoc/>
    public void ExpectCallCount(string target, string method, uint count)
    {
        _expectedCallCounts[(target, method)] = count;
    }

    /// <summary>
    /// Checks if a revert was expected and validates the result.
    /// </summary>
    public void ValidateExpectations(Core.Models.ExecutionResult result, string? target = null, string? method = null)
    {
        // Eagerly capture and clear revert state to prevent leaks if ExpectCall throws
        var wasExpectingRevert = _expectingRevert;
        var expectedRevertMsg = _expectedRevertMessage;
        _expectingRevert = false;
        _expectedRevertMessage = null;

        // Track actual call counts for ExpectCallCount validation
        if (target != null && method != null)
        {
            var key = (target, method);
            _actualCallCounts[key] = _actualCallCounts.TryGetValue(key, out var c) ? c + 1 : 1;

            // Validate ExpectCall queue (FIFO: first expected matches first actual)
            if (_expectedCalls.Count > 0)
            {
                var expected = _expectedCalls.Dequeue();

                if (!string.Equals(expected.target, target, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(expected.method, method, StringComparison.OrdinalIgnoreCase))
                {
                    throw new AssertionFailedException(
                        $"Expected call to {expected.target}.{expected.method}() but got {target}.{method}()",
                        $"{expected.target}.{expected.method}",
                        $"{target}.{method}");
                }
            }
        }
        if (wasExpectingRevert)
        {
            if (result.IsSuccess)
            {
                throw new AssertionFailedException(
                    "Expected revert but execution succeeded",
                    "FAULT",
                    "HALT");
            }

            if (expectedRevertMsg != null &&
                (result.Exception == null || !result.Exception.Contains(expectedRevertMsg)))
            {
                throw new AssertionFailedException(
                    $"Expected revert with '{expectedRevertMsg}' but got '{result.Exception}'",
                    expectedRevertMsg,
                    result.Exception);
            }
        }

        // Validate all expected events
        try
        {
            foreach (var expected in _expectedEvents)
            {
                var matchingNotifications = (result.Notifications ?? Array.Empty<Core.Models.NotificationInfo>()).Where(n =>
                    string.Equals(n.EventName, expected.EventName, StringComparison.OrdinalIgnoreCase));

                // Check contract hash if required
                if (expected.CheckContract && expected.ContractHash != null)
                {
                    matchingNotifications = matchingNotifications.Where(n =>
                        string.Equals(n.ContractHash, expected.ContractHash, StringComparison.OrdinalIgnoreCase));
                }

                var found = matchingNotifications.FirstOrDefault();
                if (found == null)
                {
                    var emitted = string.Join(", ", (result.Notifications ?? Array.Empty<Core.Models.NotificationInfo>()).Select(n =>
                        expected.CheckContract && expected.ContractHash != null
                            ? $"{n.ContractHash}:{n.EventName}"
                            : n.EventName));
                    var expectedDesc = expected.CheckContract && expected.ContractHash != null
                        ? $"{expected.ContractHash}:{expected.EventName}"
                        : expected.EventName;
                    throw new AssertionFailedException(
                        $"Expected event '{expectedDesc}' but got: [{emitted}]",
                        expectedDesc,
                        emitted);
                }

                // Check arguments if required
                if (expected.CheckArgs && expected.ExpectedArgs != null)
                {
                    var actualArgs = found.State;
                    for (int i = 0; i < expected.ExpectedArgs.Length; i++)
                    {
                        if (actualArgs == null || actualArgs.Count <= i)
                        {
                            throw new AssertionFailedException(
                                $"Expected event argument at index {i} but only {actualArgs?.Count ?? 0} arguments were emitted",
                                expected.ExpectedArgs.Length.ToString(),
                                (actualArgs?.Count ?? 0).ToString());
                        }

                        var expectedArg = expected.ExpectedArgs[i]?.ToString();
                        var actualArg = actualArgs[i]?.ToString();
                        if (!string.Equals(expectedArg, actualArg, StringComparison.Ordinal))
                        {
                            throw new AssertionFailedException(
                                $"Event argument mismatch at index {i}",
                                expectedArg,
                                actualArg);
                        }
                    }
                }
            }
        }
        finally
        {
            _expectedEvents.Clear();
        }
    }

    /// <summary>
    /// Returns true if currently expecting a revert.
    /// </summary>
    public bool IsExpectingRevert => _expectingRevert;

    /// <summary>
    /// Validates end-of-test expectations (ExpectCallCount, unconsumed ExpectCall).
    /// Called by TestRunner after each test method completes.
    /// </summary>
    public void ValidateFinalExpectations()
    {
        // Any unconsumed ExpectCall entries mean expected calls never happened
        if (_expectedCalls.Count > 0)
        {
            var missing = string.Join(", ", _expectedCalls.Select(e => $"{e.target}.{e.method}()"));
            _expectedCalls.Clear();
            _expectedCallCounts.Clear();
            _actualCallCounts.Clear();
            throw new AssertionFailedException(
                $"Expected calls were never made: {missing}",
                missing,
                "(no call)");
        }

        // Validate ExpectCallCount entries - collect all mismatches
        var mismatches = new List<string>();
        foreach (var (key, expectedCount) in _expectedCallCounts)
        {
            _actualCallCounts.TryGetValue(key, out var actualCount);
            if (actualCount != expectedCount)
            {
                mismatches.Add($"Expected {expectedCount} call(s) to {key.Item1}.{key.Item2}() but got {actualCount}");
            }
        }

        _expectedCallCounts.Clear();
        _actualCallCounts.Clear();

        if (mismatches.Count > 0)
        {
            throw new AssertionFailedException(
                string.Join("; ", mismatches));
        }
    }

    #endregion

    #region Call Mocking

    /// <inheritdoc/>
    public void MockCall(string target, string method, object returnData)
    {
        _mockedCalls[(target, method)] = returnData;
    }

    /// <inheritdoc/>
    public void MockCallRevert(string target, string method, string? revertMessage = null)
    {
        _mockedReverts[(target, method)] = revertMessage;
    }

    /// <inheritdoc/>
    public void ClearMockedCalls(string? target = null)
    {
        if (target == null)
        {
            _mockedCalls.Clear();
            _mockedReverts.Clear();
        }
        else
        {
            var keysToRemove = _mockedCalls.Keys.Where(k => k.Item1 == target).ToList();
            foreach (var key in keysToRemove)
                _mockedCalls.Remove(key);

            var revertKeysToRemove = _mockedReverts.Keys.Where(k => k.Item1 == target).ToList();
            foreach (var key in revertKeysToRemove)
                _mockedReverts.Remove(key);
        }
    }

    /// <summary>
    /// Checks if a call is mocked and returns the mock data.
    /// </summary>
    public (bool isMocked, object? returnData, bool shouldRevert, string? revertMessage) GetMock(string target, string method)
    {
        if (_mockedReverts.TryGetValue((target, method), out var revertMsg))
            return (true, null, true, revertMsg);

        if (_mockedCalls.TryGetValue((target, method), out var data))
            return (true, data, false, null);

        return (false, null, false, null);
    }

    #endregion

    #region Storage Manipulation

    /// <inheritdoc/>
    public void Store(string target, byte[] key, byte[] value)
    {
        _rpcClient.SetStorageAsync(_session.Id, target, key, value).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public byte[]? Load(string target, byte[] key)
    {
        return _rpcClient.GetStorageAsync(_session.Id, target, key).GetAwaiter().GetResult();
    }

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

    #region Broadcast Mode

    /// <inheritdoc/>
    public void StartBroadcast()
    {
        _isBroadcasting = true;
        _broadcastSender = null;
        _broadcastTxs.Clear();
    }

    /// <inheritdoc/>
    public void StartBroadcast(string sender)
    {
        _isBroadcasting = true;
        _broadcastSender = sender;
        _broadcastTxs.Clear();
    }

    /// <inheritdoc/>
    public string[] StopBroadcast()
    {
        _isBroadcasting = false;
        var txs = _broadcastTxs.ToArray();
        _broadcastTxs.Clear();
        _broadcastSender = null;
        return txs;
    }

    /// <summary>
    /// Returns true if currently in broadcast mode.
    /// </summary>
    public bool IsBroadcasting => _isBroadcasting;

    /// <summary>
    /// Gets the broadcast sender if set.
    /// </summary>
    public string? BroadcastSender => _broadcastSender;

    /// <summary>
    /// Records a broadcast transaction.
    /// </summary>
    public void RecordBroadcastTx(string txHash)
    {
        if (_isBroadcasting)
            _broadcastTxs.Add(txHash);
    }

    #endregion

    #region Environment Variables

    /// <inheritdoc/>
    public void SetEnv(string name, string value)
    {
        _envVars[name] = value;
    }

    /// <inheritdoc/>
    public string? GetEnv(string name)
    {
        if (_envVars.TryGetValue(name, out var value))
            return value;
        return Environment.GetEnvironmentVariable(name);
    }

    /// <inheritdoc/>
    public string GetEnvOr(string name, string defaultValue)
    {
        return GetEnv(name) ?? defaultValue;
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
        if (min.CompareTo(max) > 0)
            throw new ArgumentException($"min ({min}) must be <= max ({max})");
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
        var result = _recordedAccesses.ToArray();
        _recordedAccesses.Clear();
        return result;
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

    /// <inheritdoc/>
    public void Log(string message)
    {
        _logMessages.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        Console.WriteLine($"[Vm.Log] {message}");
    }

    /// <inheritdoc/>
    public void Log(string format, params object[] args)
    {
        if (args.Length == 0) { Log(format); return; }
        Log(string.Format(format, args));
    }

    /// <summary>
    /// Gets all log messages recorded during the test.
    /// </summary>
    public IReadOnlyList<string> GetLogs() => _logMessages.AsReadOnly();

    /// <summary>
    /// Clears all log messages.
    /// </summary>
    public void ClearLogs() => _logMessages.Clear();

    #endregion

    #region Fork Testing

    /// <inheritdoc/>
    public string CreateFork(string rpcUrl)
    {
        return CreateFork(rpcUrl, 0);
    }

    /// <inheritdoc/>
    public string CreateFork(string rpcUrl, uint blockNumber)
    {
        var forkId = $"fork_{++_forkCounter}";
        var fork = new ForkInfo
        {
            Id = forkId,
            RpcUrl = rpcUrl,
            BlockNumber = blockNumber,
            CreatedAt = DateTime.UtcNow
        };

        _forks[forkId] = fork;

        // Auto-select the first fork created
        if (_activeForkId == null)
        {
            _activeForkId = forkId;
        }

        return forkId;
    }

    /// <inheritdoc/>
    public void SelectFork(string forkId)
    {
        if (!_forks.ContainsKey(forkId))
        {
            throw new InvalidOperationException($"Fork '{forkId}' does not exist");
        }

        _activeForkId = forkId;
    }

    /// <inheritdoc/>
    public string? ActiveFork()
    {
        return _activeForkId;
    }

    /// <inheritdoc/>
    public void RollFork(uint blockNumber)
    {
        if (_activeForkId == null)
        {
            throw new InvalidOperationException("No active fork. Call CreateFork first.");
        }

        RollFork(_activeForkId, blockNumber);
    }

    /// <inheritdoc/>
    public void RollFork(string forkId, uint blockNumber)
    {
        if (!_forks.TryGetValue(forkId, out var fork))
        {
            throw new InvalidOperationException($"Fork '{forkId}' does not exist");
        }

        fork.BlockNumber = blockNumber;
    }

    /// <inheritdoc/>
    public void MakePersistent(params string[] contractHashes)
    {
        foreach (var hash in contractHashes)
        {
            _persistentContracts.Add(hash);
        }
    }

    /// <inheritdoc/>
    public void RevokePersistent(params string[] contractHashes)
    {
        foreach (var hash in contractHashes)
        {
            _persistentContracts.Remove(hash);
        }
    }

    /// <inheritdoc/>
    public bool IsPersistent(string contractHash)
    {
        return _persistentContracts.Contains(contractHash);
    }

    /// <summary>
    /// Gets information about a fork.
    /// </summary>
    public ForkInfo? GetForkInfo(string forkId)
    {
        return _forks.TryGetValue(forkId, out var fork) ? fork : null;
    }

    /// <summary>
    /// Gets all registered forks.
    /// </summary>
    public IReadOnlyDictionary<string, ForkInfo> GetAllForks() => _forks;

    #endregion
}

/// <summary>
/// Represents an expected event emission for validation.
/// </summary>
public sealed class ExpectedEventInfo
{
    /// <summary>
    /// Gets the expected contract hash (optional).
    /// </summary>
    public string? ContractHash { get; init; }

    /// <summary>
    /// Gets the expected event name.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the expected arguments (optional).
    /// </summary>
    public object[]? ExpectedArgs { get; init; }

    /// <summary>
    /// Gets whether to check the contract hash.
    /// </summary>
    public bool CheckContract { get; init; }

    /// <summary>
    /// Gets whether to check the event arguments.
    /// </summary>
    public bool CheckArgs { get; init; }
}

/// <summary>
/// Information about a fork for testing.
/// </summary>
public sealed class ForkInfo
{
    /// <summary>
    /// Gets the fork identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the RPC URL this fork was created from.
    /// </summary>
    public required string RpcUrl { get; init; }

    /// <summary>
    /// Gets or sets the current block number for the fork.
    /// </summary>
    public uint BlockNumber { get; set; }

    /// <summary>
    /// Gets the time when this fork was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
