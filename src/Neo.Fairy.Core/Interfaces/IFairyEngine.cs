// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Models;

namespace Neo.Fairy.Core.Interfaces;

/// <summary>
/// Defines the contract for the Fairy execution engine.
/// Responsible for executing smart contract scripts in isolated environments.
/// </summary>
public interface IFairyEngine
{
    /// <summary>
    /// Executes a script within the specified session context.
    /// </summary>
    /// <param name="session">The session providing execution context.</param>
    /// <param name="script">The script bytecode to execute.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>The execution result containing state, stack, and notifications.</returns>
    ExecutionResult Execute(IFairySession session, byte[] script, ExecutionOptions? options = null);

    /// <summary>
    /// Invokes a specific contract method.
    /// </summary>
    /// <param name="session">The session providing execution context.</param>
    /// <param name="contractHash">The contract script hash.</param>
    /// <param name="method">The method name to invoke.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="options">Optional execution options.</param>
    /// <returns>The execution result.</returns>
    ExecutionResult InvokeMethod(
        IFairySession session,
        string contractHash,
        string method,
        object[]? args = null,
        ExecutionOptions? options = null);

    /// <summary>
    /// Gets the current gas consumed by the engine.
    /// </summary>
    long GasConsumed { get; }

    /// <summary>
    /// Gets the current execution state.
    /// </summary>
    ExecutionState State { get; }
}

/// <summary>
/// Options for script execution.
/// </summary>
public record ExecutionOptions
{
    /// <summary>
    /// Maximum gas allowed for execution.
    /// </summary>
    public long MaxGas { get; init; } = 200_00000000;

    /// <summary>
    /// Whether to persist state changes after execution.
    /// </summary>
    public bool PersistChanges { get; init; } = true;

    /// <summary>
    /// Custom signers for the transaction.
    /// </summary>
    public IReadOnlyList<SignerInfo>? Signers { get; init; }

    /// <summary>
    /// Whether to collect code coverage data.
    /// </summary>
    public bool CollectCoverage { get; init; } = false;
}

/// <summary>
/// Represents a transaction signer.
/// </summary>
public record SignerInfo
{
    /// <summary>
    /// The signer's account hash.
    /// </summary>
    public required string Account { get; init; }

    /// <summary>
    /// The witness scope.
    /// </summary>
    public string Scopes { get; init; } = "CalledByEntry";

    /// <summary>
    /// Allowed contracts for CustomContracts scope.
    /// </summary>
    public IReadOnlyList<string>? AllowedContracts { get; init; }
}

/// <summary>
/// Represents the execution state of the VM.
/// </summary>
public enum ExecutionState
{
    None,
    Halt,
    Fault,
    Break
}
