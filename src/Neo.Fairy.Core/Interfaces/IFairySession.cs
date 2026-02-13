// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Core.Interfaces;

/// <summary>
/// Defines the contract for a Fairy test session.
/// Sessions provide isolated execution environments with snapshot-based state management.
/// </summary>
public interface IFairySession : IDisposable
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets or sets the custom timestamp for Runtime.Time.
    /// </summary>
    ulong? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the custom random value for Runtime.GetRandom.
    /// </summary>
    ulong? DesignatedRandom { get; set; }

    /// <summary>
    /// Gets or sets whether witness checks should always return true.
    /// </summary>
    bool CheckWitnessReturnTrue { get; set; }

    /// <summary>
    /// Gets or sets the block index override for this session.
    /// </summary>
    uint? BlockIndex { get; set; }

    /// <summary>
    /// Gets or sets the network magic number for this session.
    /// </summary>
    uint? NetworkMagic { get; set; }

    /// <summary>
    /// Creates a snapshot of the current session state.
    /// </summary>
    /// <returns>A snapshot identifier that can be used to restore state.</returns>
    string CreateSnapshot();

    /// <summary>
    /// Reverts the session to a previously created snapshot.
    /// </summary>
    /// <param name="snapshotId">The snapshot identifier.</param>
    /// <returns>True if the revert was successful.</returns>
    bool RevertToSnapshot(string snapshotId);

    /// <summary>
    /// Clones this session to create a new independent session.
    /// </summary>
    /// <param name="newSessionId">The identifier for the new session.</param>
    /// <returns>A new session with copied state.</returns>
    IFairySession Clone(string newSessionId);

    /// <summary>
    /// Gets the deployed contract hash by alias within this session.
    /// </summary>
    /// <param name="alias">The contract alias.</param>
    /// <returns>The contract hash if found, null otherwise.</returns>
    string? GetContractHash(string alias);

    /// <summary>
    /// Registers a deployed contract with an alias.
    /// </summary>
    /// <param name="alias">The contract alias.</param>
    /// <param name="contractHash">The contract hash.</param>
    void RegisterContract(string alias, string contractHash);

    /// <summary>
    /// Gets the session creation time.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the last activity time.
    /// </summary>
    DateTime LastActivityAt { get; }

    /// <summary>
    /// Updates the last activity timestamp.
    /// </summary>
    void Touch();
}
