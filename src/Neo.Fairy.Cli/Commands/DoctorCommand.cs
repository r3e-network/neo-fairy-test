// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to validate local environment prerequisites for Neo Fairy development.
/// </summary>
public static class DoctorCommand
{
    private const string TargetFramework = "net10.0";

    public static Command Create()
    {
        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL (defaults to fairy.toml or http://localhost:16868)");

        var neoRootOption = new Option<string?>(
            name: "--neo-root",
            description: "Path to Neo repo root (defaults to NEOROOT/NeoRoot, or auto-detected via ../neo_csharp or ../neo).");

        var neoCliOption = new Option<string?>(
            name: "--neo-cli",
            description: "Path to neo-cli (neo-cli.dll or executable). When omitted, auto-detected from --neo-root.");

        var networkOption = new Option<string>(
            aliases: new[] { "--network", "-n" },
            description: "Neo CLI config preset to validate (mainnet|testnet|private|<config.json>)",
            getDefaultValue: () => "mainnet");

        var command = new Command("doctor", "Check tooling, Neo repo, and node connectivity")
        {
            rpcOption,
            neoRootOption,
            neoCliOption,
            networkOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForOption(rpcOption),
                ctx.ParseResult.GetValueForOption(neoRootOption),
                ctx.ParseResult.GetValueForOption(neoCliOption),
                ctx.ParseResult.GetValueForOption(networkOption) ?? "mainnet",
                GlobalOptions.IsJson(ctx));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string? rpcUrl,
        string? neoRoot,
        string? neoCli,
        string network,
        bool json)
    {
        FairyProject? project = null;
        try
        {
            project = FairyProject.Load();
        }
        catch
        {
            // Not inside a Fairy project.
        }

        var resolvedRpc = RpcUrlResolver.Resolve(rpcUrl, project);

        var checks = new List<object>();

        Table? table = null;
        if (!json)
        {
            table = new Table().Border(TableBorder.Rounded).Title("fairy doctor");
            table.AddColumn("Check");
            table.AddColumn("Status");
            table.AddColumn("Details");
        }

        var hasFailures = false;

        AddRow("Dotnet runtime", true, Environment.Version.ToString());

        if (project != null)
        {
            AddRow("Fairy project", true, project.RootDirectory);
            AddRow("fairy.toml", true, project.ConfigPath);

            var compiler = project.Config.Compiler.Path;
            AddRow("Compiler (nccs)", LooksExecutable(compiler), compiler);
        }
        else
        {
            AddRow("Fairy project", true, "Not inside a fairy.toml project (ok for some commands)");
        }

        var (resolvedNeoRoot, resolvedNeoCli) = NeoCliLocator.ResolveNeoCli(neoRoot, neoCli, TargetFramework);

        if (resolvedNeoRoot == null && resolvedNeoCli == null)
        {
            AddRow("Neo root", false, "Not found (set --neo-root or NEOROOT)");
        }
        else
        {
            if (resolvedNeoRoot == null)
            {
                AddRow("Neo root", true, "Not found (ok when using --neo-cli)");
            }
            else
            {
                AddRow("Neo root", true, resolvedNeoRoot);
            }

            if (resolvedNeoCli == null)
            {
                var detail = resolvedNeoRoot != null
                    ? $"Not found under {resolvedNeoRoot}"
                    : "Not found (pass --neo-cli)";
                AddRow("neo-cli", false, detail);
            }
            else
            {
                AddRow("neo-cli", true, resolvedNeoCli);

                var neoCliDir = Path.GetDirectoryName(resolvedNeoCli)!;
                var resolvedConfig = NeoCliLocator.ResolveNeoCliConfigPath(neoCliDir, network);
                AddRow("neo-cli config", resolvedConfig != null, resolvedConfig ?? $"Missing preset {network}");

                var fairyPluginDir = Path.Combine(neoCliDir, "Plugins", "Fairy");
                var fairyDll = Path.Combine(fairyPluginDir, "Fairy.dll");
                AddRow("Fairy plugin", File.Exists(fairyDll), fairyPluginDir);
            }
        }

        var client = new FairyRpcClient(resolvedRpc);
        bool reachable;
        try
        {
            reachable = await client.PingAsync();
        }
        catch
        {
            reachable = false;
        }

        AddRow("Fairy RPC", reachable, resolvedRpc);

        if (json)
        {
            JsonOutput.Write(new
            {
                ok = !hasFailures,
                rpcUrl = resolvedRpc,
                checks
            });
            return hasFailures ? 1 : 0;
        }

        AnsiConsole.Write(table!);

        if (!reachable)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Hint:[/] Start a node with `fairy node start`.");
        }

        return hasFailures ? 1 : 0;

        void AddRow(string check, bool ok, string details)
        {
            checks.Add(new { check, ok, details });

            if (json)
            {
                if (!ok) hasFailures = true;
                return;
            }

            var status = ok ? "[green]✓[/]" : "[red]✗[/]";
            if (!ok) hasFailures = true;
            table!.AddRow(check, status, details.EscapeMarkup());
        }
    }

    private static bool LooksExecutable(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (Path.IsPathRooted(path))
            return File.Exists(path);

        if (path.Contains(Path.DirectorySeparatorChar) || path.Contains(Path.AltDirectorySeparatorChar))
            return File.Exists(Path.GetFullPath(path));

        var envPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, path);
            if (File.Exists(candidate))
                return true;

            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(candidate + ".exe"))
                    return true;
                if (File.Exists(candidate + ".cmd"))
                    return true;
                if (File.Exists(candidate + ".bat"))
                    return true;
            }
        }

        return false;
    }
}
