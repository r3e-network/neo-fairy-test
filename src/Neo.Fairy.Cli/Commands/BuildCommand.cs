// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.Diagnostics;
using Neo.Fairy.Cli.Services;
using Neo.Fairy.Core.Configuration;
using Neo.Fairy.Core.Models;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to build/compile smart contracts.
/// Similar to 'forge build' in Foundry.
/// </summary>
public static class BuildCommand
{
    public static Command Create()
    {
        var contractOption = new Option<string?>(
            aliases: new[] { "--contract", "-c" },
            description: "Build only a specific contract by alias");

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "Force rebuild even if up to date");

        var debugOption = new Option<bool>(
            name: "--debug",
            description: "Include debug information",
            getDefaultValue: () => true);

        var optimizeOption = new Option<bool>(
            name: "--optimize",
            description: "Enable optimization");

        var noGenOption = new Option<bool>(
            name: "--no-gen",
            description: "Skip contract interface generation");

        var command = new Command("build", "Compile smart contracts")
        {
            contractOption,
            forceOption,
            debugOption,
            optimizeOption,
            noGenOption
        };

        command.SetHandler(ExecuteAsync, contractOption, forceOption, debugOption, optimizeOption, noGenOption);

        return command;
    }

    private static async Task ExecuteAsync(
        string? contractAlias,
        bool force,
        bool debug,
        bool optimize,
        bool noGen)
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

        var contracts = project.Config.Contracts;
        if (!string.IsNullOrEmpty(contractAlias))
        {
            contracts = contracts
                .Where(c => string.Equals(c.Alias, contractAlias, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (contracts.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Contract '{contractAlias}' not found in fairy.toml");
                return;
            }
        }

        AnsiConsole.MarkupLine($"[green]Compiling {contracts.Count} contract(s)...[/]");

        var stopwatch = Stopwatch.StartNew();
        var results = new List<(string Name, bool Success, string? Error, long Size)>();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]Building contracts[/]", maxValue: contracts.Count);

                foreach (var contract in contracts)
                {
                    var sourcePath = Path.Combine(project.RootDirectory, contract.Path);

                    if (!File.Exists(sourcePath))
                    {
                        results.Add((contract.Name, false, $"Source file not found: {contract.Path}", 0));
                        task.Increment(1);
                        continue;
                    }

                    var (success, error, size) = await CompileContractAsync(
                        project,
                        contract,
                        debug,
                        optimize);

                    results.Add((contract.Name, success, error, size));
                    task.Increment(1);
                }
            });

        stopwatch.Stop();

        // Display results
        AnsiConsole.WriteLine();
        foreach (var (name, success, error, size) in results)
        {
            if (success)
            {
                var sizeKb = size / 1024.0;
                AnsiConsole.MarkupLine($"  [green][[✓]][/] {name} → out/{name}.nef ({sizeKb:F1}kb)");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red][[✗]][/] {name}: {error}");
            }
        }

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count - successCount;

        AnsiConsole.WriteLine();
        if (failCount == 0)
        {
            AnsiConsole.MarkupLine($"[green]✓ Compiled successfully in {stopwatch.Elapsed.TotalSeconds:F1}s[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Compiled {successCount}/{results.Count} contracts in {stopwatch.Elapsed.TotalSeconds:F1}s[/]");
        }

        // Generate contract interfaces if compilation succeeded and not skipped
        if (!noGen && successCount > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Generating contract interfaces...[/]");

            var buildService = new BuildService();
            var genResults = await buildService.GenerateContractInterfacesAsync(project);

            foreach (var genResult in genResults)
            {
                if (genResult.Success)
                {
                    AnsiConsole.MarkupLine($"  [green][[✓]][/] {genResult.ContractName} → Generated/{genResult.ClassName}.g.cs");
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [yellow][[!]][/] {genResult.ContractName}: {genResult.Error}");
                }
            }

            var genSuccessCount = genResults.Count(r => r.Success);
            if (genSuccessCount > 0)
            {
                AnsiConsole.MarkupLine($"[green]✓ Generated {genSuccessCount} contract interface(s)[/]");
            }
        }
    }

    private static async Task<(bool Success, string? Error, long Size)> CompileContractAsync(
        FairyProject project,
        ContractConfig contract,
        bool debug,
        bool optimize)
    {
        var compiler = project.Config.Compiler.Path;
        var sourcePath = Path.Combine(project.RootDirectory, contract.Path);
        var outputDir = project.OutputDirectory;

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        // Build compiler arguments
        var args = new List<string>
        {
            Path.GetDirectoryName(sourcePath) ?? sourcePath,
            "-o", outputDir
        };

        if (debug || project.Config.Compiler.Debug)
        {
            args.Add("--debug");
        }

        if (project.Config.Compiler.Assembly)
        {
            args.Add("--assembly");
        }

        if (optimize || project.Config.Compiler.Optimize)
        {
            args.Add("--optimize");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = compiler,
                Arguments = string.Join(" ", args),
                WorkingDirectory = project.RootDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, $"Failed to start compiler: {compiler}", 0);
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                return (false, error.Trim(), 0);
            }

            // Check output file
            var nefPath = Path.Combine(outputDir, $"{contract.Name}.nef");
            if (File.Exists(nefPath))
            {
                var size = new FileInfo(nefPath).Length;
                return (true, null, size);
            }

            return (false, "NEF file not generated", 0);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, 0);
        }
    }
}
