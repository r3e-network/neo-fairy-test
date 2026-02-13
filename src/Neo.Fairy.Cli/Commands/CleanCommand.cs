// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Core.Models;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to clean build artifacts (out/, Generated/, etc).
/// Similar to 'forge clean' in Foundry.
/// </summary>
public static class CleanCommand
{
    public static Command Create()
    {
        var allOption = new Option<bool>(
            name: "--all",
            description: "Also remove bin/ and obj/ directories (use with care)");

        var yesOption = new Option<bool>(
            aliases: new[] { "--yes", "-y" },
            description: "Skip confirmation prompt");

        var command = new Command("clean", "Remove generated artifacts")
        {
            allOption,
            yesOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForOption(allOption),
                ctx.ParseResult.GetValueForOption(yesOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(bool all, bool yes)
    {
        FairyProject project;
        try
        {
            project = FairyProject.Load();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }

        var candidates = new List<string>
        {
            project.OutputDirectory,
            Path.Combine(project.RootDirectory, "Generated"),
            Path.Combine(project.RootDirectory, "cache")
        };

        if (all)
        {
            candidates.Add(Path.Combine(project.RootDirectory, "bin"));
            candidates.Add(Path.Combine(project.RootDirectory, "obj"));
        }

        var targets = candidates
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]Nothing to clean.[/]");
            return 0;
        }

        if (!yes)
        {
            var prompt = all
                ? "This will delete out/, Generated/, cache/, bin/, and obj/. Continue?"
                : "This will delete out/, Generated/, and cache/. Continue?";

            if (!AnsiConsole.Confirm(prompt))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return 0;
            }
        }

        foreach (var dir in targets)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                AnsiConsole.MarkupLine($"[green]✓[/] Removed {dir.EscapeMarkup()}");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Failed to remove {dir.EscapeMarkup()}:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        await Task.CompletedTask;
        return 0;
    }
}

