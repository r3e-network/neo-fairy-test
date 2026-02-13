// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to manage Fairy workspaces (Foundry-style multi-contract bundles).
/// </summary>
public static class WorkspaceCommand
{
    public static Command Create()
    {
        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL");

        var workspaceOption = new Option<string?>(
            aliases: new[] { "--workspace", "-wsp" },
            description: "Workspace name (defaults to project name)");

        var command = new Command("workspace", "Manage Fairy workspaces")
        {
            rpcOption,
            workspaceOption
        };

        command.AddCommand(CreateListCommand(rpcOption));
        command.AddCommand(CreateContractsCommand(rpcOption, workspaceOption));
        command.AddCommand(CreateHashesCommand(rpcOption, workspaceOption));
        command.AddCommand(CreateClearCommand(rpcOption, workspaceOption));

        return command;
    }

    private static Command CreateListCommand(Option<string?> rpcOption)
    {
        var command = new Command("list", "List workspaces registered on the node");

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, _) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));

            IReadOnlyList<string> workspaces;
            try
            {
                workspaces = await client.ListWorkspacesAsync();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to list workspaces:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
                return;
            }

            if (workspaces.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No workspaces registered on node.[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded).Title("Workspaces");
            table.AddColumn("Name");
            foreach (var ws in workspaces.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                table.AddRow(ws);
            }

            AnsiConsole.Write(table);
        });

        return command;
    }

    private static Command CreateContractsCommand(
        Option<string?> rpcOption,
        Option<string?> workspaceOption)
    {
        var workspaceArg = new Argument<string?>(
            name: "workspace",
            description: "Workspace name (optional)");

        workspaceArg.Arity = ArgumentArity.ZeroOrOne;

        var detailsOption = new Option<bool>(
            aliases: new[] { "--details", "-d" },
            description: "Show manifest name, data flag, and default signer count");

        var command = new Command("contracts", "List contracts registered in a workspace")
        {
            workspaceArg,
            detailsOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var rpcUrl = ctx.ParseResult.GetValueForOption(rpcOption);
            var workspaceOpt = ctx.ParseResult.GetValueForOption(workspaceOption);
            var workspaceName = ctx.ParseResult.GetValueForArgument(workspaceArg);
            var details = ctx.ParseResult.GetValueForOption(detailsOption);

            var (client, project) = ResolveClient(rpcUrl);
            var resolvedWorkspace = workspaceName
                                    ?? workspaceOpt
                                    ?? project?.Config.Project.Name
                                    ?? "default";

            IReadOnlyList<object?> contracts;
            try
            {
                contracts = await client.ListWorkspaceContractsAsync(resolvedWorkspace, verbose: details);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to list workspace contracts:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
                return;
            }

            if (contracts.Count == 0)
            {
                AnsiConsole.MarkupLine($"[grey]Workspace `{resolvedWorkspace}` is empty.[/]");
                return;
            }

            if (!details)
            {
                var table = new Table().Border(TableBorder.Rounded).Title($"Workspace `{resolvedWorkspace}` Contracts");
                table.AddColumn("Alias");
                foreach (var aliasObj in contracts)
                {
                    table.AddRow(aliasObj?.ToString() ?? string.Empty);
                }
                AnsiConsole.Write(table);
                return;
            }

            var detailTable = new Table().Border(TableBorder.Rounded).Title($"Workspace `{resolvedWorkspace}` Contracts");
            detailTable.AddColumn("Alias");
            detailTable.AddColumn("Manifest Name");
            detailTable.AddColumn("Has Data");
            detailTable.AddColumn("Default Signers");

            foreach (var item in contracts)
            {
                if (item is Dictionary<string, object?> dict)
                {
                    var alias = dict.GetValueOrDefault("alias")?.ToString() ?? string.Empty;
                    var name = dict.GetValueOrDefault("manifestname")?.ToString() ?? string.Empty;
                    var hasData = dict.GetValueOrDefault("hasdata")?.ToString() ?? "false";
                    var signers = dict.GetValueOrDefault("signers")?.ToString() ?? "0";
                    detailTable.AddRow(alias, name, hasData, signers);
                }
            }

            AnsiConsole.Write(detailTable);
        });

        return command;
    }

    private static Command CreateHashesCommand(
        Option<string?> rpcOption,
        Option<string?> workspaceOption)
    {
        var workspaceArg = new Argument<string?>(
            name: "workspace",
            description: "Workspace name (optional)");

        workspaceArg.Arity = ArgumentArity.ZeroOrOne;

        var command = new Command("hashes", "Show last deployed hashes for a workspace")
        {
            workspaceArg
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var rpcUrl = ctx.ParseResult.GetValueForOption(rpcOption);
            var workspaceOpt = ctx.ParseResult.GetValueForOption(workspaceOption);
            var workspaceName = ctx.ParseResult.GetValueForArgument(workspaceArg);

            var (client, project) = ResolveClient(rpcUrl);
            var resolvedWorkspace = workspaceName
                                    ?? workspaceOpt
                                    ?? project?.Config.Project.Name
                                    ?? "default";

            IReadOnlyDictionary<string, string> hashes;
            try
            {
                hashes = await client.GetWorkspaceContractHashesAsync(resolvedWorkspace);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to get workspace hashes:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
                return;
            }

            if (hashes.Count == 0)
            {
                AnsiConsole.MarkupLine($"[grey]No deployments recorded for workspace `{resolvedWorkspace}`.[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded).Title($"Workspace `{resolvedWorkspace}` Deployments");
            table.AddColumn("Alias");
            table.AddColumn("Script Hash");

            foreach (var (alias, hash) in hashes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                table.AddRow(alias, hash);
            }

            AnsiConsole.Write(table);
        });

        return command;
    }

    private static Command CreateClearCommand(
        Option<string?> rpcOption,
        Option<string?> workspaceOption)
    {
        var workspaceArg = new Argument<string?>(
            name: "workspace",
            description: "Workspace name (optional)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var aliasArg = new Argument<string?>(
            name: "alias",
            description: "Alias to remove (optional; clears entire workspace when omitted)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var yesOption = new Option<bool>(
            aliases: new[] { "--yes", "-y" },
            description: "Skip confirmation prompt");

        var command = new Command("clear", "Clear a workspace or remove an alias")
        {
            workspaceArg,
            aliasArg,
            yesOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var rpcUrl = ctx.ParseResult.GetValueForOption(rpcOption);
            var workspaceOpt = ctx.ParseResult.GetValueForOption(workspaceOption);
            var workspaceName = ctx.ParseResult.GetValueForArgument(workspaceArg);
            var alias = ctx.ParseResult.GetValueForArgument(aliasArg);
            var yes = ctx.ParseResult.GetValueForOption(yesOption);

            var (client, project) = ResolveClient(rpcUrl);
            var resolvedWorkspace = workspaceName
                                    ?? workspaceOpt
                                    ?? project?.Config.Project.Name
                                    ?? "default";

            if (!yes)
            {
                var message = string.IsNullOrWhiteSpace(alias)
                    ? $"Clear workspace `{resolvedWorkspace}` and all deployment records?"
                    : $"Remove alias `{alias}` from workspace `{resolvedWorkspace}`?";

                if (!AnsiConsole.Confirm(message))
                {
                    AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                    return;
                }
            }

            try
            {
                var response = await client.ClearWorkspaceAsync(resolvedWorkspace, string.IsNullOrWhiteSpace(alias) ? null : alias);
                var removedObj = response.GetValueOrDefault("removed");
                var removed = removedObj is bool b
                    ? b
                    : bool.TryParse(removedObj?.ToString(), out var parsed) && parsed;

                if (string.IsNullOrWhiteSpace(alias))
                {
                    if (removed)
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Cleared workspace `{resolvedWorkspace}`.");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Workspace `{resolvedWorkspace}` not found.[/]");
                    }
                }
                else
                {
                    if (removed)
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] Removed `{alias}` from workspace `{resolvedWorkspace}`.");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]Alias `{alias}` not found in workspace `{resolvedWorkspace}`.[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to clear workspace:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
            }
        });

        return command;
    }

    private static (FairyRpcClient Client, FairyProject? Project) ResolveClient(string? rpcUrl)
    {
        FairyProject? project = null;
        if (string.IsNullOrWhiteSpace(rpcUrl))
        {
            try
            {
                project = FairyProject.Load();
            }
            catch
            {
                // Not inside a Fairy project.
            }
        }

        var resolvedRpc = RpcUrlResolver.Resolve(rpcUrl, project);
        return (new FairyRpcClient(resolvedRpc), project);
    }
}
