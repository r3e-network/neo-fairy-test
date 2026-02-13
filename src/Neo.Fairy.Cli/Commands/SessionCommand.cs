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
/// Command to manage Fairy snapshots/sessions (Foundry-style).
/// </summary>
public static class SessionCommand
{
    public static Command Create()
    {
        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL");

        var command = new Command("session", "Manage Fairy sessions (snapshots)")
        {
            rpcOption
        };

        command.AddCommand(CreateListCommand(rpcOption));
        command.AddCommand(CreateNewCommand(rpcOption));
        command.AddCommand(CreateCloneCommand(rpcOption));
        command.AddCommand(CreateRenameCommand(rpcOption));
        command.AddCommand(CreateDeleteCommand(rpcOption));
        command.AddCommand(CreateInfoCommand(rpcOption));

        return command;
    }

    private static Command CreateListCommand(Option<string?> rpcOption)
    {
        var detailsOption = new Option<bool>(
            aliases: new[] { "--details", "-d" },
            description: "Show timestamp, random, and witness flags");

        var command = new Command("list", "List sessions on the node")
        {
            detailsOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, resolvedRpc) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));
            var details = ctx.ParseResult.GetValueForOption(detailsOption);

            IReadOnlyList<string> sessions;
            try
            {
                sessions = await client.ListSnapshotsAsync();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to list sessions:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
                return;
            }

            if (sessions.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No sessions found on node.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[grey]RPC:[/] {resolvedRpc}");

            if (!details)
            {
                var table = new Table().Border(TableBorder.Rounded).Title("Sessions");
                table.AddColumn("Name");
                foreach (var s in sessions.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                {
                    table.AddRow(s);
                }

                AnsiConsole.Write(table);
                return;
            }

            IReadOnlyDictionary<string, ulong?> timestamps = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, ulong?> randoms = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, bool> witnesses = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            try
            {
                timestamps = await client.GetSnapshotTimestampAsync(sessions.ToArray());
                randoms = await client.GetSnapshotRandomAsync(sessions.ToArray());
                witnesses = await client.GetSnapshotCheckWitnessAsync(sessions.ToArray());
            }
            catch
            {
                // Best-effort details; still show session list.
            }

            var detailTable = new Table().Border(TableBorder.Rounded).Title("Sessions");
            detailTable.AddColumn("Name");
            detailTable.AddColumn("Timestamp");
            detailTable.AddColumn("Random");
            detailTable.AddColumn("CheckWitness");

            foreach (var s in sessions.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var ts = timestamps.TryGetValue(s, out var t) && t != null ? (t.ToString() ?? "-") : "-";
                var rand = randoms.TryGetValue(s, out var r) && r != null ? (r.ToString() ?? "-") : "-";
                var wit = witnesses.TryGetValue(s, out var w) ? ((w.ToString() ?? "-").ToLowerInvariant()) : "-";
                detailTable.AddRow(s, ts, rand, wit);
            }

            AnsiConsole.Write(detailTable);
        });

        return command;
    }

    private static Command CreateNewCommand(Option<string?> rpcOption)
    {
        var sessionsArgument = new Argument<string[]>(
            name: "sessions",
            description: "Session name(s) to create")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var command = new Command("new", "Create new sessions from current system state")
        {
            sessionsArgument
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, _) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));
            var sessions = ctx.ParseResult.GetValueForArgument(sessionsArgument);

            try
            {
                await client.NewSnapshotsFromCurrentSystemAsync(sessions);
                AnsiConsole.MarkupLine($"[green]✓[/] Created {sessions.Length} session(s).");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to create sessions:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateCloneCommand(Option<string?> rpcOption)
    {
        var fromArgument = new Argument<string>(
            name: "from",
            description: "Source session name");

        var toArgument = new Argument<string>(
            name: "to",
            description: "Destination session name");

        var command = new Command("clone", "Clone/copy a session to a new name")
        {
            fromArgument,
            toArgument
        };

        command.AddAlias("copy");
        command.AddAlias("cp");

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, _) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));
            var from = ctx.ParseResult.GetValueForArgument(fromArgument);
            var to = ctx.ParseResult.GetValueForArgument(toArgument);

            try
            {
                await client.CopySnapshotAsync(from, to);
                AnsiConsole.MarkupLine($"[green]✓[/] Cloned `{from}` → `{to}`.");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to clone session:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateRenameCommand(Option<string?> rpcOption)
    {
        var fromArgument = new Argument<string>(
            name: "from",
            description: "Existing session name");

        var toArgument = new Argument<string>(
            name: "to",
            description: "New session name");

        var command = new Command("rename", "Rename a session")
        {
            fromArgument,
            toArgument
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, _) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));
            var from = ctx.ParseResult.GetValueForArgument(fromArgument);
            var to = ctx.ParseResult.GetValueForArgument(toArgument);

            try
            {
                await client.RenameSnapshotAsync(from, to);
                AnsiConsole.MarkupLine($"[green]✓[/] Renamed `{from}` → `{to}`.");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to rename session:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateDeleteCommand(Option<string?> rpcOption)
    {
        var sessionsArgument = new Argument<string[]>(
            name: "sessions",
            description: "Session name(s) to delete")
        {
            Arity = ArgumentArity.OneOrMore
        };

        var command = new Command("delete", "Delete sessions")
        {
            sessionsArgument
        };

        command.AddAlias("rm");

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, _) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));
            var sessions = ctx.ParseResult.GetValueForArgument(sessionsArgument);

            try
            {
                await client.DeleteSnapshotsAsync(sessions);
                AnsiConsole.MarkupLine($"[green]✓[/] Deleted {sessions.Length} session(s).");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to delete sessions:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateInfoCommand(Option<string?> rpcOption)
    {
        var sessionsArgument = new Argument<string[]>(
            name: "sessions",
            description: "Session name(s) to query (defaults to all)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command("info", "Show session configuration (timestamp/random/witness)")
        {
            sessionsArgument
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            var (client, resolvedRpc) = ResolveClient(ctx.ParseResult.GetValueForOption(rpcOption));
            var sessions = ctx.ParseResult.GetValueForArgument(sessionsArgument);

            IReadOnlyList<string> sessionList;
            if (sessions.Length == 0)
            {
                try
                {
                    sessionList = await client.ListSnapshotsAsync();
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to list sessions:[/] {ex.Message.EscapeMarkup()}");
                    ctx.ExitCode = 1;
                    return;
                }
            }
            else
            {
                sessionList = sessions.ToList();
            }

            if (sessionList.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No sessions found on node.[/]");
                return;
            }

            IReadOnlyDictionary<string, ulong?> timestamps;
            IReadOnlyDictionary<string, ulong?> randoms;
            IReadOnlyDictionary<string, bool> witnesses;
            try
            {
                timestamps = await client.GetSnapshotTimestampAsync(sessionList.ToArray());
                randoms = await client.GetSnapshotRandomAsync(sessionList.ToArray());
                witnesses = await client.GetSnapshotCheckWitnessAsync(sessionList.ToArray());
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to query session info:[/] {ex.Message.EscapeMarkup()}");
                ctx.ExitCode = 1;
                return;
            }

            AnsiConsole.MarkupLine($"[grey]RPC:[/] {resolvedRpc}");

            var table = new Table().Border(TableBorder.Rounded).Title("Session Info");
            table.AddColumn("Session");
            table.AddColumn("Timestamp");
            table.AddColumn("Random");
            table.AddColumn("CheckWitness");

            foreach (var s in sessionList.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                var ts = timestamps.TryGetValue(s, out var t) && t != null ? (t.ToString() ?? "-") : "-";
                var rand = randoms.TryGetValue(s, out var r) && r != null ? (r.ToString() ?? "-") : "-";
                var wit = witnesses.TryGetValue(s, out var w) ? ((w.ToString() ?? "-").ToLowerInvariant()) : "-";
                table.AddRow(s, ts, rand, wit);
            }

            AnsiConsole.Write(table);
        });

        return command;
    }

    private static (FairyRpcClient Client, string RpcUrl) ResolveClient(string? rpcUrl)
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
        return (new FairyRpcClient(resolvedRpc), resolvedRpc);
    }
}
