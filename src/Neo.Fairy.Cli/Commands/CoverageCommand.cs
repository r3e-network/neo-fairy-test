// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Services;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Configuration;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to generate coverage reports from a running Fairy node without running tests.
/// </summary>
public static class CoverageCommand
{
    public static Command Create()
    {
        var contractsArgument = new Argument<string[]>(
            name: "contracts",
            description: "Contract hashes (0x...) or workspace aliases. If omitted, uses all deployments recorded for the workspace.")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var workspaceOption = new Option<string?>(
            aliases: new[] { "--workspace", "-wsp" },
            description: "Workspace name to resolve aliases and default contract list (defaults to project name)");

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "RPC endpoint URL (defaults to fairy.toml or http://localhost:16868)");

        var outOption = new Option<string?>(
            aliases: new[] { "--out", "-o", "--coverage-out" },
            description: "Write coverage reports to a directory (defaults to <out>/coverage)");

        var command = new Command("coverage", "Generate coverage reports from the Fairy node")
        {
            contractsArgument,
            workspaceOption,
            rpcOption,
            outOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(contractsArgument),
                ctx.ParseResult.GetValueForOption(workspaceOption),
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(outOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string[] contracts,
        string? workspace,
        string? rpcUrl,
        string? outputDirectory)
    {
        var project = TryLoadProject(out var projectLoaded);

        var resolvedRpcUrl = RpcUrlResolver.Resolve(rpcUrl, project);

        var workspaceName = workspace
                            ?? (projectLoaded ? project.Config.Project.Name : null)
                            ?? "default";

        var client = new FairyRpcClient(resolvedRpcUrl);

        Dictionary<string, string?> contractsByHash;
        if (contracts.Length == 0)
        {
            try
            {
                contractsByHash = await LoadWorkspaceContractsAsync(client, workspaceName);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to query workspace `{workspaceName}`:[/] {ex.Message.EscapeMarkup()}");
                return 1;
            }

            if (contractsByHash.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No deployments recorded for workspace `{workspaceName}`.[/]");
                AnsiConsole.MarkupLine("[grey]Deploy via `fairy deploy --session <name>` or use workspace RPCs first.[/]");
                return 1;
            }
        }
        else
        {
            contractsByHash = await ResolveContractsAsync(client, workspaceName, contracts);
            if (contractsByHash.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No contracts resolved for coverage.[/]");
                return 1;
            }
        }

        AnsiConsole.MarkupLine($"[grey]Collecting coverage from[/] {resolvedRpcUrl}");
        var ok = await CoverageCliHelper.PrintCoverageAsync(project, contractsByHash, outputDirectory, resolvedRpcUrl);
        return ok ? 0 : 1;
    }

    private static FairyProject TryLoadProject(out bool loaded)
    {
        try
        {
            loaded = true;
            return FairyProject.Load();
        }
        catch
        {
            loaded = false;
            var cwd = Directory.GetCurrentDirectory();
            return new FairyProject
            {
                RootDirectory = cwd,
                Config = new FairyConfig
                {
                    Project = new ProjectConfig
                    {
                        Name = "default",
                        Out = "out"
                    },
                    Fairy = new FairyRuntimeConfig()
                }
            };
        }
    }

    private static async Task<Dictionary<string, string?>> LoadWorkspaceContractsAsync(
        FairyRpcClient client,
        string workspaceName)
    {
        var aliasToHash = await client.GetWorkspaceContractHashesAsync(workspaceName);

        var hashToAlias = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (alias, hash) in aliasToHash)
        {
            if (!string.IsNullOrWhiteSpace(hash))
            {
                hashToAlias[hash] = alias;
            }
        }

        return hashToAlias;
    }

    private static async Task<Dictionary<string, string?>> ResolveContractsAsync(
        FairyRpcClient client,
        string workspaceName,
        IReadOnlyList<string> contracts)
    {
        var needsAliasResolution = contracts.Any(c => !CliArgumentParser.LooksLikeHash(c));
        IReadOnlyDictionary<string, string> aliasToHash =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (needsAliasResolution)
        {
            try
            {
                aliasToHash = await client.GetWorkspaceContractHashesAsync(workspaceName);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to query workspace `{workspaceName}`:[/] {ex.Message.EscapeMarkup()}");
                return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }
        }

        var hashToLabel = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var contract in contracts)
        {
            if (CliArgumentParser.LooksLikeHash(contract))
            {
                hashToLabel[contract] = null;
                continue;
            }

            if (!aliasToHash.TryGetValue(contract, out var resolvedHash) ||
                string.IsNullOrWhiteSpace(resolvedHash))
            {
                AnsiConsole.MarkupLine($"[yellow]No deployment recorded for alias `{contract}` in workspace `{workspaceName}`.[/]");
                continue;
            }

            hashToLabel[resolvedHash] = contract;
        }

        return hashToLabel;
    }
}
