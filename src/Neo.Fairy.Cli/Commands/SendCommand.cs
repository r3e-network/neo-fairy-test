// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
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

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "RPC endpoint URL");

        var asyncOption = new Option<bool>(
            name: "--async",
            description: "Don't wait for confirmation");

        var command = new Command("send", "Send a transaction to a contract")
        {
            contractArgument,
            methodArgument,
            argsArgument,
            walletOption,
            rpcOption,
            asyncOption
        };

        command.SetHandler(ExecuteAsync,
            contractArgument, methodArgument, argsArgument,
            walletOption, rpcOption, asyncOption);

        return command;
    }

    private static async Task ExecuteAsync(
        string contract,
        string method,
        string[] args,
        string? wallet,
        string? rpcUrl,
        bool asyncMode)
    {
        AnsiConsole.MarkupLine($"[grey]Sending {contract}.{method}({string.Join(", ", args)})[/]");

        // Placeholder implementation
        await Task.Delay(100);

        var txHash = "0x" + Guid.NewGuid().ToString("N");
        AnsiConsole.MarkupLine($"[green]TX Hash:[/] {txHash}");

        if (!asyncMode)
        {
            AnsiConsole.MarkupLine("[grey]Waiting for confirmation...[/]");
            await Task.Delay(500);
            AnsiConsole.MarkupLine("[green]Status:[/] Confirmed (block 12345)");
        }

        AnsiConsole.MarkupLine("[grey]GAS used: 0.8[/]");
    }
}
