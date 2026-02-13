// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Neo.Fairy.Core.CodeGen;
using Neo.Fairy.Core.Models;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to inspect compiled contract artifacts and manifests.
/// Similar to 'forge inspect' in Foundry.
/// </summary>
public static class InspectCommand
{
    public static Command Create()
    {
        var contractArgument = new Argument<string>(
            name: "contract",
            description: "Contract alias/name from fairy.toml or path to .nef");

        var deployerOption = new Option<string?>(
            name: "--deployer",
            description: "Sender/deployer script hash for predicted hash (defaults to FAIRY_DEPLOYER env)");

        var jsonOption = new Option<bool>(
            name: "--json",
            description: "Print raw manifest JSON only");

        var command = new Command("inspect", "Inspect a compiled contract")
        {
            contractArgument,
            deployerOption,
            jsonOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForArgument(contractArgument),
                ctx.ParseResult.GetValueForOption(deployerOption),
                ctx.ParseResult.GetValueForOption(jsonOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(string contract, string? deployer, bool json)
    {
        FairyProject? project = null;
        ContractArtifact? artifact = null;

        try
        {
            project = FairyProject.Load();
            await project.LoadArtifactsAsync();

            artifact = project.GetArtifact(contract)
                       ?? project.Artifacts.FirstOrDefault(a =>
                           string.Equals(a.Name, contract, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Not inside a Fairy project, fall back to file inspection if possible.
        }

        if (artifact == null && File.Exists(contract))
        {
            var nefPath = contract;
            var baseName = Path.GetFileNameWithoutExtension(nefPath);
            var manifestPath = Path.ChangeExtension(nefPath, ".manifest.json");
            if (!File.Exists(manifestPath))
            {
                AnsiConsole.MarkupLine($"[red]Manifest not found:[/] {manifestPath}");
                return 1;
            }

            artifact = await ContractArtifact.LoadFromFilesAsync(
                baseName,
                nefPath,
                manifestPath,
                debugInfoPath: Path.ChangeExtension(nefPath, ".nefdbgnfo"));
        }

        if (artifact == null)
        {
            AnsiConsole.MarkupLine("[red]Contract artifact not found.[/]");
            AnsiConsole.MarkupLine("[grey]Run `fairy build` first or pass a path to a .nef file.[/]");
            return 1;
        }

        if (json)
        {
            AnsiConsole.WriteLine(artifact.ManifestJson);
            return 0;
        }

        var parsedManifest = ManifestParser.Parse(artifact.ManifestJson);

        var header = new Panel($"[green]{artifact.Alias}[/] ({parsedManifest.Name})")
            .Header("Contract")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(header);

        var infoTable = new Table().Border(TableBorder.Rounded).Title("Artifact");
        infoTable.AddColumn("Field");
        infoTable.AddColumn("Value");

        infoTable.AddRow("Alias", artifact.Alias);
        infoTable.AddRow("Name", parsedManifest.Name);
        infoTable.AddRow("NEF size", $"{artifact.NefBytes.Length} bytes");
        infoTable.AddRow("NEF checksum", $"0x{artifact.NefChecksum:x8}");

        if (artifact.DebugInfoBytes != null)
        {
            infoTable.AddRow("Debug info", "present");
        }
        else
        {
            infoTable.AddRow("Debug info", "missing");
        }

        if (!string.IsNullOrWhiteSpace(artifact.InitializationDataJson))
        {
            infoTable.AddRow("_deploy data", artifact.InitializationDataJson.EscapeMarkup());
        }

        var deployerHash = deployer
                           ?? Environment.GetEnvironmentVariable("FAIRY_DEPLOYER");

        if (!string.IsNullOrWhiteSpace(deployerHash) &&
            deployerHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                infoTable.AddRow("Predicted hash", artifact.GetPredictedHash(deployerHash));
            }
            catch (Exception ex)
            {
                infoTable.AddRow("Predicted hash", $"error: {ex.Message.EscapeMarkup()}");
            }
        }

        if (artifact.SourcePath != null)
        {
            infoTable.AddRow("Source path", artifact.SourcePath);
        }

        AnsiConsole.Write(infoTable);

        if (parsedManifest.SupportedStandards.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]Supported standards:[/] {string.Join(", ", parsedManifest.SupportedStandards)}");
        }

        PrintPermissions(parsedManifest);
        PrintAbi(parsedManifest);

        return 0;
    }

    private static void PrintPermissions(Neo.Fairy.Core.CodeGen.ContractManifest manifest)
    {
        if (manifest.Permissions.Count == 0)
            return;

        AnsiConsole.WriteLine();
        var table = new Table().Border(TableBorder.Rounded).Title("Permissions");
        table.AddColumn("Contract");
        table.AddColumn("Methods");

        foreach (var perm in manifest.Permissions)
        {
            var methods = perm.Methods.Count == 0 ? "*" : string.Join(", ", perm.Methods);
            table.AddRow(perm.Contract, methods);
        }

        AnsiConsole.Write(table);
    }

    private static void PrintAbi(Neo.Fairy.Core.CodeGen.ContractManifest manifest)
    {
        AnsiConsole.WriteLine();

        var methods = manifest.Abi.Methods
            .OrderBy(m => m.IsInternal)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var methodTable = new Table().Border(TableBorder.Rounded).Title("Methods");
        methodTable.AddColumn("Name");
        methodTable.AddColumn("Safe");
        methodTable.AddColumn("Params");
        methodTable.AddColumn("Return");

        foreach (var method in methods)
        {
            var paramList = string.Join(", ", method.Parameters.Select(p => $"{p.Name}:{p.Type}"));
            methodTable.AddRow(
                method.Name,
                method.Safe ? "yes" : "no",
                paramList,
                method.ReturnType.ToString());
        }

        AnsiConsole.Write(methodTable);

        if (manifest.Abi.Events.Count == 0)
            return;

        AnsiConsole.WriteLine();
        var eventTable = new Table().Border(TableBorder.Rounded).Title("Events");
        eventTable.AddColumn("Name");
        eventTable.AddColumn("Params");

        foreach (var evt in manifest.Abi.Events)
        {
            var paramList = string.Join(", ", evt.Parameters.Select(p => $"{p.Name}:{p.Type}"));
            eventTable.AddRow(evt.Name, paramList);
        }

        AnsiConsole.Write(eventTable);
    }
}
