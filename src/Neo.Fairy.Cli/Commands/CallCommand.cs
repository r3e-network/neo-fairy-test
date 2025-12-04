// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
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

        var command = new Command("call", "Call a contract method (read-only)")
        {
            contractArgument,
            methodArgument,
            argsArgument,
            sessionOption,
            rpcOption
        };

        command.SetHandler(ExecuteAsync,
            contractArgument, methodArgument, argsArgument,
            sessionOption, rpcOption);

        return command;
    }

    private static async Task ExecuteAsync(
        string contract,
        string method,
        string[] args,
        string? session,
        string? rpcUrl)
    {
        AnsiConsole.MarkupLine($"[grey]Calling {contract}.{method}({string.Join(", ", args)})[/]");

        // Placeholder implementation
        // Actual implementation would call Fairy RPC
        await Task.Delay(100);

        AnsiConsole.MarkupLine("[green]Result:[/] 42 (BigInteger)");
        AnsiConsole.MarkupLine("[grey]GAS used: 0.05[/]");
    }
}
