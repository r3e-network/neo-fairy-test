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
    void Roll(uint blockNumber);

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
