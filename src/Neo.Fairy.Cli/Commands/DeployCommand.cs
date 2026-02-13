// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Services;
using Neo.Fairy.Engine;
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

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL (overrides --network and fairy.toml)");

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

        var broadcastOption = new Option<bool>(
            name: "--broadcast",
            description: "Relay deploy transactions to chain (requires session wallet)");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "Wallet password (or FAIRY_WALLET_PASSWORD env)");

        var waitOption = new Option<bool>(
            name: "--wait",
            description: "Wait for relayed transactions to be confirmed");

        var waitBlocksOption = new Option<uint>(
            name: "--wait-blocks",
            description: "Number of blocks to wait for confirmation",
            getDefaultValue: () => 2);

        var workspaceOption = new Option<string?>(
            aliases: new[] { "--workspace", "-wsp" },
            description: "Workspace name for multi-contract deploy (defaults to project name)");

        var command = new Command("deploy", "Deploy smart contracts")
        {
            sessionOption,
            networkOption,
            rpcOption,
            walletOption,
            contractOption,
            verifyOption,
            dryRunOption,
            broadcastOption,
            passwordOption,
            waitOption,
            waitBlocksOption,
            workspaceOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForOption(sessionOption),
                ctx.ParseResult.GetValueForOption(networkOption),
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(walletOption),
                ctx.ParseResult.GetValueForOption(contractOption),
                ctx.ParseResult.GetValueForOption(verifyOption),
                ctx.ParseResult.GetValueForOption(dryRunOption),
                ctx.ParseResult.GetValueForOption(broadcastOption),
                ctx.ParseResult.GetValueForOption(passwordOption),
                ctx.ParseResult.GetValueForOption(waitOption),
                ctx.ParseResult.GetValueForOption(waitBlocksOption),
                ctx.ParseResult.GetValueForOption(workspaceOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string? session,
        string? network,
        string? rpcUrl,
        string? wallet,
        string? contractAlias,
        bool verify,
        bool dryRun,
        bool broadcast,
        string? password,
        bool wait,
        uint waitBlocks,
        string? workspace)
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
            return 1;
        }

        if (project.Artifacts.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No compiled contracts found. Run 'fairy build' first.[/]");
            return 1;
        }

        // Filter contracts if specified
        var artifacts = project.GetArtifactsInDependencyOrder();
        if (!string.IsNullOrEmpty(contractAlias))
        {
            var artifact = project.GetArtifact(contractAlias);
            if (artifact == null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Contract '{contractAlias}' not found");
                return 1;
            }
            artifacts = new[] { artifact };
        }

        // Determine deployment mode
        var isVirtual = !string.IsNullOrEmpty(session);
        var (resolvedNetwork, resolvedRpcUrl) = NetworkResolver.Resolve(network, project.Config.Fairy);
        var rpcEndpoint = rpcUrl ?? resolvedRpcUrl;
        var targetDescription = isVirtual
            ? $"session '{session}' (Fairy RPC {rpcEndpoint})"
            : $"{resolvedNetwork} (Fairy RPC {rpcEndpoint})";
        var workspaceName = workspace ?? project.Config.Project.Name;

        if (broadcast && string.IsNullOrEmpty(session))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --broadcast requires --session so a signing wallet can be attached.");
            return 1;
        }

        if (!broadcast && string.IsNullOrEmpty(session) && !dryRun)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Virtual deployment requires --session.");
            AnsiConsole.MarkupLine("[grey]Example: fairy deploy --session dev[/]");
            return 1;
        }

        if (dryRun)
        {
            AnsiConsole.MarkupLine($"[yellow]Dry run:[/] Would deploy to {targetDescription}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Deploying to {targetDescription}...[/]");
        }

        if (verify)
        {
            AnsiConsole.MarkupLine("[grey]Verify flag requested; verification is not supported in CLI yet.[/]");
        }

        var client = new FairyRpcClient(rpcEndpoint);

        if (broadcast && !dryRun)
        {
            try
            {
                var walletSpec = WalletLoader.Load(wallet, password, project);
                if (walletSpec.Nep2Keys != null)
                {
                    await client.SetSessionWalletWithNep2Async(session!, walletSpec.Nep2Keys, walletSpec.Password);
                }
                else if (walletSpec.Wifs != null)
                {
                    await client.SetSessionWalletWithWifAsync(session!, walletSpec.Wifs.ToArray());
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to configure session wallet:[/] {ex.Message.EscapeMarkup()}");
                return 1;
            }
        }

        var results = new List<DeploymentResult>();

        await AnsiConsole.Status()
            .StartAsync("Deploying contracts...", async ctx =>
            {
                // Sync artifacts to workspace first so alias-based calls work.
                foreach (var artifact in artifacts)
                {
                    ctx.Status($"Registering {artifact.Alias} in workspace...");
                    await client.UpsertWorkspaceContractAsync(workspaceName, artifact);
                }

                if (dryRun)
                {
                    foreach (var artifact in artifacts)
                    {
                        results.Add(DeploymentResult.Success(
                            artifact.Alias,
                            "0x" + Guid.NewGuid().ToString("N")[..40],
                            0));
                    }
                    return;
                }

                ctx.Status($"Deploying workspace {workspaceName}...");
                var aliasFilter = !string.IsNullOrEmpty(contractAlias)
                    ? artifacts.Select(a => a.Alias).ToArray()
                    : null;

                IReadOnlyList<DeploymentResult> deployResults = broadcast
                    ? await client.RelayDeployWorkspaceAsync(workspaceName, session!, aliasFilter)
                    : await client.VirtualDeployWorkspaceAsync(workspaceName, session!, aliasFilter);

                results.AddRange(deployResults);
            });

        // Display results
        AnsiConsole.WriteLine();
        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                var gasStr = $"{result.GasConsumed / 100000000.0:F1} GAS";
                AnsiConsole.MarkupLine($"  [green][[✓]][/] {result.Alias} → {result.ContractHash} ({gasStr})");

                var pendingSig = string.Equals(result.Note, "Pending signature", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(result.Note))
                {
                    AnsiConsole.MarkupLine($"      [yellow]{result.Note}[/]");
                }

                if (result.TransactionHash != null)
                {
                    AnsiConsole.MarkupLine($"      [grey]TX: {result.TransactionHash}[/]");

                    if (broadcast && wait && pendingSig)
                    {
                        AnsiConsole.MarkupLine("      [grey]Skipping confirmation wait until signatures are complete.[/]");
                    }

                    if (broadcast && wait && !pendingSig)
                    {
                        AnsiConsole.MarkupLine("      [grey]Waiting for confirmation...[/]");
                        try
                        {
                            var confirmed = await client.AwaitConfirmedTransactionAsync(
                                result.TransactionHash,
                                verbose: true,
                                waitBlocks: waitBlocks);

                            var confirmations = confirmed.GetValueOrDefault("confirmations")?.ToString();
                            var blockHash = confirmed.GetValueOrDefault("blockhash")?.ToString();
                            if (!string.IsNullOrEmpty(confirmations))
                            {
                                AnsiConsole.MarkupLine($"      [green]Confirmed[/] ({confirmations} confirmations, block {blockHash})");
                            }
                            else
                            {
                                AnsiConsole.MarkupLine("      [green]Confirmed[/]");
                            }
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"      [yellow]Confirmation wait failed:[/] {ex.Message.EscapeMarkup()}");
                        }
                    }
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

        return results.All(r => r.IsSuccess) ? 0 : 1;
    }
}
