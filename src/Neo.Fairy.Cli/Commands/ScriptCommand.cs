// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neo.Fairy.Cli.Services;
using Neo.Fairy.Core.Configuration;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Deployment;
using Neo.Fairy.Engine;
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
            description: "Script file or class to run (e.g., script/Deploy.cs or Deploy)");

        var networkOption = new Option<string?>(
            aliases: new[] { "--network", "-n" },
            description: "Target network (mainnet, testnet, neo-express, or RPC URL)");

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL (overrides --network and fairy.toml)");

        var sessionOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "Execute inside a named Fairy session (default: ephemeral)");

        var deployerOption = new Option<string?>(
            name: "--deployer",
            description: "Deployer account script hash (defaults to FAIRY_DEPLOYER env or random)");

        var walletOption = new Option<string?>(
            aliases: new[] { "--wallet", "-w" },
            description: "Wallet file, NEP2 key, or WIF for --broadcast");

        var passwordOption = new Option<string?>(
            name: "--password",
            description: "Wallet password (or FAIRY_WALLET_PASSWORD env)");

        var broadcastOption = new Option<bool>(
            name: "--broadcast",
            description: "Relay transactions on-chain (requires session wallet)");

        var verifyOption = new Option<bool>(
            name: "--verify",
            description: "Verify contracts after deployment (not yet supported)");

        var command = new Command("script", "Run a deployment or migration script")
        {
            scriptArgument,
            networkOption,
            rpcOption,
            sessionOption,
            deployerOption,
            walletOption,
            passwordOption,
            broadcastOption,
            verifyOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(scriptArgument),
                ctx.ParseResult.GetValueForOption(networkOption),
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(sessionOption),
                ctx.ParseResult.GetValueForOption(deployerOption),
                ctx.ParseResult.GetValueForOption(walletOption),
                ctx.ParseResult.GetValueForOption(passwordOption),
                ctx.ParseResult.GetValueForOption(broadcastOption),
                ctx.ParseResult.GetValueForOption(verifyOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string script,
        string? network,
        string? rpcUrl,
        string? session,
        string? deployer,
        string? wallet,
        string? password,
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
            return 1;
        }

        if (verify)
        {
            AnsiConsole.MarkupLine("[grey]Verify flag requested; verification is not implemented yet.[/]");
        }

        var (_, resolvedRpcUrl) = NetworkResolver.Resolve(network, project.Config.Fairy);
        var rpcEndpoint = rpcUrl ?? resolvedRpcUrl;
        var client = new FairyRpcClient(rpcEndpoint);

        var sessionId = session ?? $"script_{Guid.NewGuid():N}";

        try
        {
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

            var assembly = CompileScripts(project.ScriptDirectory);
            var scriptType = ResolveScriptType(assembly, script);

            var scriptInstance = (FairyScript)Activator.CreateInstance(scriptType)!;
            InitializeScript(scriptInstance, project, client, sessionId, broadcast, deployer);

            AnsiConsole.MarkupLine($"[green]Running script:[/] {scriptType.FullName}");
            if (!broadcast)
            {
                AnsiConsole.MarkupLine("[yellow]Simulation mode (use --broadcast to relay on-chain)[/]");
            }
            AnsiConsole.WriteLine();

            await scriptInstance.RunAsync();

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ Script completed successfully[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Script failed:[/] {ex.Message.EscapeMarkup()}");
            if (project.Config.Test.Verbosity >= 3)
            {
                AnsiConsole.WriteException(ex);
            }
            return 1;
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
                    // best-effort cleanup
                }
            }
        }
    }

    private static Assembly CompileScripts(string scriptDirectory)
    {
        if (!Directory.Exists(scriptDirectory))
        {
            throw new InvalidOperationException($"Script directory not found: {scriptDirectory}");
        }

        var scriptFiles = Directory.GetFiles(scriptDirectory, "*.cs", SearchOption.AllDirectories);
        if (scriptFiles.Length == 0)
        {
            throw new InvalidOperationException("No script files (.cs) found.");
        }

        var syntaxTrees = scriptFiles.Select(file =>
        {
            var code = File.ReadAllText(file);
            return CSharpSyntaxTree.ParseText(code, path: file);
        }).ToList();

        var references = CollectMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "Fairy.Scripts",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        if (!emitResult.Success)
        {
            var diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();

            AnsiConsole.MarkupLine("[red]Script compilation failed:[/]");
            foreach (var diag in diagnostics)
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {diag.EscapeMarkup()}");
            }

            throw new InvalidOperationException("Failed to compile scripts.");
        }

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
    }

    private static Type ResolveScriptType(Assembly assembly, string scriptArg)
    {
        var scriptTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(FairyScript).IsAssignableFrom(t))
            .ToList();

        if (scriptTypes.Count == 0)
        {
            throw new InvalidOperationException("No FairyScript classes found in script assembly.");
        }

        var classFilter = scriptArg;
        if (scriptArg.Contains("::", StringComparison.Ordinal))
        {
            classFilter = scriptArg.Split("::", 2)[1];
        }
        else if (scriptArg.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            classFilter = Path.GetFileNameWithoutExtension(scriptArg);
        }

        var selected = scriptTypes.FirstOrDefault(t =>
            t.Name.Equals(classFilter, StringComparison.OrdinalIgnoreCase));

        if (selected != null)
        {
            return selected;
        }

        if (scriptTypes.Count == 1)
        {
            return scriptTypes[0];
        }

        var available = string.Join(", ", scriptTypes.Select(t => t.Name));
        throw new InvalidOperationException($"Script '{classFilter}' not found. Available scripts: {available}");
    }

    private static void InitializeScript(
        FairyScript script,
        FairyProject project,
        FairyRpcClient client,
        string sessionId,
        bool broadcast,
        string? deployer)
    {
        var initMethod = typeof(FairyScript).GetMethod(
            "Initialize",
            BindingFlags.Instance | BindingFlags.NonPublic);

        initMethod?.Invoke(script, new object?[] { project, client, sessionId, broadcast, deployer });
    }

    private static List<MetadataReference> CollectMetadataReferences()
    {
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add trusted platform assemblies (standard Roslyn approach)
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trustedAssemblies))
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && seen.Add(path))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                    }
                    catch
                    {
                        // Skip problematic assemblies
                    }
                }
            }
        }

        // 2. Add Neo.Fairy assemblies explicitly
        var fairyAssemblies = new[]
        {
            typeof(Neo.Fairy.Core.Configuration.FairyConfig).Assembly,
            typeof(Neo.Fairy.Engine.FairyRpcClient).Assembly,
            typeof(Neo.Fairy.Deployment.FairyScript).Assembly,
        };

        foreach (var assembly in fairyAssemblies)
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && seen.Add(location))
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }
        }

        // 3. Fallback: add loaded assemblies from current AppDomain
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            var location = assembly.Location;
            if (string.IsNullOrEmpty(location))
                continue;

            if (seen.Add(location))
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }
        }

        return references;
    }
}
