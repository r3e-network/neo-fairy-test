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
/// Command to call contract methods (read-only).
/// Similar to 'cast call' in Foundry.
/// </summary>
public static class CallCommand
{
    public static Command Create()
    {
        var contractArgument = new Argument<string>(
            name: "contract",
            description: "Contract hash or alias");

        var methodArgument = new Argument<string>(
            name: "method",
            description: "Method name to call");

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
            description: "Workspace name for alias-based calls (defaults to project name)");

        var command = new Command("call", "Call a contract method (read-only)")
        {
            contractArgument,
            methodArgument,
            argsArgument,
            sessionOption,
            rpcOption,
            workspaceOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(contractArgument),
                ctx.ParseResult.GetValueForArgument(methodArgument),
                ctx.ParseResult.GetValueForArgument(argsArgument),
                ctx.ParseResult.GetValueForOption(sessionOption),
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(workspaceOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string contract,
        string method,
        string[] args,
        string? session,
        string? rpcUrl,
        string? workspace)
    {
        FairyProject? project = null;
        try
        {
            project = FairyProject.Load();
        }
        catch
        {
            // Calling outside a Fairy project is allowed if full args are provided.
        }

        var resolvedRpcUrl = RpcUrlResolver.Resolve(rpcUrl, project);
        var client = new FairyRpcClient(resolvedRpcUrl);

        var sessionId = session ?? $"cli_call_{Guid.NewGuid():N}";
        var workspaceName = workspace ?? project?.Config.Project.Name ?? "default";
        var parsedArgs = args.Select(CliArgumentParser.ParseArgument).ToArray();

        AnsiConsole.MarkupLine($"[grey]Calling {contract.EscapeMarkup()}.{method.EscapeMarkup()}({string.Join(", ", args).EscapeMarkup()})[/]");

        try
        {
            ExecutionResult result;

            if (!CliArgumentParser.LooksLikeHash(contract))
            {
                if (session == null)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Alias calls require --session (deploy via workspace first).");
                    return 1;
                }

                result = await client.InvokeWorkspaceFunctionWithSessionAsync(
                    workspaceName,
                    contract,
                    sessionId,
                    method,
                    parsedArgs,
                    writeSnapshot: false);
            }
            else
            {
                result = await client.InvokeFunctionWithSessionAsync(
                    sessionId,
                    contract,
                    method,
                    parsedArgs,
                    writeSnapshot: false);
            }

            PrintExecutionResult(result);

            return result.IsFault ? 1 : 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Call failed:[/] {ex.Message.EscapeMarkup()}");
            return 1;
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
    }

    private static void PrintExecutionResult(ExecutionResult result)
    {
        var stateColor = result.IsSuccess ? "green" : "red";
        AnsiConsole.MarkupLine($"[{stateColor}]State:[/] {result.State.ToString().EscapeMarkup()}");
        AnsiConsole.MarkupLine($"[grey]GAS consumed:[/] {result.GasConsumed / 100000000.0:F8}");

        if (result.Stack.Count > 0)
        {
            var first = result.Stack[0];
            AnsiConsole.MarkupLine($"[green]Result:[/] ({first.Type.EscapeMarkup()}) {first.Value?.ToString()?.EscapeMarkup()}");
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Result:[/] <empty stack>");
        }

        if (result.Notifications.Count > 0)
        {
            AnsiConsole.MarkupLine("[grey]Notifications:[/]");
            foreach (var n in result.Notifications)
            {
                AnsiConsole.MarkupLine($"  - {n.EventName.EscapeMarkup()} ({(n.ContractName ?? n.ContractHash).EscapeMarkup()})");
            }
        }

        if (result.IsFault)
        {
            AnsiConsole.MarkupLine($"[red]Exception:[/] {result.Exception?.EscapeMarkup()}");
            if (!string.IsNullOrWhiteSpace(result.Traceback))
            {
                AnsiConsole.MarkupLine($"[grey]{result.Traceback.EscapeMarkup()}[/]");
            }
        }
    }
}
