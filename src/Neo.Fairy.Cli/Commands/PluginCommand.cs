// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Neo.Fairy.Cli.Utilities;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to build/install the Fairy plugin into neo-cli.
/// </summary>
public static class PluginCommand
{
    private const string TargetFramework = "net10.0";

    public static Command Create()
    {
        var command = new Command("plugin", "Build and install the Fairy plugin for neo-cli");
        command.AddCommand(CreateInstallCommand());
        command.AddCommand(CreateStatusCommand());
        command.AddCommand(CreateUninstallCommand());
        return command;
    }

    private static Command CreateInstallCommand()
    {
        var neoRootOption = new Option<string?>(
            name: "--neo-root",
            description: "Path to Neo repo root (defaults to NEOROOT/NeoRoot, or auto-detected via ../neo).");

        var neoCliOption = new Option<string?>(
            name: "--neo-cli",
            description: "Path to neo-cli (neo-cli.dll or executable). When omitted, auto-detected from --neo-root.");

        var fairyRootOption = new Option<string?>(
            name: "--fairy-root",
            description: "Path to neo-fairy repo root (defaults to walking up from CWD)");

        var configurationOption = new Option<string>(
            name: "--configuration",
            description: "Build configuration when compiling Fairy plugin (Debug|Release)",
            getDefaultValue: () => "Release");

        var hostOption = new Option<string>(
            aliases: new[] { "--host", "-H" },
            description: "Bind host address (patches RpcServer.json)",
            getDefaultValue: () => "127.0.0.1");

        var portOption = new Option<int>(
            aliases: new[] { "--port", "-p" },
            description: "RPC port (patches RpcServer.json)",
            getDefaultValue: () => 16868);

        var networkOption = new Option<string>(
            aliases: new[] { "--network", "-n" },
            description: "Neo CLI config preset to read network magic (mainnet|testnet|private|<config.json>)",
            getDefaultValue: () => "mainnet");

        var noBuildOption = new Option<bool>(
            name: "--no-build",
            description: "Skip building the Fairy plugin (copy existing output from bin/)");

        var command = new Command("install", "Install Fairy plugin into neo-cli Plugins/Fairy")
        {
            neoRootOption,
            neoCliOption,
            fairyRootOption,
            configurationOption,
            hostOption,
            portOption,
            networkOption,
            noBuildOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await InstallAsync(
                ctx.ParseResult.GetValueForOption(neoRootOption),
                ctx.ParseResult.GetValueForOption(neoCliOption),
                ctx.ParseResult.GetValueForOption(fairyRootOption),
                ctx.ParseResult.GetValueForOption(configurationOption) ?? "Release",
                ctx.ParseResult.GetValueForOption(hostOption) ?? "127.0.0.1",
                ctx.ParseResult.GetValueForOption(portOption),
                ctx.ParseResult.GetValueForOption(networkOption) ?? "mainnet",
                ctx.ParseResult.GetValueForOption(noBuildOption));
        });

        return command;
    }

    private static Command CreateStatusCommand()
    {
        var neoRootOption = new Option<string?>(
            name: "--neo-root",
            description: "Path to Neo repo root (defaults to NEOROOT/NeoRoot, or auto-detected via ../neo).");

        var neoCliOption = new Option<string?>(
            name: "--neo-cli",
            description: "Path to neo-cli (neo-cli.dll or executable). When omitted, auto-detected from --neo-root.");

        var command = new Command("status", "Show whether the Fairy plugin is installed")
        {
            neoRootOption,
            neoCliOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = Status(
                ctx.ParseResult.GetValueForOption(neoRootOption),
                ctx.ParseResult.GetValueForOption(neoCliOption),
                GlobalOptions.IsJson(ctx));
        });

        return command;
    }

    private static Command CreateUninstallCommand()
    {
        var neoRootOption = new Option<string?>(
            name: "--neo-root",
            description: "Path to Neo repo root (defaults to NEOROOT/NeoRoot, or auto-detected via ../neo).");

        var neoCliOption = new Option<string?>(
            name: "--neo-cli",
            description: "Path to neo-cli (neo-cli.dll or executable). When omitted, auto-detected from --neo-root.");

        var yesOption = new Option<bool>(
            aliases: new[] { "--yes", "-y" },
            description: "Skip confirmation prompt");

        var command = new Command("uninstall", "Remove the Fairy plugin from neo-cli Plugins/Fairy")
        {
            neoRootOption,
            neoCliOption,
            yesOption
        };

        command.SetHandler((InvocationContext ctx) =>
        {
            ctx.ExitCode = Uninstall(
                ctx.ParseResult.GetValueForOption(neoRootOption),
                ctx.ParseResult.GetValueForOption(neoCliOption),
                ctx.ParseResult.GetValueForOption(yesOption));
        });

        command.AddAlias("remove");
        command.AddAlias("rm");

        return command;
    }

    private static async Task<int> InstallAsync(
        string? neoRoot,
        string? neoCli,
        string? fairyRoot,
        string configuration,
        string host,
        int port,
        string network,
        bool noBuild)
    {
        var (resolvedNeoRoot, resolvedNeoCli) = NeoCliLocator.ResolveNeoCli(neoRoot, neoCli, TargetFramework);
        if (resolvedNeoCli == null)
        {
            AnsiConsole.MarkupLine("[red]neo-cli not found.[/]");
            AnsiConsole.MarkupLine("[grey]Build Neo.CLI first or pass --neo-cli <path-to-neo-cli.dll>.[/]");
            return 1;
        }

        if (!noBuild && resolvedNeoRoot == null)
        {
            AnsiConsole.MarkupLine("[red]Neo root is required to build the Fairy plugin.[/]");
            AnsiConsole.MarkupLine("[grey]Provide --neo-root/NEOROOT, or use --no-build to install an already-built plugin.[/]");
            return 1;
        }

        var neoCliDirectory = Path.GetDirectoryName(resolvedNeoCli)!;
        var pluginsRoot = Path.Combine(neoCliDirectory, "Plugins");
        var fairyPluginDir = Path.Combine(pluginsRoot, "Fairy");
        Directory.CreateDirectory(fairyPluginDir);

        var resolvedConfigPath = NeoCliLocator.ResolveNeoCliConfigPath(neoCliDirectory, network);
        uint? networkMagic = null;
        if (resolvedConfigPath != null)
        {
            networkMagic = FairyPluginInstaller.TryReadNetworkMagic(resolvedConfigPath);
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Neo CLI config not found for network `{network.EscapeMarkup()}`; skipping Network magic patch.");
        }

        var resolvedFairyRoot = !string.IsNullOrWhiteSpace(fairyRoot)
            ? Path.GetFullPath(fairyRoot)
            : NeoCliLocator.FindFairyRepoRoot();

        if (resolvedFairyRoot == null || !Directory.Exists(resolvedFairyRoot))
        {
            AnsiConsole.MarkupLine("[red]Fairy repo root not found.[/]");
            AnsiConsole.MarkupLine("[grey]Run from the neo-fairy repo, or pass --fairy-root <path>.[/]");
            return 1;
        }

        if (!noBuild)
        {
            AnsiConsole.MarkupLine($"[grey]Building Fairy plugin ({configuration})...[/]");
            var (ok, error) = await FairyPluginInstaller.BuildFromSourceAsync(resolvedFairyRoot, resolvedNeoRoot!, configuration);
            if (!ok)
            {
                AnsiConsole.MarkupLine($"[red]Plugin build failed:[/] {error?.EscapeMarkup()}");
                return 1;
            }
        }

        var pluginOutputDir = Path.Combine(
            resolvedFairyRoot,
            "src",
            "Fairy.Plugin",
            "bin",
            configuration,
            TargetFramework);

        if (!Directory.Exists(pluginOutputDir))
        {
            AnsiConsole.MarkupLine($"[red]Plugin output not found:[/] {pluginOutputDir.EscapeMarkup()}");
            AnsiConsole.MarkupLine("[grey]Build the plugin first, or remove --no-build.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[grey]Installing plugin to[/] {fairyPluginDir.EscapeMarkup()}");
        FairyPluginInstaller.CopyDirectory(pluginOutputDir, fairyPluginDir);

        var rpcServerJson = Path.Combine(fairyPluginDir, "RpcServer.json");
        if (File.Exists(rpcServerJson))
        {
            var (patched, patchError) = FairyPluginInstaller.PatchRpcServerConfig(rpcServerJson, host, port, networkMagic);
            if (!patched)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to patch RpcServer.json: {patchError?.EscapeMarkup()}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] RpcServer.json not found in {fairyPluginDir.EscapeMarkup()}");
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("Fairy plugin install");

        table.AddColumn("Setting");
        table.AddColumn("Value");

        table.AddRow("Neo root", resolvedNeoRoot.EscapeMarkup());
        table.AddRow("neo-cli", resolvedNeoCli.EscapeMarkup());
        table.AddRow("Plugins dir", pluginsRoot.EscapeMarkup());
        table.AddRow("Installed to", fairyPluginDir.EscapeMarkup());
        if (resolvedConfigPath != null)
            table.AddRow("neo-cli config", resolvedConfigPath.EscapeMarkup());
        table.AddRow("Bind", $"{host}:{port}");
        if (networkMagic != null)
            table.AddRow("Network magic", networkMagic.ToString()!);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓[/] Fairy plugin installed.");

        return 0;
    }

    private static int Status(string? neoRoot, string? neoCli, bool json)
    {
        var (resolvedNeoRoot, resolvedNeoCli) = NeoCliLocator.ResolveNeoCli(neoRoot, neoCli, TargetFramework);
        if (resolvedNeoCli == null)
        {
            if (json)
            {
                JsonOutput.Write(new
                {
                    ok = false,
                    error = "neo-cli not found"
                });
            }
            else
            {
                AnsiConsole.MarkupLine("[red]neo-cli not found.[/]");
                AnsiConsole.MarkupLine("[grey]Build Neo.CLI first or pass --neo-cli <path-to-neo-cli.dll>.[/]");
            }
            return 1;
        }

        var neoCliDirectory = Path.GetDirectoryName(resolvedNeoCli)!;
        var pluginsRoot = Path.Combine(neoCliDirectory, "Plugins");
        var fairyPluginDir = Path.Combine(pluginsRoot, "Fairy");
        var fairyDll = Path.Combine(fairyPluginDir, "Fairy.dll");
        var installed = File.Exists(fairyDll);

        if (json)
        {
            JsonOutput.Write(new
            {
                ok = installed,
                neoRoot = resolvedNeoRoot,
                neoCli = resolvedNeoCli,
                pluginsDir = pluginsRoot,
                fairyPluginDir,
                installed
            });
            return installed ? 0 : 1;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("Fairy plugin status");

        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("Neo root", (resolvedNeoRoot ?? "<unknown>").EscapeMarkup());
        table.AddRow("neo-cli", resolvedNeoCli.EscapeMarkup());
        table.AddRow("Plugins dir", pluginsRoot.EscapeMarkup());
        table.AddRow("Fairy plugin dir", fairyPluginDir.EscapeMarkup());
        table.AddRow("Installed", installed ? "[green]✓[/]" : "[red]✗[/]");

        AnsiConsole.Write(table);

        if (!installed)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Install with:[/] [white]fairy plugin install[/]");
        }

        return installed ? 0 : 1;
    }

    private static int Uninstall(string? neoRoot, string? neoCli, bool yes)
    {
        var (_, resolvedNeoCli) = NeoCliLocator.ResolveNeoCli(neoRoot, neoCli, TargetFramework);
        if (resolvedNeoCli == null)
        {
            AnsiConsole.MarkupLine("[red]neo-cli not found.[/]");
            AnsiConsole.MarkupLine("[grey]Build Neo.CLI first or pass --neo-cli <path-to-neo-cli.dll>.[/]");
            return 1;
        }

        var neoCliDirectory = Path.GetDirectoryName(resolvedNeoCli)!;
        var pluginsRoot = Path.Combine(neoCliDirectory, "Plugins");
        var fairyPluginDir = Path.Combine(pluginsRoot, "Fairy");

        if (!Directory.Exists(fairyPluginDir))
        {
            AnsiConsole.MarkupLine("[grey]Fairy plugin is not installed.[/]");
            return 0;
        }

        if (!yes)
        {
            if (!AnsiConsole.Confirm($"Delete [white]{fairyPluginDir.EscapeMarkup()}[/]?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[grey]Cancelled.[/]");
                return 1;
            }
        }

        try
        {
            Directory.Delete(fairyPluginDir, recursive: true);
            AnsiConsole.MarkupLine("[green]✓[/] Fairy plugin removed.");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to remove plugin:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }
    }
}
