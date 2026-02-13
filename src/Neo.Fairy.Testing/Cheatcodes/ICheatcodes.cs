// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Testing.Cheatcodes;

/// <summary>
/// Defines cheatcodes for test manipulation.
/// Inspired by Foundry's vm.* cheatcodes.
/// </summary>
public interface ICheatcodes
{
    #region Caller Manipulation

    /// <summary>
    /// Sets the caller (msg.sender) for the next contract call only.
    /// </summary>
    /// <param name="account">The account hash to use as caller.</param>
    void Prank(string account);

    /// <summary>
    /// Sets the caller (msg.sender) for all subsequent calls until StopPrank is called.
    /// </summary>
    /// <param name="account">The account hash to use as caller.</param>
    void StartPrank(string account);

    /// <summary>
    /// Stops the ongoing prank, reverting to normal caller behavior.
    /// </summary>
    void StopPrank();

    #endregion

    #region Balance Manipulation

    /// <summary>
    /// Sets the GAS balance of an account.
    /// </summary>
    /// <param name="account">The account hash.</param>
    /// <param name="amount">The balance amount in GAS fractions (1 GAS = 100000000).</param>
    void Deal(string account, long amount);

    /// <summary>
    /// Sets the NEO balance of an account.
    /// </summary>
    /// <param name="account">The account hash.</param>
    /// <param name="amount">The balance amount.</param>
    void DealNeo(string account, long amount);

    /// <summary>
    /// Sets the balance of a specific token for an account.
    /// </summary>
    /// <param name="token">The token contract hash.</param>
    /// <param name="account">The account hash.</param>
    /// <param name="amount">The balance amount.</param>
    void DealToken(string token, string account, long amount);

    #endregion

    #region Time Manipulation

    /// <summary>
    /// Sets the block timestamp for Runtime.Time.
    /// </summary>
    /// <param name="timestamp">The timestamp in milliseconds.</param>
    void Warp(ulong timestamp);

    /// <summary>
    /// Advances the block timestamp by a duration.
    /// </summary>
    /// <param name="seconds">Seconds to advance.</param>
    void Skip(ulong seconds);

    /// <summary>
    /// Rewinds the block timestamp by a duration.
    /// </summary>
    /// <param name="seconds">Seconds to rewind.</param>
    void Rewind(ulong seconds);

    #endregion

    #region Block Manipulation

    /// <summary>
    /// Sets the block number/index.
    /// </summary>
    /// <param name="blockNumber">The block number.</param>
    /// <remarks>
    /// Note: Block number override requires RPC server support.
    /// Check FairyEngine capabilities before relying on this feature.
    /// </remarks>
    void Roll(uint blockNumber);

    /// <summary>
    /// Gets the current block number.
    /// </summary>
    /// <returns>The current block number.</returns>
    uint GetBlockNumber();

    /// <summary>
    /// Gets the current chain ID / network magic.
    /// </summary>
    /// <returns>The chain ID.</returns>
    uint ChainId();

    #endregion

    #region Random Manipulation

    /// <summary>
    /// Sets the value returned by Runtime.GetRandom.
    /// </summary>
    /// <param name="value">The random value.</param>
    void SetRandom(ulong value);

    #endregion

    #region Witness Manipulation

    /// <summary>
    /// Makes all witness checks return true.
    /// </summary>
    void AssumeWitness();

    /// <summary>
    /// Restores normal witness checking behavior.
    /// </summary>
    void RestoreWitness();

    #endregion

    #region Expectation Cheatcodes

    /// <summary>
    /// Expects the next call to revert.
    /// </summary>
    void ExpectRevert();

    /// <summary>
    /// Expects the next call to revert with a specific message.
    /// </summary>
    /// <param name="message">The expected revert message.</param>
    void ExpectRevert(string message);

    /// <summary>
    /// Expects a specific event to be emitted.
    /// </summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="checkTopics">Whether to check indexed topics.</param>
    /// <param name="checkData">Whether to check event data.</param>
    void ExpectEmit(string eventName, bool checkTopics = true, bool checkData = true);

    /// <summary>
    /// Expects a contract call to be made.
    /// </summary>
    /// <param name="target">The target contract hash.</param>
    /// <param name="method">The method name expected to be called.</param>
    void ExpectCall(string target, string method);

    /// <summary>
    /// Expects a contract call to be made N times.
    /// </summary>
    /// <param name="target">The target contract hash.</param>
    /// <param name="method">The method name expected to be called.</param>
    /// <param name="count">The expected number of calls.</param>
    void ExpectCallCount(string target, string method, uint count);

    #endregion

    #region Call Mocking

    /// <summary>
    /// Mocks a contract call to return specific data.
    /// </summary>
    /// <param name="target">The target contract hash.</param>
    /// <param name="method">The method name to mock.</param>
    /// <param name="returnData">The data to return from the mocked call.</param>
    void MockCall(string target, string method, object returnData);

    /// <summary>
    /// Mocks a contract call to revert.
    /// </summary>
    /// <param name="target">The target contract hash.</param>
    /// <param name="method">The method name to mock.</param>
    /// <param name="revertMessage">The revert message.</param>
    void MockCallRevert(string target, string method, string? revertMessage = null);

    /// <summary>
    /// Clears all mocked calls for a contract.
    /// </summary>
    /// <param name="target">The target contract hash, or null to clear all mocks.</param>
    void ClearMockedCalls(string? target = null);

    #endregion

    #region Storage Manipulation

    /// <summary>
    /// Directly stores a value in contract storage.
    /// </summary>
    /// <param name="target">The target contract hash.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The value to store.</param>
    void Store(string target, byte[] key, byte[] value);

    /// <summary>
    /// Directly loads a value from contract storage.
    /// </summary>
    /// <param name="target">The target contract hash.</param>
    /// <param name="key">The storage key.</param>
    /// <returns>The stored value, or null if not found.</returns>
    byte[]? Load(string target, byte[] key);

    #endregion

    #region Snapshot Management

    /// <summary>
    /// Creates a snapshot of the current state.
    /// </summary>
    /// <returns>The snapshot ID.</returns>
    string Snapshot();

    /// <summary>
    /// Reverts to a previously created snapshot.
    /// </summary>
    /// <param name="snapshotId">The snapshot ID.</param>
    /// <returns>True if successful.</returns>
    bool RevertTo(string snapshotId);

    #endregion

    #region Broadcast Mode

    /// <summary>
    /// Starts broadcast mode - subsequent calls will be queued for real transaction broadcast.
    /// </summary>
    void StartBroadcast();

    /// <summary>
    /// Starts broadcast mode with a specific sender account.
    /// </summary>
    /// <param name="sender">The sender account for transactions.</param>
    void StartBroadcast(string sender);

    /// <summary>
    /// Stops broadcast mode and returns queued transactions.
    /// </summary>
    /// <returns>List of transaction hashes that were broadcast.</returns>
    string[] StopBroadcast();

    #endregion

    #region Environment Variables

    /// <summary>
    /// Sets an environment variable for the test session.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The variable value.</param>
    void SetEnv(string name, string value);

    /// <summary>
    /// Gets an environment variable value.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The variable value, or null if not set.</returns>
    string? GetEnv(string name);

    /// <summary>
    /// Gets an environment variable value with a default.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="defaultValue">The default value if not set.</param>
    /// <returns>The variable value or default.</returns>
    string GetEnvOr(string name, string defaultValue);

    #endregion

    #region Fuzz Testing

    /// <summary>
    /// Skips the current fuzz run if the condition is false.
    /// Used to filter out invalid fuzz inputs.
    /// </summary>
    /// <param name="condition">The condition that must be true.</param>
    void Assume(bool condition);

    /// <summary>
    /// Bounds a value to a range for fuzz testing.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The input value.</param>
    /// <param name="min">The minimum bound.</param>
    /// <param name="max">The maximum bound.</param>
    /// <returns>The bounded value.</returns>
    T Bound<T>(T value, T min, T max) where T : IComparable<T>;

    #endregion

    #region Debugging

    /// <summary>
    /// Labels an address for better trace output.
    /// </summary>
    /// <param name="account">The account hash.</param>
    /// <param name="label">The label to display.</param>
    void Label(string account, string label);

    /// <summary>
    /// Starts recording all storage reads and writes.
    /// </summary>
    void StartRecording();

    /// <summary>
    /// Stops recording and returns the recorded accesses.
    /// </summary>
    /// <returns>The recorded storage accesses.</returns>
    StorageAccess[] StopRecording();

    /// <summary>
    /// Logs a message to the test output.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Log(string message);

    /// <summary>
    /// Logs a formatted message to the test output.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <param name="args">The format arguments.</param>
    void Log(string format, params object[] args);

    #endregion

    #region Fork Testing

    /// <summary>
    /// Creates a fork from a remote RPC endpoint.
    /// This allows testing against mainnet/testnet state without affecting real state.
    /// </summary>
    /// <param name="rpcUrl">The RPC endpoint URL to fork from.</param>
    /// <returns>The fork ID.</returns>
    /// <remarks>
    /// Fork testing enables:
    /// - Testing against live mainnet/testnet contract state
    /// - Simulating transactions without broadcasting
    /// - Debugging production issues locally
    /// </remarks>
    string CreateFork(string rpcUrl);

    /// <summary>
    /// Creates a fork from a remote RPC at a specific block.
    /// </summary>
    /// <param name="rpcUrl">The RPC endpoint URL to fork from.</param>
    /// <param name="blockNumber">The block number to fork at.</param>
    /// <returns>The fork ID.</returns>
    string CreateFork(string rpcUrl, uint blockNumber);

    /// <summary>
    /// Selects a fork to use for subsequent operations.
    /// </summary>
    /// <param name="forkId">The fork ID returned by CreateFork.</param>
    void SelectFork(string forkId);

    /// <summary>
    /// Gets the currently active fork ID.
    /// </summary>
    /// <returns>The active fork ID, or null if no fork is active.</returns>
    string? ActiveFork();

    /// <summary>
    /// Rolls the fork to a specific block number.
    /// </summary>
    /// <param name="blockNumber">The target block number.</param>
    void RollFork(uint blockNumber);

    /// <summary>
    /// Rolls a specific fork to a block number.
    /// </summary>
    /// <param name="forkId">The fork ID.</param>
    /// <param name="blockNumber">The target block number.</param>
    void RollFork(string forkId, uint blockNumber);

    /// <summary>
    /// Makes a persistent state change on the current fork.
    /// By default, fork state changes are ephemeral.
    /// </summary>
    void MakePersistent(params string[] contractHashes);

    /// <summary>
    /// Revokes persistence for contracts.
    /// </summary>
    void RevokePersistent(params string[] contractHashes);

    /// <summary>
    /// Checks if a contract has persistent state on the fork.
    /// </summary>
    /// <param name="contractHash">The contract hash.</param>
    /// <returns>True if persistent.</returns>
    bool IsPersistent(string contractHash);

    #endregion
}

/// <summary>
/// Represents a recorded storage access.
/// </summary>
public sealed class StorageAccess
{
    /// <summary>
    /// Gets the contract that accessed storage.
    /// </summary>
    public required string ContractHash { get; init; }

    /// <summary>
    /// Gets the storage key.
    /// </summary>
    public required byte[] Key { get; init; }

    /// <summary>
    /// Gets whether this was a read operation.
    /// </summary>
    public required bool IsRead { get; init; }

    /// <summary>
    /// Gets whether this was a write operation.
    /// </summary>
    public bool IsWrite => !IsRead;

    /// <summary>
    /// Gets the value (for writes) or null (for reads).
    /// </summary>
    public byte[]? Value { get; init; }
}

/// <summary>
/// Exception thrown when Vm.Assume condition is false.
/// Used to skip invalid fuzz inputs.
/// </summary>
public sealed class AssumeViolationException : Exception
{
    public AssumeViolationException() : base("Assume condition was false") { }
}
