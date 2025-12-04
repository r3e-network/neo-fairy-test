// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Models;

namespace Neo.Fairy.Core.Interfaces;

/// <summary>
/// Defines the contract for deploying smart contracts.
/// Supports both virtual (session-based) and real (on-chain) deployments.
/// </summary>
public interface IContractDeployer
{
    /// <summary>
    /// Deploys a contract to a virtual session (no on-chain write).
    /// </summary>
    /// <param name="session">The target session.</param>
    /// <param name="artifact">The contract artifact to deploy.</param>
    /// <param name="options">Deployment options.</param>
    /// <returns>The deployment result.</returns>
    Task<DeploymentResult> DeployVirtualAsync(
        IFairySession session,
        ContractArtifact artifact,
        DeploymentOptions? options = null);

    /// <summary>
    /// Deploys a contract to the real blockchain.
    /// </summary>
    /// <param name="artifact">The contract artifact to deploy.</param>
    /// <param name="options">Deployment options including wallet.</param>
    /// <returns>The deployment result with transaction hash.</returns>
    Task<DeploymentResult> DeployToChainAsync(
        ContractArtifact artifact,
        DeploymentOptions options);

    /// <summary>
    /// Deploys multiple contracts in dependency order.
    /// </summary>
    /// <param name="session">The target session.</param>
    /// <param name="artifacts">The contract artifacts to deploy.</param>
    /// <param name="options">Deployment options.</param>
    /// <returns>Results for each deployment.</returns>
    Task<IReadOnlyList<DeploymentResult>> DeployWorkspaceAsync(
        IFairySession session,
        IReadOnlyList<ContractArtifact> artifacts,
        DeploymentOptions? options = null);
}

/// <summary>
/// Options for contract deployment.
/// </summary>
public record DeploymentOptions
{
    /// <summary>
    /// Custom signers for the deployment transaction.
    /// </summary>
    public IReadOnlyList<SignerInfo>? Signers { get; init; }

    /// <summary>
    /// Initialization data to pass to the contract's _deploy method.
    /// </summary>
    public object? InitializationData { get; init; }

    /// <summary>
    /// Path to the wallet file for on-chain deployment.
    /// </summary>
    public string? WalletPath { get; init; }

    /// <summary>
    /// Wallet password for on-chain deployment.
    /// </summary>
    public string? WalletPassword { get; init; }

    /// <summary>
    /// Whether to stop deployment on first failure.
    /// </summary>
    public bool StopOnFailure { get; init; } = true;

    /// <summary>
    /// Whether to verify the contract after deployment.
    /// </summary>
    public bool Verify { get; init; } = false;
}
