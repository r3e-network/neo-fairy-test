// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Neo.Fairy.Testing.Coverage;
using Spectre.Console;

namespace Neo.Fairy.Cli.Services;

internal static class CoverageCliHelper
{
    public static async Task<bool> PrintCoverageAsync(
        FairyProject project,
        IReadOnlyDictionary<string, string?> contractsByHash,
        string? outputDirectory = null,
        string? rpcUrl = null)
    {
        if (contractsByHash.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No contracts found for coverage.[/]");
            return false;
        }

        var resolvedRpcUrl = RpcUrlResolver.Resolve(rpcUrl, project);
        using var client = new FairyRpcClient(resolvedRpcUrl);
        var collector = new CoverageCollector();
        collector.Start();

        var succeeded = 0;
        foreach (var (hash, name) in contractsByHash)
        {
            try
            {
                var rawCoverage = await client.GetContractSourceCodeCoverageAsync(hash);
                ApplyFairyCoverage(collector, hash, rawCoverage);
                succeeded++;
            }
            catch (Exception ex)
            {
                var label = name ?? hash;
                AnsiConsole.MarkupLine($"[yellow]Coverage unavailable for {label}:[/] {ex.Message.EscapeMarkup()}");
            }
        }

        collector.Stop();

        if (succeeded == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Coverage query failed for all requested contracts.[/]");
            return false;
        }

        var report = collector.GetReport();

        foreach (var contract in report.Contracts)
        {
            if (contractsByHash.TryGetValue(contract.ContractHash, out var name) &&
                !string.IsNullOrWhiteSpace(name))
            {
                contract.ContractName = name;
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine(CoverageReporter.GenerateConsoleSummary(report));

        var outDir = outputDirectory;
        if (string.IsNullOrWhiteSpace(outDir))
        {
            outDir = Path.Combine(project.OutputDirectory, "coverage");
        }

        try
        {
            CoverageReporter.WriteReports(report, outDir);
            AnsiConsole.MarkupLine($"[grey]Coverage reports written to[/] {outDir}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Failed to write coverage reports:[/] {ex.Message.EscapeMarkup()}");
        }

        return true;
    }

    private static void ApplyFairyCoverage(
        CoverageCollector collector,
        string contractHash,
        Dictionary<string, object?> rawCoverage)
    {
        var totalInstructions = new HashSet<int>();
        var fileTotalLines = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var fileExecutedLines = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, valueObj) in rawCoverage)
        {
            var parsed = ParseSourceCoverageKey(key);
            if (parsed == null)
                continue;

            var (file, line) = parsed.Value;

            if (!fileTotalLines.TryGetValue(file, out var totalLineSet))
            {
                totalLineSet = new HashSet<int>();
                fileTotalLines[file] = totalLineSet;
            }

            totalLineSet.Add(line);

            if (valueObj is not Dictionary<string, object?> opcodeMap)
                continue;

            var lineExecuted = false;
            foreach (var (ipKey, coveredObj) in opcodeMap)
            {
                if (!int.TryParse(ipKey, out var ip))
                    continue;

                totalInstructions.Add(ip);

                var covered = coveredObj is bool b
                    ? b
                    : bool.TryParse(coveredObj?.ToString(), out var parsedBool) && parsedBool;

                if (covered)
                {
                    collector.RecordInstruction(contractHash, ip, "OP");
                    lineExecuted = true;
                }
            }

            if (lineExecuted)
            {
                if (!fileExecutedLines.TryGetValue(file, out var executedSet))
                {
                    executedSet = new HashSet<int>();
                    fileExecutedLines[file] = executedSet;
                }

                executedSet.Add(line);
            }
        }

        collector.SetTotalInstructions(contractHash, totalInstructions.Count);

        foreach (var (file, lines) in fileTotalLines)
        {
            collector.SetTotalSourceLines(contractHash, file, lines.Count);

            if (fileExecutedLines.TryGetValue(file, out var executed))
            {
                foreach (var line in executed)
                {
                    collector.RecordSourceLine(contractHash, file, line);
                }
            }
        }
    }

    private static (string File, int Line)? ParseSourceCoverageKey(string key)
    {
        const string marker = "::line ";
        var idx = key.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var file = key[..idx].Trim();
        var rest = key[(idx + marker.Length)..];

        var colonIdx = rest.IndexOf(':');
        var lineStr = colonIdx >= 0 ? rest[..colonIdx].Trim() : rest.Trim();

        return int.TryParse(lineStr, out var line)
            ? (file, line)
            : null;
    }
}
