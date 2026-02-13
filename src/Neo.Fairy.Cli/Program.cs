// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.Reflection;
using Neo.Fairy.Cli.Commands;
using Neo.Fairy.Cli.Utilities;
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
        rootCommand.AddCommand(CleanCommand.Create());
        rootCommand.AddCommand(BuildCommand.Create());
        rootCommand.AddCommand(TestCommand.Create());
        rootCommand.AddCommand(CoverageCommand.Create());
        rootCommand.AddCommand(DoctorCommand.Create());
        rootCommand.AddCommand(DeployCommand.Create());
        rootCommand.AddCommand(CallCommand.Create());
        rootCommand.AddCommand(SendCommand.Create());
        rootCommand.AddCommand(InspectCommand.Create());
        rootCommand.AddCommand(DebugCommand.Create());
        rootCommand.AddCommand(SessionCommand.Create());
        rootCommand.AddCommand(WorkspaceCommand.Create());
        rootCommand.AddCommand(ScriptCommand.Create());
        rootCommand.AddCommand(NodeCommand.Create());
        rootCommand.AddCommand(PluginCommand.Create());

        // Add global options
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output");
        rootCommand.AddGlobalOption(verboseOption);

        var quietOption = new Option<bool>(
            aliases: new[] { "--quiet", "-q" },
            description: "Suppress non-essential output");
        rootCommand.AddGlobalOption(quietOption);

        rootCommand.AddGlobalOption(GlobalOptions.Json);

        var versionOption = new Option<bool>(
            name: "--version",
            description: "Show version information");
        rootCommand.AddGlobalOption(versionOption);

        // Version display
        rootCommand.SetHandler((bool version) =>
        {
            if (version)
            {
                AnsiConsole.MarkupLine($"fairy {GetVersionString().EscapeMarkup()}");
                return;
            }

            PrintBanner();
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Run [white]fairy --help[/] for usage information.[/]");
        }, versionOption);

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
        var version = GetVersionString();

        AnsiConsole.Write(new FigletText("Fairy")
            .Color(Color.Green));
        AnsiConsole.MarkupLine($"[green]Neo Fairy Framework[/] [grey]v{version.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine("[grey]Professional Neo N3 smart contract development toolkit[/]");
    }

    private static string GetVersionString()
    {
        return typeof(Program).Assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? typeof(Program).Assembly.GetName().Version?.ToString()
               ?? "dev";
    }
}
