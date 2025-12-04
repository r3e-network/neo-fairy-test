// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to manage the Fairy RPC node.
/// Similar to 'anvil' in Foundry.
/// </summary>
public static class NodeCommand
{
    public static Command Create()
    {
        var portOption = new Option<int>(
            aliases: new[] { "--port", "-p" },
            description: "RPC port",
            getDefaultValue: () => 16868);

        var hostOption = new Option<string>(
            aliases: new[] { "--host", "-h" },
            description: "Host address",
            getDefaultValue: () => "0.0.0.0");

        var networkOption = new Option<string>(
            aliases: new[] { "--network", "-n" },
            description: "Network to connect to (mainnet, testnet, or private)",
            getDefaultValue: () => "mainnet");

        var command = new Command("node", "Start or manage the Fairy RPC node")
        {
            portOption,
            hostOption,
            networkOption
        };

        // Add subcommands
        command.AddCommand(CreateStartCommand(portOption, hostOption, networkOption));
        command.AddCommand(CreateStatusCommand());
        command.AddCommand(CreateStopCommand());

        return command;
    }

    private static Command CreateStartCommand(
        Option<int> portOption,
        Option<string> hostOption,
        Option<string> networkOption)
    {
        var command = new Command("start", "Start the Fairy RPC node")
        {
            portOption,
            hostOption,
            networkOption
        };

        command.SetHandler(StartAsync, portOption, hostOption, networkOption);
        return command;
    }

    private static Command CreateStatusCommand()
    {
        var command = new Command("status", "Check node status");
        command.SetHandler(StatusAsync);
        return command;
    }

    private static Command CreateStopCommand()
    {
        var command = new Command("stop", "Stop the Fairy RPC node");
        command.SetHandler(StopAsync);
        return command;
    }

    private static async Task StartAsync(int port, string host, string network)
    {
        AnsiConsole.Write(new FigletText("Fairy Node")
            .Color(Color.Green));

        AnsiConsole.MarkupLine($"[green]Starting Fairy RPC node...[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("Host", host);
        table.AddRow("Port", port.ToString());
        table.AddRow("Network", network);
        table.AddRow("WebSocket", $"{port + 1}");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[green]RPC endpoint:[/] http://{host}:{port}");
        AnsiConsole.MarkupLine($"[green]WebSocket:[/] ws://{host}:{port + 1}");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop[/]");

        // In actual implementation, this would start the neo-cli with Fairy plugin
        // For now, just wait
        try
        {
            await Task.Delay(Timeout.Infinite);
        }
        catch (TaskCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Shutting down...[/]");
        }
    }

    private static async Task StatusAsync()
    {
        AnsiConsole.MarkupLine("[grey]Checking node status...[/]");
        await Task.Delay(100);

        // Placeholder - would actually check if node is running
        AnsiConsole.MarkupLine("[green]Node Status:[/] Running");
        AnsiConsole.MarkupLine("[grey]Uptime: 2h 34m[/]");
        AnsiConsole.MarkupLine("[grey]Sessions: 3 active[/]");
        AnsiConsole.MarkupLine("[grey]Block height: 12345678[/]");
    }

    private static async Task StopAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Stopping Fairy node...[/]");
        await Task.Delay(100);
        AnsiConsole.MarkupLine("[green]Node stopped[/]");
    }
}
