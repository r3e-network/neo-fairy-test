// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;

namespace Neo.Fairy.Core.Models;

/// <summary>
/// Represents the result of a contract deployment.
/// </summary>
public sealed class DeploymentResult
{
    /// <summary>
    /// Gets the contract alias.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the deployed contract hash.
    /// </summary>
    public required string ContractHash { get; init; }

    /// <summary>
    /// Gets the execution state.
    /// </summary>
    public required ExecutionState State { get; init; }

    /// <summary>
    /// Gets the GAS consumed for deployment.
    /// </summary>
    public required long GasConsumed { get; init; }

    /// <summary>
    /// Gets the network fee (for on-chain deployment).
    /// </summary>
    public long? NetworkFee { get; init; }

    /// <summary>
    /// Gets the transaction hash (for on-chain deployment).
    /// </summary>
    public string? TransactionHash { get; init; }

    /// <summary>
    /// Gets the exception message if deployment failed.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Gets whether the deployment was successful.
    /// </summary>
    public bool IsSuccess => State == ExecutionState.Halt;

    /// <summary>
    /// Gets whether the contract already existed.
    /// </summary>
    public bool AlreadyExists { get; init; }

    /// <summary>
    /// Gets any additional notes about the deployment.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Creates a successful deployment result.
    /// </summary>
    public static DeploymentResult Success(
        string alias,
        string contractHash,
        long gasConsumed,
        long? networkFee = null,
        string? transactionHash = null)
    {
        return new DeploymentResult
        {
            Alias = alias,
            ContractHash = contractHash,
            State = ExecutionState.Halt,
            GasConsumed = gasConsumed,
            NetworkFee = networkFee,
            TransactionHash = transactionHash
        };
    }

    /// <summary>
    /// Creates a failed deployment result.
    /// </summary>
    public static DeploymentResult Failure(
        string alias,
        string exception,
        long gasConsumed = 0)
    {
        return new DeploymentResult
        {
            Alias = alias,
            ContractHash = string.Empty,
            State = ExecutionState.Fault,
            GasConsumed = gasConsumed,
            Exception = exception
        };
    }

    /// <summary>
    /// Creates a result for an already existing contract.
    /// </summary>
    public static DeploymentResult Existing(
        string alias,
        string contractHash)
    {
        return new DeploymentResult
        {
            Alias = alias,
            ContractHash = contractHash,
            State = ExecutionState.Halt,
            GasConsumed = 0,
            AlreadyExists = true,
            Note = "Contract already exists"
        };
    }
}
