// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Services;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to send transactions (state-changing calls).
/// Similar to 'cast send' in Foundry.
/// </summary>
public static class SendCommand
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

        var walletOption = new Option<string?>(
            aliases: new[] { "--wallet", "-w" },
            description: "Wallet file for signing");

        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Execute in a virtual Fairy session snapshot");

        var broadcastOption = new Option<bool>(
            name: "--broadcast",
            description: "Relay invocation to chain (requires session wallet)");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "Wallet password (or FAIRY_WALLET_PASSWORD env)");

        var waitOption = new Option<bool>(
            name: "--wait",
            description: "Wait for a broadcast transaction to be confirmed");

        var waitBlocksOption = new Option<uint>(
            name: "--wait-blocks",
            description: "Number of blocks to wait for confirmation",
            getDefaultValue: () => 2);

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "RPC endpoint URL");

        var workspaceOption = new Option<string?>(
            aliases: new[] { "--workspace", "-wsp" },
            description: "Workspace name for alias-based sends (defaults to project name)");

        var asyncOption = new Option<bool>(
            name: "--async",
            description: "Don't wait for confirmation");

        var command = new Command("send", "Send a transaction to a contract")
        {
            contractArgument,
            methodArgument,
            argsArgument,
            walletOption,
            sessionOption,
            broadcastOption,
            passwordOption,
            waitOption,
            waitBlocksOption,
            rpcOption,
            workspaceOption,
            asyncOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(contractArgument),
                ctx.ParseResult.GetValueForArgument(methodArgument),
                ctx.ParseResult.GetValueForArgument(argsArgument),
                ctx.ParseResult.GetValueForOption(walletOption),
                ctx.ParseResult.GetValueForOption(sessionOption),
                ctx.ParseResult.GetValueForOption(broadcastOption),
                ctx.ParseResult.GetValueForOption(passwordOption),
                ctx.ParseResult.GetValueForOption(waitOption),
                ctx.ParseResult.GetValueForOption(waitBlocksOption),
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(workspaceOption),
                ctx.ParseResult.GetValueForOption(asyncOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string contract,
        string method,
        string[] args,
        string? wallet,
        string? session,
        bool broadcast,
        string? password,
        bool wait,
        uint waitBlocks,
        string? rpcUrl,
        string? workspace,
        bool asyncMode)
    {
        FairyProject? project = null;
        try
        {
            project = FairyProject.Load();
        }
        catch
        {
        }

        if (broadcast && string.IsNullOrEmpty(session))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --broadcast requires --session.");
            return 1;
        }

        if (!broadcast && string.IsNullOrEmpty(session))
        {
            // For virtual sends we auto-create a throwaway session.
            session = null;
        }

        var resolvedRpcUrl = RpcUrlResolver.Resolve(rpcUrl, project);
        var client = new FairyRpcClient(resolvedRpcUrl);

        var sessionId = session ?? $"cli_send_{Guid.NewGuid():N}";
        var workspaceName = workspace ?? project?.Config.Project.Name ?? "default";
        var parsedArgs = args.Select(CliArgumentParser.ParseArgument).ToArray();

        AnsiConsole.MarkupLine($"[grey]Sending {contract}.{method}({string.Join(", ", args)})[/]");

        if (broadcast)
        {
            try
            {
                var walletSpec = WalletLoader.Load(wallet, password, project);
                if (walletSpec.Nep2Keys != null)
                {
                    await client.SetSessionWalletWithNep2Async(sessionId, walletSpec.Nep2Keys, walletSpec.Password);
                }
                else if (walletSpec.Wifs != null)
                {
                    await client.SetSessionWalletWithWifAsync(sessionId, walletSpec.Wifs.ToArray());
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to configure session wallet:[/] {ex.Message.EscapeMarkup()}");
                return 1;
            }
        }

        var exitCode = 0;
        try
        {
            ExecutionResult result;

            if (!CliArgumentParser.LooksLikeHash(contract))
            {
                if (session == null)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Alias sends require --session (deploy via workspace first).");
                    exitCode = 1;
                    return exitCode;
                }

                result = broadcast
                    ? await client.RelayInvokeWorkspaceFunctionAsync(
                        workspaceName,
                        contract,
                        sessionId,
                        method,
                        parsedArgs)
                    : await client.InvokeWorkspaceFunctionWithSessionAsync(
                        workspaceName,
                        contract,
                        sessionId,
                        method,
                        parsedArgs,
                        writeSnapshot: true);
            }
            else
            {
                result = broadcast
                    ? await client.RelayInvokeFunctionAsync(
                        sessionId,
                        contract,
                        method,
                        parsedArgs)
                    : await client.InvokeFunctionWithSessionAsync(
                        sessionId,
                        contract,
                        method,
                        parsedArgs,
                        writeSnapshot: true);
            }

            PrintExecutionResult(result, asyncMode);
            exitCode = result.IsFault ? 1 : 0;

            var pendingSig = string.Equals(result.Note, "Pending signature", StringComparison.OrdinalIgnoreCase);

            if (broadcast && wait && result.TransactionHash != null && pendingSig)
            {
                AnsiConsole.MarkupLine("[grey]Skipping confirmation wait until signatures are complete.[/]");
            }

            if (broadcast && wait && result.TransactionHash != null && !pendingSig)
            {
                AnsiConsole.MarkupLine("[grey]Waiting for confirmation...[/]");
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
                        AnsiConsole.MarkupLine($"[green]Confirmed[/] ({confirmations} confirmations, block {blockHash})");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[green]Confirmed[/]");
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Confirmation wait failed:[/] {ex.Message.EscapeMarkup()}");
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Send failed:[/] {ex.Message.EscapeMarkup()}");
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
                }
            }
        }

        return exitCode;
    }

    private static void PrintExecutionResult(ExecutionResult result, bool asyncMode)
    {
        var stateColor = result.IsSuccess ? "green" : "red";
        AnsiConsole.MarkupLine($"[{stateColor}]State:[/] {result.State}");
        AnsiConsole.MarkupLine($"[grey]GAS consumed:[/] {result.GasConsumed / 100000000.0:F8}");

        if (result.TransactionHash != null)
        {
            AnsiConsole.MarkupLine($"[green]TX Hash:[/] {result.TransactionHash}");
        }
        else if (!asyncMode)
        {
            AnsiConsole.MarkupLine("[grey]Virtual execution (no relay).[/]");
        }

        if (!string.IsNullOrWhiteSpace(result.Note))
        {
            AnsiConsole.MarkupLine($"[yellow]{result.Note}[/]");
        }

        if (result.Stack.Count > 0)
        {
            var first = result.Stack[0];
            AnsiConsole.MarkupLine($"[green]Result:[/] ({first.Type}) {first.Value}");
        }

        if (result.IsFault)
        {
            AnsiConsole.MarkupLine($"[red]Exception:[/] {result.Exception}");
        }
    }
}
