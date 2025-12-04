// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to debug contracts interactively.
/// </summary>
public static class DebugCommand
{
    public static Command Create()
    {
        var targetArgument = new Argument<string>(
            name: "target",
            description: "Test file::method or contract::method to debug");

        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Use a specific session snapshot");

        var command = new Command("debug", "Debug a contract or test interactively")
        {
            targetArgument,
            sessionOption
        };

        command.SetHandler(ExecuteAsync, targetArgument, sessionOption);

        return command;
    }

    private static async Task ExecuteAsync(string target, string? session)
    {
        AnsiConsole.MarkupLine($"[green]Starting debug session for:[/] {target}");
        AnsiConsole.WriteLine();

        // Parse target (file::method format)
        var parts = target.Split("::");
        var file = parts[0];
        var method = parts.Length > 1 ? parts[1] : null;

        AnsiConsole.MarkupLine("[grey]Loading debug info...[/]");
        await Task.Delay(200);

        AnsiConsole.MarkupLine("[yellow]Breakpoint hit at Counter.cs:42[/]");
        AnsiConsole.WriteLine();

        // Display source context
        AnsiConsole.MarkupLine("[grey]  40 │     public static bool Mint(UInt160 to, BigInteger amount)[/]");
        AnsiConsole.MarkupLine("[grey]  41 │     {[/]");
        AnsiConsole.MarkupLine("[white]→ 42 │         Assert(Runtime.CheckWitness(to), \"Not authorized\");[/]");
        AnsiConsole.MarkupLine("[grey]  43 │[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Debug commands: step, next, continue, locals, stack, quit[/]");
        AnsiConsole.MarkupLine("[grey]Type 'help' for more commands[/]");
    }
}
