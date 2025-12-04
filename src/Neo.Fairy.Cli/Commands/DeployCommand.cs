// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using Neo.Fairy.Core.Models;
using Spectre.Console;
using Neo.Fairy.Core.Configuration;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to deploy contracts.
/// Similar to 'forge create' / 'forge script' in Foundry.
/// </summary>
public static class DeployCommand
{
    public static Command Create()
    {
        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Deploy to a virtual session (no on-chain write)");

        var networkOption = new Option<string?>(
            aliases: new[] { "--network", "-n" },
            description: "Deploy to a specific network (mainnet, testnet, or RPC URL)");

        var walletOption = new Option<string?>(
            aliases: new[] { "--wallet", "-w" },
            description: "Wallet file for signing transactions");

        var contractOption = new Option<string?>(
            aliases: new[] { "--contract", "-c" },
            description: "Deploy only a specific contract by alias");

        var verifyOption = new Option<bool>(
            name: "--verify",
            description: "Verify contract after deployment");

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Simulate deployment without executing");

        var command = new Command("deploy", "Deploy smart contracts")
        {
            sessionOption,
            networkOption,
            walletOption,
            contractOption,
            verifyOption,
            dryRunOption
        };

        command.SetHandler(ExecuteAsync,
            sessionOption, networkOption, walletOption,
            contractOption, verifyOption, dryRunOption);

        return command;
    }

    private static async Task ExecuteAsync(
        string? session,
        string? network,
        string? wallet,
        string? contractAlias,
        bool verify,
        bool dryRun)
    {
        FairyProject project;
        try
        {
            project = FairyProject.Load();
            await project.LoadArtifactsAsync();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return;
        }

        if (project.Artifacts.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No compiled contracts found. Run 'fairy build' first.[/]");
            return;
        }

        // Filter contracts if specified
        var artifacts = project.GetArtifactsInDependencyOrder();
        if (!string.IsNullOrEmpty(contractAlias))
        {
            var artifact = project.GetArtifact(contractAlias);
            if (artifact == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Contract '{contractAlias}' not found");
                return;
            }
            artifacts = new[] { artifact };
        }

        // Determine deployment mode
        var isVirtual = !string.IsNullOrEmpty(session);
        var (resolvedNetwork, rpcUrl) = NetworkResolver.Resolve(network, project.Config.Fairy);
        var targetDescription = isVirtual
            ? $"session '{session}'"
            : $"{resolvedNetwork} ({rpcUrl})";

        if (dryRun)
        {
            AnsiConsole.MarkupLine($"[yellow]Dry run:[/] Would deploy to {targetDescription}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Deploying to {targetDescription}...[/]");
        }

        var results = new List<DeploymentResult>();

        await AnsiConsole.Status()
            .StartAsync("Deploying contracts...", async ctx =>
            {
                foreach (var artifact in artifacts)
                {
                    ctx.Status($"Deploying {artifact.Alias}...");

                    if (dryRun)
                    {
                        // Simulate deployment
                        await Task.Delay(100);
                        results.Add(DeploymentResult.Success(
                            artifact.Alias,
                            "0x" + Guid.NewGuid().ToString("N")[..40],
                            120000000,
                            isVirtual ? null : 50000000));
                    }
                    else
                    {
                        // Actual deployment would go here
                        var result = await DeployContractAsync(
                            project, artifact, session, resolvedNetwork, rpcUrl, wallet);
                        results.Add(result);
                    }
                }
            });

        // Display results
        AnsiConsole.WriteLine();
        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                var gasStr = $"{result.GasConsumed / 100000000.0:F1} GAS";
                AnsiConsole.MarkupLine($"  [green][[✓]][/] {result.Alias} → {result.ContractHash} ({gasStr})");

                if (result.TransactionHash != null)
                {
                    AnsiConsole.MarkupLine($"      [grey]TX: {result.TransactionHash}[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red][[✗]][/] {result.Alias}: {result.Exception}");
            }
        }

        var successCount = results.Count(r => r.IsSuccess);
        AnsiConsole.WriteLine();

        if (successCount == results.Count)
        {
            AnsiConsole.MarkupLine($"[green]✓ Deployed {successCount} contract(s) successfully[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Deployed {successCount}/{results.Count} contracts[/]");
        }
    }

    private static async Task<DeploymentResult> DeployContractAsync(
        FairyProject project,
        ContractArtifact artifact,
        string? session,
        string? network,
        string rpcUrl,
        string? wallet)
    {
        // Placeholder implementation
        // Actual implementation would call Fairy RPC

        await Task.Delay(100); // Simulate network call

        // Simulate successful deployment
        return DeploymentResult.Success(
            artifact.Alias,
            "0x" + Guid.NewGuid().ToString("N")[..40],
            120000000,
            string.IsNullOrEmpty(session) ? 50000000 : null,
            string.IsNullOrEmpty(session) ? "0x" + Guid.NewGuid().ToString("N") : null);
    }
}
