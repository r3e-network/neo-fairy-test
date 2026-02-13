// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Debugging;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to debug contracts interactively through Fairy debugger RPC.
/// </summary>
public static class DebugCommand
{
    public static Command Create()
    {
        var targetArgument = new Argument<string>(
            name: "target",
            description: "Contract::method to debug (hash or alias)");

        var argsArgument = new Argument<string[]>(
            name: "args",
            description: "Method arguments")
        {
            Arity = ArgumentArity.ZeroOrMore
        };

        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Use a specific session snapshot");

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "RPC endpoint URL");

        var workspaceOption = new Option<string?>(
            aliases: new[] { "--workspace", "-wsp" },
            description: "Workspace name for alias targets (defaults to project name)");

        var dumpNefOption = new Option<string?>(
            name: "--dumpnef",
            description: "Path to dumpnef text file for source-level stepping (optional)");

        var command = new Command("debug", "Debug a contract interactively")
        {
            targetArgument,
            argsArgument,
            sessionOption,
            rpcOption,
            workspaceOption,
            dumpNefOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(targetArgument),
                ctx.ParseResult.GetValueForArgument(argsArgument),
                ctx.ParseResult.GetValueForOption(sessionOption),
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(workspaceOption),
                ctx.ParseResult.GetValueForOption(dumpNefOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string target,
        string[] args,
        string? session,
        string? rpcUrl,
        string? workspace,
        string? dumpNefPath)
    {
        FairyProject? project = null;
        try
        {
            project = FairyProject.Load();
        }
        catch
        {
            // Debugging outside a Fairy project is allowed if full args are provided.
        }

        var resolvedRpcUrl = RpcUrlResolver.Resolve(rpcUrl, project);
        var client = new FairyRpcClient(resolvedRpcUrl);

        var sessionId = session ?? $"cli_debug_{Guid.NewGuid():N}";
        var workspaceName = workspace ?? project?.Config.Project.Name ?? "default";

        var (contractPart, method) = ParseTarget(target);
        if (string.IsNullOrWhiteSpace(contractPart) || string.IsNullOrWhiteSpace(method))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Target must be in contract::method form.");
            return 1;
        }

        if (!CliArgumentParser.LooksLikeHash(contractPart) && session == null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Alias debugging requires --session (deploy via workspace first).");
            return 1;
        }

        string scriptHash = contractPart;
        if (!CliArgumentParser.LooksLikeHash(contractPart))
        {
            try
            {
                var hashes = await client.GetWorkspaceContractHashesAsync(workspaceName);
                if (!hashes.TryGetValue(contractPart, out var resolvedHash) || string.IsNullOrWhiteSpace(resolvedHash))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] No deployment recorded for alias `{contractPart}` in workspace `{workspaceName}`.");
                    return 1;
                }

                scriptHash = resolvedHash!;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to resolve alias:[/] {ex.Message.EscapeMarkup()}");
                return 1;
            }
        }

        await TryRegisterDebugInfoAsync(project, client, contractPart, scriptHash, dumpNefPath);

        var parsedArgs = args.Select(CliArgumentParser.ParseArgument).ToArray();

        AnsiConsole.MarkupLine($"[green]Debugging {contractPart}::{method}[/]");
        AnsiConsole.MarkupLine($"[grey]RPC:[/] {resolvedRpcUrl}  [grey]Session:[/] {sessionId}");
        AnsiConsole.WriteLine();

        Dictionary<string, object?> current;
        try
        {
            current = await client.DebugFunctionWithSessionAsync(
                sessionId,
                writeSnapshot: true,
                scriptHash,
                method,
                parsedArgs);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to start debug run:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }

        var hasSourceInfo = HasSourceInfo(current);
        PrintLocation(current);
        PrintHelp(hasSourceInfo);

        var exitCode = 0;
        try
        {
            while (true)
            {
                if (IsTerminalState(current))
                {
                    exitCode = string.Equals(
                        current.GetValueOrDefault("state")?.ToString(),
                        "FAULT",
                        StringComparison.OrdinalIgnoreCase)
                        ? 1
                        : 0;
                    PrintTerminalState(current);
                    break;
                }

                var input = AnsiConsole.Ask<string>("[grey]debug>[/]").Trim();
                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var tokens = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var cmd = tokens[0].ToLowerInvariant();
                var rest = tokens.Length > 1 ? tokens[1].Trim() : string.Empty;

                switch (cmd)
                {
                    case "h":
                    case "help":
                        PrintHelp(hasSourceInfo);
                        continue;

                    case "q":
                    case "quit":
                    case "exit":
                        AnsiConsole.MarkupLine("[grey]Exiting debug session.[/]");
                        return exitCode;

                    case "s":
                    case "step":
                        current = await client.DebugStepIntoAsync(sessionId);
                        break;

                    case "n":
                    case "next":
                        current = hasSourceInfo
                            ? await client.DebugStepOverSourceAsync(sessionId)
                            : await client.DebugStepOverAssemblyAsync(sessionId);
                        break;

                    case "ni":
                    case "nexti":
                        current = await client.DebugStepOverAssemblyAsync(sessionId);
                        break;

                    case "c":
                    case "continue":
                        current = await client.DebugContinueAsync(sessionId);
                        break;

                    case "o":
                    case "out":
                        current = await client.DebugStepOutAsync(sessionId);
                        break;

                    case "locals":
                    case "l":
                        await PrintLocalsAsync(client, sessionId);
                        continue;

                    case "stack":
                        await PrintEvaluationStackAsync(client, sessionId);
                        continue;

                    case "frames":
                    case "bt":
                        await PrintFramesAsync(client, sessionId);
                        continue;

                    case "break":
                    case "b":
                        if (string.IsNullOrWhiteSpace(rest))
                        {
                            AnsiConsole.MarkupLine("[yellow]Usage:[/] break <file>:<line>");
                            continue;
                        }

                        await SetSourceBreakpointAsync(client, scriptHash, rest);
                        continue;

                    case "breaki":
                    case "bi":
                        if (!uint.TryParse(rest, out var ip))
                        {
                            AnsiConsole.MarkupLine("[yellow]Usage:[/] breaki <instructionPointer>");
                            continue;
                        }

                        await client.SetAssemblyBreakpointsAsync(scriptHash, ip);
                        AnsiConsole.MarkupLine($"[green]Set assembly breakpoint at {ip}.[/]");
                        continue;

                    default:
                        AnsiConsole.MarkupLine("[yellow]Unknown command. Type 'help' for commands.[/]");
                        continue;
                }

                hasSourceInfo = HasSourceInfo(current);
                PrintLocation(current);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Debug session failed:[/] {ex.Message.EscapeMarkup()}");
            exitCode = 1;
        }
        finally
        {
            if (session == null)
            {
                try
                {
                    await client.DeleteSnapshotsAsync(sessionId);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }

        return exitCode;
    }

    private static (string Contract, string Method) ParseTarget(string target)
    {
        var parts = target.Split("::", 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return (target, string.Empty);
        return (parts[0], parts[1]);
    }

    private static bool HasSourceInfo(Dictionary<string, object?> debugResult)
    {
        var filename = debugResult.GetValueOrDefault("sourcefilename")?.ToString();
        return !string.IsNullOrWhiteSpace(filename);
    }

    private static bool IsTerminalState(Dictionary<string, object?> debugResult)
    {
        var state = debugResult.GetValueOrDefault("state")?.ToString() ?? string.Empty;
        return state.Equals("HALT", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("FAULT", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintLocation(Dictionary<string, object?> debugResult)
    {
        var state = debugResult.GetValueOrDefault("state")?.ToString() ?? "UNKNOWN";
        var ip = debugResult.GetValueOrDefault("instructionpointer")?.ToString() ?? "?";

        var filename = debugResult.GetValueOrDefault("sourcefilename")?.ToString();
        var lineStr = debugResult.GetValueOrDefault("sourcelinenum")?.ToString();
        var content = debugResult.GetValueOrDefault("sourcecontent")?.ToString();

        if (!string.IsNullOrWhiteSpace(filename) && uint.TryParse(lineStr, out var line))
        {
            AnsiConsole.MarkupLine($"[yellow]{state}[/] at [white]{filename}:{line}[/]");
            if (!string.IsNullOrWhiteSpace(content))
                AnsiConsole.MarkupLine($"[grey]→ {content.EscapeMarkup()}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]{state}[/] at IP [white]{ip}[/]");
        }

        if (debugResult.GetValueOrDefault("exception") is string ex && !string.IsNullOrWhiteSpace(ex))
        {
            AnsiConsole.MarkupLine($"[red]Exception:[/] {ex.EscapeMarkup()}");
        }
    }

    private static void PrintTerminalState(Dictionary<string, object?> debugResult)
    {
        var state = debugResult.GetValueOrDefault("state")?.ToString() ?? "UNKNOWN";
        var gasStr = debugResult.GetValueOrDefault("gasconsumed")?.ToString() ?? "0";
        var gas = long.TryParse(gasStr, out var g) ? g : 0;

        AnsiConsole.WriteLine();
        if (state.Equals("HALT", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[green]Execution halted.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Execution faulted.[/]");
            var traceback = debugResult.GetValueOrDefault("traceback")?.ToString();
            if (!string.IsNullOrWhiteSpace(traceback))
            {
                AnsiConsole.MarkupLine($"[grey]{traceback.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.MarkupLine($"[grey]GAS consumed:[/] {gas / 100000000.0:F8}");

        if (debugResult.GetValueOrDefault("stack") is List<object?> stack && stack.Count > 0)
        {
            AnsiConsole.MarkupLine("[grey]Result stack:[/]");
            foreach (var item in stack)
            {
                AnsiConsole.MarkupLine($"  - {FormatStackItem(item)}");
            }
        }
    }

    private static async Task PrintLocalsAsync(FairyRpcClient client, string sessionId)
    {
        try
        {
            var vars = await client.GetVariableNamesAndValuesAsync(sessionId);
            if (vars.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]<no variables>[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded).Title("Variables");
            table.AddColumn("Name");
            table.AddColumn("Value");

            foreach (var kvp in vars.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                table.AddRow(kvp.Key, FormatStackItem(kvp.Value));
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Locals unavailable:[/] {ex.Message.EscapeMarkup()}");
        }
    }

    private static async Task PrintEvaluationStackAsync(FairyRpcClient client, string sessionId)
    {
        try
        {
            var stack = await client.GetEvaluationStackAsync(sessionId);
            if (stack.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]<empty evaluation stack>[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded).Title("Evaluation Stack");
            table.AddColumn("#");
            table.AddColumn("Value");

            for (int i = 0; i < stack.Count; i++)
            {
                table.AddRow(i.ToString(), FormatStackItem(stack[i]));
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Stack unavailable:[/] {ex.Message.EscapeMarkup()}");
        }
    }

    private static async Task PrintFramesAsync(FairyRpcClient client, string sessionId)
    {
        try
        {
            var frames = await client.GetInvocationStackAsync(sessionId);
            if (frames.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]<no frames>[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded).Title("Invocation Stack");
            table.AddColumn("#");
            table.AddColumn("ScriptHash");
            table.AddColumn("Location");

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i] as Dictionary<string, object?>;
                var hash = frame?.GetValueOrDefault("scripthash")?.ToString() ?? "?";
                var filename = frame?.GetValueOrDefault("sourcefilename")?.ToString();
                var lineStr = frame?.GetValueOrDefault("sourcelinenum")?.ToString();
                var ip = frame?.GetValueOrDefault("instructionpointer")?.ToString();

                var location = (!string.IsNullOrWhiteSpace(filename) && uint.TryParse(lineStr, out var line))
                    ? $"{filename}:{line}"
                    : $"IP {ip}";

                table.AddRow(i.ToString(), hash, location.EscapeMarkup());
            }

            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Frames unavailable:[/] {ex.Message.EscapeMarkup()}");
        }
    }

    private static async Task SetSourceBreakpointAsync(FairyRpcClient client, string scriptHash, string spec)
    {
        var idx = spec.LastIndexOf(':');
        if (idx <= 0 || idx == spec.Length - 1)
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] break <file>:<line>");
            return;
        }

        var file = spec[..idx];
        var lineStr = spec[(idx + 1)..];
        if (!uint.TryParse(lineStr, out var line))
        {
            AnsiConsole.MarkupLine("[yellow]Usage:[/] break <file>:<line>");
            return;
        }

        await client.SetSourceCodeBreakpointsAsync(scriptHash, (file, line));
        AnsiConsole.MarkupLine($"[green]Set source breakpoint at {file}:{line}.[/]");
    }

    private static void PrintHelp(bool hasSourceInfo)
    {
        AnsiConsole.MarkupLine("[grey]Commands:[/]");
        AnsiConsole.MarkupLine("  [white]step|s[/]     Step into");
        AnsiConsole.MarkupLine(hasSourceInfo
            ? "  [white]next|n[/]     Step over source line"
            : "  [white]next|n[/]     Step over instruction");
        AnsiConsole.MarkupLine("  [white]nexti|ni[/]   Step over instruction (assembly)");
        AnsiConsole.MarkupLine("  [white]out|o[/]      Step out");
        AnsiConsole.MarkupLine("  [white]continue|c[/] Continue");
        AnsiConsole.MarkupLine("  [white]locals|l[/]   Show variables (requires debug info)");
        AnsiConsole.MarkupLine("  [white]stack[/]      Show evaluation stack");
        AnsiConsole.MarkupLine("  [white]frames|bt[/]  Show invocation stack");
        AnsiConsole.MarkupLine("  [white]break|b[/]    Set source breakpoint file:line");
        AnsiConsole.MarkupLine("  [white]breaki|bi[/]  Set assembly breakpoint ip");
        AnsiConsole.MarkupLine("  [white]quit|q[/]     Exit debugger");
    }

    private static string FormatStackItem(object? item)
    {
        if (item is Dictionary<string, object?> dict)
        {
            var type = dict.GetValueOrDefault("type")?.ToString() ?? "Unknown";
            var value = dict.GetValueOrDefault("value");
            return $"({type}) {value}";
        }

        return item?.ToString() ?? "<null>";
    }

    private static async Task TryRegisterDebugInfoAsync(
        FairyProject? project,
        FairyRpcClient client,
        string contractPart,
        string contractHash,
        string? dumpNefPath)
    {
        if (project == null)
            return;

        ContractPathInfo? info = null;
        if (!CliArgumentParser.LooksLikeHash(contractPart))
        {
            info = project.GetContractByAlias(contractPart);
        }

        if (info == null)
            return;

        if (!File.Exists(info.DebugInfoPath))
            return;

        string dumpNefText = string.Empty;

        if (!string.IsNullOrWhiteSpace(dumpNefPath) && File.Exists(dumpNefPath))
        {
            dumpNefText = await File.ReadAllTextAsync(dumpNefPath);
        }
        else
        {
            var candidates = new[]
            {
                Path.ChangeExtension(info.NefPath, ".nef.txt"),
                Path.Combine(Path.GetDirectoryName(info.NefPath)!, $"{info.Name}.nef.txt"),
                Path.Combine(Path.GetDirectoryName(info.NefPath)!, $"{info.Name}.nef.asm"),
                Path.Combine(Path.GetDirectoryName(info.NefPath)!, $"{info.Name}.asm"),
            };

            var found = candidates.FirstOrDefault(File.Exists);
            if (found != null)
            {
                dumpNefText = await File.ReadAllTextAsync(found);
            }
        }

        if (string.IsNullOrWhiteSpace(dumpNefText))
        {
            try
            {
                var dbgBytes = await File.ReadAllBytesAsync(info.DebugInfoPath);
                var nefBytes = await File.ReadAllBytesAsync(info.NefPath);

                if (NefDumpGenerator.TryGenerateDumpNef(
                        nefBytes,
                        dbgBytes,
                        project.RootDirectory,
                        out var generatedDump,
                        out var genError))
                {
                    dumpNefText = generatedDump;

                    // Best-effort cache for future runs.
                    try
                    {
                        var cachePath = Path.ChangeExtension(info.NefPath, ".nef.txt");
                        await File.WriteAllTextAsync(cachePath, dumpNefText);
                    }
                    catch
                    {
                    }

                    AnsiConsole.MarkupLine("[grey]Generated dumpnef text from debug info.[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Debug info (.nefdbgnfo) found but dump text could not be generated.[/]");
                    if (!string.IsNullOrWhiteSpace(genError))
                    {
                        AnsiConsole.MarkupLine($"[grey]{genError.EscapeMarkup()}[/]");
                    }
                    AnsiConsole.MarkupLine("[grey]Provide --dumpnef <path> to enable source-level stepping.[/]");
                    return;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[yellow]Debug info (.nefdbgnfo) found but no dumpnef text available.[/]");
                AnsiConsole.MarkupLine($"[grey]{ex.Message.EscapeMarkup()}[/]");
                AnsiConsole.MarkupLine("[grey]Provide --dumpnef <path> to enable source-level stepping.[/]");
                return;
            }
        }

        try
        {
            var dbgBytes = await File.ReadAllBytesAsync(info.DebugInfoPath);
            await client.SetDebugInfoAsync(contractHash, dbgBytes, dumpNefText);
            AnsiConsole.MarkupLine("[grey]Registered debug info for source-level stepping.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Failed to register debug info:[/] {ex.Message.EscapeMarkup()}");
        }
    }
}
