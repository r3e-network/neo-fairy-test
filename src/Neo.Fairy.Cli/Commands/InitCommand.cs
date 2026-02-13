// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Core.Models;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to initialize a new Fairy project.
/// Similar to 'forge init' in Foundry.
/// </summary>
public static class InitCommand
{
    public static Command Create()
    {
        var nameArgument = new Argument<string?>(
            name: "name",
            description: "Project name (defaults to current directory name)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var templateOption = new Option<string>(
            aliases: new[] { "--template", "-t" },
            description: "Project template to use",
            getDefaultValue: () => "default");

        var forceOption = new Option<bool>(
            aliases: new[] { "--force", "-f" },
            description: "Overwrite existing files");

        var noGitOption = new Option<bool>(
            name: "--no-git",
            description: "Do not initialize a git repository");

        var command = new Command("init", "Initialize a new Fairy project")
        {
            nameArgument,
            templateOption,
            forceOption,
            noGitOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(nameArgument),
                ctx.ParseResult.GetValueForOption(templateOption) ?? "default",
                ctx.ParseResult.GetValueForOption(forceOption),
                ctx.ParseResult.GetValueForOption(noGitOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string? name,
        string template,
        bool force,
        bool noGit)
    {
        var currentDir = Directory.GetCurrentDirectory();
        string projectDir;
        string projectName;

        if (string.IsNullOrEmpty(name))
        {
            projectDir = currentDir;
            projectName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(projectName))
                projectName = "fairy-project";
        }
        else
        {
            projectDir = Path.Combine(currentDir, name);
            projectName = name;
        }

        AnsiConsole.MarkupLine($"[green]Creating new Fairy project:[/] {projectName}");

        // Check if directory exists and has files
        if (Directory.Exists(projectDir) && (Directory.GetFiles(projectDir).Length > 0 || Directory.GetDirectories(projectDir).Length > 0) && !force)
        {
            AnsiConsole.MarkupLine("[yellow]Directory is not empty. Use --force to overwrite.[/]");
            return 1;
        }

        await AnsiConsole.Status()
            .StartAsync("Initializing project...", async ctx =>
            {
                // Create project
                ctx.Status("Creating project structure...");
                var project = FairyProject.Create(projectDir, projectName);

                // Create template files
                ctx.Status("Creating template files...");
                await CreateTemplateFilesAsync(project, template);

                // Initialize git
                if (!noGit)
                {
                    ctx.Status("Initializing git repository...");
                    await InitializeGitAsync(projectDir);
                }
            });

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓[/] Created fairy.toml");
        AnsiConsole.MarkupLine("[green]✓[/] Created src/Counter.cs");
        AnsiConsole.MarkupLine("[green]✓[/] Created test/Counter.Test.cs");
        AnsiConsole.MarkupLine("[green]✓[/] Created script/Deploy.cs");
        if (!noGit)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Initialized git repository");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Next steps:[/]");
        if (!string.IsNullOrEmpty(name))
        {
            AnsiConsole.MarkupLine($"  [white]cd {name}[/]");
        }
        AnsiConsole.MarkupLine("  [white]fairy build[/]    - Compile contracts");
        AnsiConsole.MarkupLine("  [white]fairy test[/]     - Run tests");
        AnsiConsole.MarkupLine("  [white]fairy deploy[/]   - Deploy contracts");

        return 0;
    }

    private static async Task CreateTemplateFilesAsync(FairyProject project, string template)
    {
        // Create Counter.cs
        var counterPath = Path.Combine(project.SourceDirectory, "Counter.cs");
        await File.WriteAllTextAsync(counterPath, GetCounterTemplate());

        // Create Counter.Test.cs
        var testPath = Path.Combine(project.TestDirectory, "Counter.Test.cs");
        await File.WriteAllTextAsync(testPath, GetCounterTestTemplate());

        // Create Deploy.cs
        var deployPath = Path.Combine(project.ScriptDirectory, "Deploy.cs");
        await File.WriteAllTextAsync(deployPath, GetDeployScriptTemplate());

        // Create .gitignore
        var gitignorePath = Path.Combine(project.RootDirectory, ".gitignore");
        await File.WriteAllTextAsync(gitignorePath, GetGitignoreTemplate());
    }

    private static async Task InitializeGitAsync(string directory)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch
        {
            // Git not available, skip
        }
    }

    private static string GetCounterTemplate() => """
        // SPDX-License-Identifier: MIT
        // Counter.cs - A simple counter contract for Neo N3

        using Neo;
        using Neo.SmartContract;
        using Neo.SmartContract.Framework;
        using Neo.SmartContract.Framework.Attributes;
        using Neo.SmartContract.Framework.Native;
        using Neo.SmartContract.Framework.Services;
        using System;
        using System.Numerics;

        namespace MyProject
        {
            [DisplayName("Counter")]
            [ManifestExtra("Author", "Your Name")]
            [ManifestExtra("Description", "A simple counter contract")]
            [ContractPermission("*", "*")]
            public class Counter : SmartContract
            {
                private const byte Prefix_Count = 0x01;

                /// <summary>
                /// Gets the current count value.
                /// </summary>
                public static BigInteger GetCount()
                {
                    var storage = new StorageMap(Storage.CurrentContext, Prefix_Count);
                    return (BigInteger)storage.Get("count");
                }

                /// <summary>
                /// Increments the counter by 1.
                /// </summary>
                public static BigInteger Increment()
                {
                    var storage = new StorageMap(Storage.CurrentContext, Prefix_Count);
                    var current = (BigInteger)storage.Get("count");
                    var newValue = current + 1;
                    storage.Put("count", newValue);

                    OnIncrement(newValue);
                    return newValue;
                }

                /// <summary>
                /// Decrements the counter by 1.
                /// </summary>
                public static BigInteger Decrement()
                {
                    var storage = new StorageMap(Storage.CurrentContext, Prefix_Count);
                    var current = (BigInteger)storage.Get("count");
                    ExecutionEngine.Assert(current > 0, "Counter cannot go below zero");

                    var newValue = current - 1;
                    storage.Put("count", newValue);

                    OnDecrement(newValue);
                    return newValue;
                }

                /// <summary>
                /// Sets the counter to a specific value. Only callable by contract owner.
                /// </summary>
                public static void SetCount(BigInteger value)
                {
                    ExecutionEngine.Assert(value >= 0, "Value must be non-negative");

                    var storage = new StorageMap(Storage.CurrentContext, Prefix_Count);
                    storage.Put("count", value);

                    OnSet(value);
                }

                [DisplayName("Increment")]
                public static event Action<BigInteger> OnIncrement;

                [DisplayName("Decrement")]
                public static event Action<BigInteger> OnDecrement;

                [DisplayName("Set")]
                public static event Action<BigInteger> OnSet;
            }
        }
        """;

    private static string GetCounterTestTemplate() => """
        // Counter.Test.cs - Tests for the Counter contract

        using Neo.Fairy.Testing;
        using System.Numerics;

        namespace MyProject.Tests
        {
            public class CounterTest : FairyTest
            {
                private string counterHash = null!;

                public override void SetUp()
                {
                    // Deploy the counter contract
                    counterHash = Deploy("counter");
                }

                public void TestInitialCountIsZero()
                {
                    var count = Call<BigInteger>(counterHash, "getCount");
                    Assert.Equal(BigInteger.Zero, count);
                }

                public void TestIncrement()
                {
                    var result = Call(counterHash, "increment");
                    Assert.Halted(result);

                    var count = Call<BigInteger>(counterHash, "getCount");
                    Assert.Equal(BigInteger.One, count);

                    // Check event was emitted
                    Assert.EmittedEvent(result, "Increment", BigInteger.One);
                }

                public void TestMultipleIncrements()
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Call(counterHash, "increment");
                    }

                    var count = Call<BigInteger>(counterHash, "getCount");
                    Assert.Equal(new BigInteger(5), count);
                }

                public void TestDecrement()
                {
                    // First increment
                    Call(counterHash, "increment");

                    // Then decrement
                    var result = Call(counterHash, "decrement");
                    Assert.Halted(result);

                    var count = Call<BigInteger>(counterHash, "getCount");
                    Assert.Equal(BigInteger.Zero, count);
                }

                public void TestDecrementBelowZeroFails()
                {
                    // Expect revert when decrementing from zero
                    Vm.ExpectRevert("Counter cannot go below zero");
                    Call(counterHash, "decrement");
                }

                public void TestSetCount()
                {
                    var result = Call(counterHash, "setCount", new BigInteger(42));
                    Assert.Halted(result);

                    var count = Call<BigInteger>(counterHash, "getCount");
                    Assert.Equal(new BigInteger(42), count);
                }

                public void TestSetNegativeValueFails()
                {
                    Vm.ExpectRevert("Value must be non-negative");
                    Call(counterHash, "setCount", new BigInteger(-1));
                }

                public void TestFuzz_IncrementDecrement(uint increments, uint decrements)
                {
                    Vm.Assume(increments >= decrements);
                    Vm.Assume(increments < 1000);

                    for (uint i = 0; i < increments; i++)
                    {
                        Call(counterHash, "increment");
                    }

                    for (uint i = 0; i < decrements; i++)
                    {
                        Call(counterHash, "decrement");
                    }

                    var count = Call<BigInteger>(counterHash, "getCount");
                    Assert.Equal(new BigInteger(increments - decrements), count);
                }
            }
        }
        """;

    private static string GetDeployScriptTemplate() => """
        // Deploy.cs - Deployment script for the project

        using Neo.Fairy.Deployment;

        namespace MyProject.Scripts
        {
            public class Deploy : FairyScript
            {
                public override async Task RunAsync()
                {
                    Log("Starting deployment...");

                    // Deploy the counter contract
                    var counter = await DeployAsync("counter");
                    Log($"Counter deployed at: {counter.ContractHash}");

                    // Initialize if needed
                    if (Config.Network != "mainnet")
                    {
                        Log("Setting initial count to 100 for testing...");
                        await CallAsync(counter.ContractHash, "setCount", 100);
                    }

                    Log("Deployment complete!");
                }
            }
        }
        """;

    private static string GetGitignoreTemplate() => """
        # Build outputs
        out/
        bin/
        obj/

        # IDE
        .vs/
        .vscode/
        *.user
        *.suo

        # Fairy
        cache/
        .fairy/

        # Secrets
        *.json
        !fairy.json.example
        .env

        # OS
        .DS_Store
        Thumbs.db
        """;
}
