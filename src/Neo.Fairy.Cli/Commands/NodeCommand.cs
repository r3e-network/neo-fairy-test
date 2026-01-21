// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to manage the Fairy RPC node.
/// Similar to 'anvil' in Foundry.
/// </summary>
public static class NodeCommand
{
    private const string TargetFramework = "net10.0";

    public static Command Create()
    {
        var portOption = new Option<int>(
            aliases: new[] { "--port", "-p" },
            description: "RPC port",
            getDefaultValue: () => 16868);

        var hostOption = new Option<string>(
            aliases: new[] { "--host", "-H" },
            description: "Host address",
            getDefaultValue: () => "127.0.0.1");

        var networkOption = new Option<string>(
            aliases: new[] { "--network", "-n" },
            description: "Network to connect to (mainnet, testnet, or private)",
            getDefaultValue: () => "mainnet");

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL (overrides host/port/config)");

        var command = new Command("node", "Start or manage the Fairy RPC node")
        {
            portOption,
            hostOption,
            networkOption,
            rpcOption
        };

        // Add subcommands
        command.AddCommand(CreateStartCommand(portOption, hostOption, networkOption));
        command.AddCommand(CreateStatusCommand(portOption, hostOption, rpcOption));
        command.AddCommand(CreateStopCommand(portOption));

        return command;
    }

    private static Command CreateStartCommand(
        Option<int> portOption,
        Option<string> hostOption,
        Option<string> networkOption)
    {
        var neoRootOption = new Option<string?>(
            name: "--neo-root",
            description: "Path to Neo repo root (defaults to NEOROOT/NeoRoot, or auto-detected via ../neo_csharp or ../neo).");

        var neoCliOption = new Option<string?>(
            name: "--neo-cli",
            description: "Path to neo-cli (neo-cli.dll or executable). When omitted, auto-detected from --neo-root.");

        var configurationOption = new Option<string>(
            name: "--configuration",
            description: "Build configuration when compiling Fairy plugin (Debug|Release)",
            getDefaultValue: () => "Release");

        var noBuildPluginOption = new Option<bool>(
            name: "--no-build-plugin",
            description: "Skip building Fairy plugin from source (uses existing plugin install)");

        var noInstallPluginOption = new Option<bool>(
            name: "--no-install-plugin",
            description: "Skip installing Fairy plugin into neo-cli Plugins folder");

        var interactiveOption = new Option<bool>(
            name: "--interactive",
            description: "Run neo-cli interactively (disables --background)");

        var forceOption = new Option<bool>(
            name: "--force",
            description: "Kill any recorded node on the same port before starting");

        var command = new Command("start", "Start the Fairy RPC node")
        {
            portOption,
            hostOption,
            networkOption,
            neoRootOption,
            neoCliOption,
            configurationOption,
            noBuildPluginOption,
            noInstallPluginOption,
            interactiveOption,
            forceOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await StartAsync(
                ctx.ParseResult.GetValueForOption(portOption),
                ctx.ParseResult.GetValueForOption(hostOption) ?? "0.0.0.0",
                ctx.ParseResult.GetValueForOption(networkOption) ?? "mainnet",
                ctx.ParseResult.GetValueForOption(neoRootOption),
                ctx.ParseResult.GetValueForOption(neoCliOption),
                ctx.ParseResult.GetValueForOption(configurationOption) ?? "Release",
                ctx.ParseResult.GetValueForOption(noBuildPluginOption),
                ctx.ParseResult.GetValueForOption(noInstallPluginOption),
                ctx.ParseResult.GetValueForOption(interactiveOption),
                ctx.ParseResult.GetValueForOption(forceOption));
        });
        return command;
    }

    private static Command CreateStatusCommand(
        Option<int> portOption,
        Option<string> hostOption,
        Option<string?> rpcOption)
    {
        var command = new Command("status", "Check node status");
        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await StatusAsync(
                ctx.ParseResult.GetValueForOption(portOption),
                ctx.ParseResult.GetValueForOption(hostOption) ?? "127.0.0.1",
                ctx.ParseResult.GetValueForOption(rpcOption),
                GlobalOptions.IsJson(ctx));
        });
        return command;
    }

    private static Command CreateStopCommand(Option<int> portOption)
    {
        var command = new Command("stop", "Stop the Fairy RPC node started by `fairy node start`")
        {
            portOption
        };
        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await StopAsync(ctx.ParseResult.GetValueForOption(portOption));
        });
        return command;
    }

    private static async Task<int> StartAsync(
        int port,
        string host,
        string network,
        string? neoRoot,
        string? neoCli,
        string configuration,
        bool noBuildPlugin,
        bool noInstallPlugin,
        bool interactive,
        bool force)
    {
        AnsiConsole.Write(new FigletText("Fairy Node").Color(Color.Green));

        AnsiConsole.MarkupLine($"[green]Starting Fairy RPC node...[/]");
        AnsiConsole.WriteLine();

        var existing = FairyNodeProcessStore.TryRead(port);
        if (existing != null)
        {
            var alreadyRunning = false;
            try
            {
                using var existingProcess = Process.GetProcessById(existing.Pid);
                alreadyRunning = !existingProcess.HasExited;

                if (alreadyRunning && force)
                {
                    AnsiConsole.MarkupLine($"[yellow]Killing existing node PID {existing.Pid} on port {port} (--force)...[/]");
                    existingProcess.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                alreadyRunning = false;
            }

            if (alreadyRunning && !force)
            {
                AnsiConsole.MarkupLine($"[red]A Fairy node is already recorded on port {port} (PID {existing.Pid}).[/]");
                AnsiConsole.MarkupLine($"[grey]Stop it with[/] [white]fairy node stop --port {port}[/]");
                AnsiConsole.MarkupLine("[grey]Or restart with[/] [white]fairy node start --force[/]");
                return 1;
            }

            if (!alreadyRunning)
            {
                FairyNodeProcessStore.Delete(port);
            }
        }

        var (resolvedNeoRoot, resolvedNeoCli) = NeoCliLocator.ResolveNeoCli(neoRoot, neoCli, TargetFramework);
        if (resolvedNeoCli == null)
        {
            AnsiConsole.MarkupLine("[red]neo-cli not found.[/]");
            AnsiConsole.MarkupLine("[grey]Build Neo.CLI first or pass --neo-cli <path-to-neo-cli.dll>.[/]");
            AnsiConsole.MarkupLine("[grey]Optionally set --neo-root or NEOROOT/NeoRoot env var for auto-detection.[/]");
            return 1;
        }

        var neoCliDirectory = Path.GetDirectoryName(resolvedNeoCli)!;
        var resolvedConfigPath = NeoCliLocator.ResolveNeoCliConfigPath(neoCliDirectory, network);
        if (resolvedConfigPath == null)
        {
            AnsiConsole.MarkupLine($"[red]Config not found for network:[/] {network.EscapeMarkup()}");
            AnsiConsole.MarkupLine("[grey]Use --network mainnet|testnet|private or pass a config filename/path.[/]");
            return 1;
        }

        uint? networkMagic = FairyPluginInstaller.TryReadNetworkMagic(resolvedConfigPath);

        var pluginsRoot = Path.Combine(neoCliDirectory, "Plugins");
        var fairyPluginDir = Path.Combine(pluginsRoot, "Fairy");

        if (!noInstallPlugin)
        {
            Directory.CreateDirectory(fairyPluginDir);

            if (!noBuildPlugin)
            {
                if (resolvedNeoRoot == null)
                {
                    AnsiConsole.MarkupLine("[red]Neo root is required to build the Fairy plugin.[/]");
                    AnsiConsole.MarkupLine("[grey]Provide --neo-root/NEOROOT, or use --no-build-plugin if already installed.[/]");
                    return 1;
                }

                var fairyRepoRoot = NeoCliLocator.FindFairyRepoRoot();
                if (fairyRepoRoot == null)
                {
                    AnsiConsole.MarkupLine("[red]Fairy plugin source not found.[/]");
                    AnsiConsole.MarkupLine("[grey]Run from the neo-fairy repo root, or pass --no-build-plugin/--no-install-plugin.[/]");
                    return 1;
                }

                var (ok, error) = await FairyPluginInstaller.BuildFromSourceAsync(fairyRepoRoot, resolvedNeoRoot, configuration);
                if (!ok)
                {
                    AnsiConsole.MarkupLine($"[red]Plugin build failed:[/] {error?.EscapeMarkup()}");
                    return 1;
                }

                var pluginOutputDir = Path.Combine(
                    fairyRepoRoot,
                    "src",
                    "Fairy.Plugin",
                    "bin",
                    configuration,
                    TargetFramework);

                if (!Directory.Exists(pluginOutputDir))
                {
                    AnsiConsole.MarkupLine($"[red]Plugin output not found:[/] {pluginOutputDir.EscapeMarkup()}");
                    return 1;
                }

                FairyPluginInstaller.CopyDirectory(pluginOutputDir, fairyPluginDir);
            }

            var rpcServerJson = Path.Combine(fairyPluginDir, "RpcServer.json");
            if (File.Exists(rpcServerJson))
            {
                var (patched, patchError) = FairyPluginInstaller.PatchRpcServerConfig(rpcServerJson, host, port, networkMagic);
                if (!patched)
                {
                    AnsiConsole.MarkupLine($"[yellow]Failed to patch RpcServer.json:[/] {patchError?.EscapeMarkup()}");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] RpcServer.json not found in {fairyPluginDir.EscapeMarkup()}");
            }

            // If we didn't build/copy, ensure the plugin DLL exists.
            if (noBuildPlugin)
            {
                var fairyDll = Path.Combine(fairyPluginDir, "Fairy.dll");
                if (!File.Exists(fairyDll))
                {
                    AnsiConsole.MarkupLine($"[red]Fairy plugin not installed:[/] {fairyDll.EscapeMarkup()}");
                    AnsiConsole.MarkupLine("[grey]Run `fairy plugin install` or omit --no-build-plugin.[/]");
                    return 1;
                }
            }
        }

        var table = new Table()
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Host", host);
        table.AddRow("Port", port.ToString());
        table.AddRow("Network", network);
        table.AddRow("WebSocket", $"{port + 1}");
        table.AddRow("Neo root", resolvedNeoRoot ?? "<unknown>");
        table.AddRow("neo-cli", resolvedNeoCli);
        table.AddRow("neo-cli config", resolvedConfigPath);
        table.AddRow("Plugins dir", pluginsRoot);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var urlHost = FormatHostForUrl(host);
        AnsiConsole.MarkupLine($"[green]RPC endpoint:[/] http://{urlHost}:{port}");
        AnsiConsole.MarkupLine($"[green]WebSocket:[/] ws://{urlHost}:{port + 1}");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[grey]Press Ctrl+C to stop (or run `fairy node stop --port {port}`)[/]");

        var (fileName, args) = BuildNeoCliInvocation(resolvedNeoCli, resolvedConfigPath, interactive);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            WorkingDirectory = neoCliDirectory,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to start neo-cli process.[/]");
            return 1;
        }

        try
        {
            FairyNodeProcessStore.Write(new FairyNodeProcessInfo
            {
                Pid = process.Id,
                Port = port,
                Host = host,
                RpcUrl = $"http://{urlHost}:{port}",
                NeoCliPath = resolvedNeoCli,
                NeoCliConfigPath = resolvedConfigPath,
                WorkingDirectory = neoCliDirectory,
                StartedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to record node PID: {ex.Message.EscapeMarkup()}");
        }

        var cancelled = false;
        void CancelHandler(object? _, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            cancelled = true;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // best-effort
            }
        }

        Console.CancelKeyPress += CancelHandler;

        try
        {
            await process.WaitForExitAsync();
        }
        finally
        {
            Console.CancelKeyPress -= CancelHandler;
            FairyNodeProcessStore.Delete(port);
        }

        if (cancelled)
        {
            AnsiConsole.MarkupLine("[yellow]Shutting down...[/]");
        }

        return process.ExitCode;
    }

    private static async Task<int> StatusAsync(int port, string host, string? rpcUrl, bool json)
    {
        FairyProject? project = null;
        if (string.IsNullOrWhiteSpace(rpcUrl))
        {
            try
            {
                project = FairyProject.Load();
            }
            catch
            {
                // Not inside a Fairy project.
            }
        }

        var defaultRpc = $"http://{FormatHostForUrl(host)}:{port}";
        var resolvedRpc = RpcUrlResolver.Resolve(rpcUrl, project, defaultRpc);

        var stateFilePath = FairyNodeProcessStore.GetStatePath(port);
        var recorded = FairyNodeProcessStore.TryRead(port);
        bool? recordedPidRunning = null;
        if (recorded != null)
        {
            try
            {
                using var p = Process.GetProcessById(recorded.Pid);
                recordedPidRunning = !p.HasExited;
            }
            catch
            {
                recordedPidRunning = false;
            }

            if (!json)
            {
                var runningLabel = recordedPidRunning == true ? "[green]running[/]" : "[yellow]not running[/]";
                AnsiConsole.MarkupLine($"[grey]Recorded node:[/] PID {recorded.Pid} ({runningLabel}), started {recorded.StartedAtUtc.UtcDateTime:u}");
                AnsiConsole.MarkupLine($"[grey]State file:[/] {stateFilePath.EscapeMarkup()}");
                AnsiConsole.WriteLine();
            }
        }

        if (!json)
        {
            AnsiConsole.MarkupLine($"[grey]Checking Fairy node at {resolvedRpc}...[/]");
        }

        var client = new FairyRpcClient(resolvedRpc);
        if (!await client.PingAsync())
        {
            if (json)
            {
                JsonOutput.Write(new
                {
                    ok = false,
                    rpcUrl = resolvedRpc,
                    reachable = false,
                    recorded = recorded == null
                        ? null
                        : new
                        {
                            port,
                            pid = recorded.Pid,
                            running = recordedPidRunning,
                            stateFile = stateFilePath,
                            startedAtUtc = recorded.StartedAtUtc
                        }
                });
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Node Status:[/] Not reachable");
                AnsiConsole.MarkupLine("[grey]Ensure neo-cli is running with the Fairy plugin.[/]");
                if (recorded != null && recordedPidRunning != true)
                {
                    AnsiConsole.MarkupLine($"[grey]Hint:[/] Recorded node PID is not running; clean up with `fairy node stop --port {port}`.");
                }
            }
            return 1;
        }

        var hello = await client.HelloFairyAsync();
        var currentIndex = hello.GetValueOrDefault("currentindex")?.ToString() ?? "unknown";
        var syncUntil = hello.GetValueOrDefault("syncuntilblock")?.ToString() ?? "unknown";

        IReadOnlyList<string> sessions;
        try
        {
            sessions = await client.ListSnapshotsAsync();
        }
        catch
        {
            sessions = Array.Empty<string>();
        }

        if (json)
        {
            JsonOutput.Write(new
            {
                ok = true,
                rpcUrl = resolvedRpc,
                reachable = true,
                hello,
                sessions,
                recorded = recorded == null
                    ? null
                    : new
                    {
                        port,
                        pid = recorded.Pid,
                        running = recordedPidRunning,
                        stateFile = stateFilePath,
                        startedAtUtc = recorded.StartedAtUtc
                    }
            });
            return 0;
        }

        AnsiConsole.MarkupLine("[green]Node Status:[/] Running");
        AnsiConsole.MarkupLine($"[grey]Current block index:[/] {currentIndex}");
        AnsiConsole.MarkupLine($"[grey]Sync until block:[/] {syncUntil}");
        AnsiConsole.MarkupLine($"[grey]Sessions:[/] {sessions.Count} active");

        if (sessions.Count > 0)
        {
            var table = new Table().AddColumn("Session");
            foreach (var s in sessions.OrderBy(s => s))
            {
                table.AddRow(s);
            }
            AnsiConsole.Write(table);
        }

        return 0;
    }

    private static async Task<int> StopAsync(int port)
    {
        var info = FairyNodeProcessStore.TryRead(port);
        if (info == null)
        {
            AnsiConsole.MarkupLine($"[yellow]No recorded Fairy node found for port {port}.[/]");
            AnsiConsole.MarkupLine($"[grey]State file:[/] {FairyNodeProcessStore.GetStatePath(port).EscapeMarkup()}");
            AnsiConsole.MarkupLine("[grey]Tip:[/] Start a node with `fairy node start` first.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Stopping node PID {info.Pid} ({info.RpcUrl})...[/]");

        try
        {
            using var process = Process.GetProcessById(info.Pid);
            if (process.HasExited)
            {
                AnsiConsole.MarkupLine("[yellow]Process already exited.[/]");
                FairyNodeProcessStore.Delete(port);
                return 0;
            }

            process.Kill(entireProcessTree: true);

            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(10)));
            if (completed != waitTask)
            {
                AnsiConsole.MarkupLine("[yellow]Timed out waiting for node to exit (kill sent).[/]");
            }

            FairyNodeProcessStore.Delete(port);
            AnsiConsole.MarkupLine("[green]✓[/] Node stopped.");
            return 0;
        }
        catch (ArgumentException)
        {
            AnsiConsole.MarkupLine("[yellow]Process not found (already stopped).[/]");
            FairyNodeProcessStore.Delete(port);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to stop node:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }

    private static (string FileName, string Arguments) BuildNeoCliInvocation(
        string neoCliPath,
        string configPath,
        bool interactive)
    {
        var isDll = neoCliPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var baseArgs = $"--config \"{configPath}\"";
        var backgroundArg = interactive ? string.Empty : " --background";

        if (isDll)
        {
            return ("dotnet", $"\"{neoCliPath}\" {baseArgs}{backgroundArg}");
        }

        return (neoCliPath, $"{baseArgs}{backgroundArg}");
    }

    private static string FormatHostForUrl(string host)
    {
        // 0.0.0.0/:: are bind addresses, not usable client URLs.
        if (host == "0.0.0.0")
            return "127.0.0.1";

        if (host == "::")
            return "[::1]";

        if (host.Contains(':') && !host.StartsWith("[", StringComparison.Ordinal))
            return $"[{host}]";

        return host;
    }

}
