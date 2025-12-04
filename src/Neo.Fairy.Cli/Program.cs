// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using Neo.Fairy.Cli.Commands;
using Spectre.Console;

namespace Neo.Fairy.Cli;

/// <summary>
/// Entry point for the Fairy CLI tool.
/// Provides Foundry-style commands for Neo N3 smart contract development.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Neo Fairy Framework - Professional Neo N3 smart contract development toolkit")
        {
            Name = "fairy"
        };

        // Add all commands
        rootCommand.AddCommand(InitCommand.Create());
        rootCommand.AddCommand(BuildCommand.Create());
        rootCommand.AddCommand(TestCommand.Create());
        rootCommand.AddCommand(DeployCommand.Create());
        rootCommand.AddCommand(CallCommand.Create());
        rootCommand.AddCommand(SendCommand.Create());
        rootCommand.AddCommand(DebugCommand.Create());
        rootCommand.AddCommand(ScriptCommand.Create());
        rootCommand.AddCommand(NodeCommand.Create());

        // Add global options
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");
        rootCommand.AddGlobalOption(verboseOption);

        var quietOption = new Option<bool>(
            aliases: new[] { "--quiet", "-q" },
            description: "Suppress non-essential output");
        rootCommand.AddGlobalOption(quietOption);

        // Version display
        rootCommand.SetHandler(() =>
        {
            PrintBanner();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Run [white]fairy --help[/] for usage information.[/]");
        });

        try
        {
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            if (args.Contains("--verbose") || args.Contains("-v"))
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
        }
    }

    internal static void PrintBanner()
    {
        AnsiConsole.Write(new FigletText("Fairy")
            .Color(Color.Green));
        AnsiConsole.MarkupLine("[green]Neo Fairy Framework[/] [grey]v1.0.0[/]");
        AnsiConsole.MarkupLine("[grey]Professional Neo N3 smart contract development toolkit[/]");
    }
}
