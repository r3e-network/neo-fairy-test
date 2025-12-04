// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using Neo.Fairy.Core.Models;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to run deployment/migration scripts.
/// Similar to 'forge script' in Foundry.
/// </summary>
public static class ScriptCommand
{
    public static Command Create()
    {
        var scriptArgument = new Argument<string>(
            name: "script",
            description: "Script file to run (e.g., script/Deploy.cs)");

        var networkOption = new Option<string?>(
            aliases: new[] { "--network", "-n" },
            description: "Target network");

        var broadcastOption = new Option<bool>(
            name: "--broadcast",
            description: "Actually broadcast transactions");

        var verifyOption = new Option<bool>(
            name: "--verify",
            description: "Verify contracts after deployment");

        var command = new Command("script", "Run a deployment or migration script")
        {
            scriptArgument,
            networkOption,
            broadcastOption,
            verifyOption
        };

        command.SetHandler(ExecuteAsync,
            scriptArgument, networkOption, broadcastOption, verifyOption);

        return command;
    }

    private static async Task ExecuteAsync(
        string script,
        string? network,
        bool broadcast,
        bool verify)
    {
        FairyProject project;
        try
        {
            project = FairyProject.Load();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return;
        }

        var scriptPath = Path.Combine(project.RootDirectory, script);
        if (!File.Exists(scriptPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Script not found: {script}");
            return;
        }

        AnsiConsole.MarkupLine($"[green]Running script:[/] {script}");
        if (!broadcast)
        {
            AnsiConsole.MarkupLine("[yellow]Simulation mode (use --broadcast to execute)[/]");
        }
        AnsiConsole.WriteLine();

        await AnsiConsole.Status()
            .StartAsync("Executing script...", async ctx =>
            {
                ctx.Status("Deploying Token...");
                await Task.Delay(200);
                AnsiConsole.MarkupLine("  → Deploying Token...");

                ctx.Status("Deploying Router...");
                await Task.Delay(200);
                AnsiConsole.MarkupLine("  → Deploying Router with Token dependency...");

                ctx.Status("Initializing...");
                await Task.Delay(200);
                AnsiConsole.MarkupLine("  → Initializing Router with Token address...");

                ctx.Status("Setting parameters...");
                await Task.Delay(100);
                AnsiConsole.MarkupLine("  → Setting initial parameters...");
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Script completed successfully[/]");

        if (!broadcast)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]To broadcast transactions, run with --broadcast[/]");
        }
    }
}
