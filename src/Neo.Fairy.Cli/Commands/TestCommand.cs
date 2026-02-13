// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neo.Fairy.Cli.Services;
using Neo.Fairy.Cli.Utilities;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Testing.Coverage;
using Neo.Fairy.Testing.Runner;
using Spectre.Console;

namespace Neo.Fairy.Cli.Commands;

/// <summary>
/// Command to run tests.
/// Similar to 'forge test' in Foundry.
/// </summary>
public static class TestCommand
{
    public static Command Create()
    {
        var matchOption = new Option<string?>(
            aliases: new[] { "--match", "-m" },
            description: "Only run tests matching the pattern");

        var matchContractOption = new Option<string?>(
            name: "--match-contract",
            description: "Only run tests in contracts/classes matching the pattern");

        var verbosityOption = new Option<int>(
            aliases: new[] { "--verbosity", "-V" },
            description: "Verbosity level (0-4). Default from fairy.toml if omitted.",
            getDefaultValue: () => -1);

        var gasReportOption = new Option<bool>(
            name: "--gas-report",
            description: "Print gas usage report");

        var coverageOption = new Option<bool>(
            name: "--coverage",
            description: "Collect code coverage (if supported by node)");

        var coverageOutOption = new Option<string?>(
            name: "--coverage-out",
            description: "Write coverage reports to a directory (defaults to <out>/coverage when enabled)");

        var fuzzRunsOption = new Option<int>(
            name: "--fuzz-runs",
            description: "Number of fuzz test runs. Default from fairy.toml if omitted.",
            getDefaultValue: () => -1);

        var failFastOption = new Option<bool>(
            name: "--fail-fast",
            description: "Stop on first test failure");

        var noBuildOption = new Option<bool>(
            name: "--no-build",
            description: "Skip automatic build before running tests");

        var rpcOption = new Option<string?>(
            aliases: new[] { "--rpc-url", "-r" },
            description: "Fairy RPC endpoint URL (defaults to FAIRY_RPC_URL, fairy.toml, or http://localhost:16868)");

        var command = new Command("test", "Run tests")
        {
            matchOption,
            matchContractOption,
            verbosityOption,
            gasReportOption,
            coverageOption,
            coverageOutOption,
            fuzzRunsOption,
            failFastOption,
            noBuildOption,
            rpcOption
        };

        command.SetHandler(async (InvocationContext ctx) =>
        {
            ctx.ExitCode = await ExecuteAsync(
                ctx.ParseResult.GetValueForOption(matchOption),
                ctx.ParseResult.GetValueForOption(matchContractOption),
                ctx.ParseResult.GetValueForOption(verbosityOption),
                ctx.ParseResult.GetValueForOption(gasReportOption),
                ctx.ParseResult.GetValueForOption(coverageOption),
                ctx.ParseResult.GetValueForOption(coverageOutOption),
                ctx.ParseResult.GetValueForOption(fuzzRunsOption),
                ctx.ParseResult.GetValueForOption(failFastOption),
                ctx.ParseResult.GetValueForOption(noBuildOption),
                ctx.ParseResult.GetValueForOption(rpcOption));
        });

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string? match,
        string? matchContract,
        int verbosity,
        bool gasReport,
        bool coverage,
        string? coverageOut,
        int fuzzRuns,
        bool failFast,
        bool noBuild,
        string? rpcUrl)
    {
        FairyProject project;
        try
        {
            project = FairyProject.Load();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
            return 1;
        }

        var verbosityLevel = verbosity >= 0 ? verbosity : project.Config.Test.Verbosity;
        var fuzzRunsValue = fuzzRuns > 0 ? fuzzRuns : project.Config.Test.FuzzRuns;
        var collectCoverage = coverage || project.Config.Test.Coverage;
        var parallel = project.Config.Test.Parallel;
        var failFastValue = failFast || project.Config.Test.FailFast;
        var resolvedRpcUrl = RpcUrlResolver.Resolve(rpcUrl, project);

        if (collectCoverage)
        {
            CoverageRegistry.Clear();
        }

        // Auto-build if needed
        var buildService = new BuildService();
        if (!noBuild)
        {
            // Build contracts if needed
            if (buildService.NeedsBuild(project))
            {
                AnsiConsole.MarkupLine("[blue]Building contracts...[/]");
                var buildResult = await buildService.BuildProjectAsync(project);

                if (!buildResult.AllSucceeded)
                {
                    AnsiConsole.MarkupLine("[red]Build failed:[/]");
                    foreach (var result in buildResult.Results.Where(r => !r.Success))
                    {
                        AnsiConsole.MarkupLine($"  [red]✗[/] {result.ContractName}: {result.Error}");
                    }
                    return 1;
                }

                AnsiConsole.MarkupLine($"[green]✓ Contracts built in {buildResult.Duration.TotalSeconds:F1}s[/]");
                AnsiConsole.WriteLine();
            }

            // Build test project if a csproj exists
            var testBuildResult = await buildService.BuildTestProjectAsync(project);
            if (!testBuildResult.Success && !string.IsNullOrWhiteSpace(testBuildResult.Error))
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Test build skipped:[/] {testBuildResult.Error}");
            }
        }

        // Discover test files
        var testDir = project.TestDirectory;
        if (!Directory.Exists(testDir))
        {
            AnsiConsole.MarkupLine($"[yellow]No test directory found at {testDir}[/]");
            return 0;
        }

        var testFiles = Directory.GetFiles(testDir, "*.Test.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(testDir, "*Test.cs", SearchOption.AllDirectories))
            .Distinct()
            .ToList();

        if (testFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No test files found[/]");
            return 0;
        }

        if (!string.IsNullOrEmpty(matchContract))
        {
            testFiles = testFiles
                .Where(f => Path.GetFileName(f).Contains(matchContract, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        AnsiConsole.MarkupLine($"[green]Running tests ({testFiles.Count} file(s))...[/]");
        AnsiConsole.WriteLine();

        Assembly testAssembly;
        var assemblyPath = TryFindBuiltTestAssembly(project, testFiles);
        if (assemblyPath != null)
        {
            AnsiConsole.MarkupLine($"[grey]Loading compiled tests:[/] {assemblyPath}");
            testAssembly = Assembly.LoadFrom(assemblyPath);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Compiling tests in-memory...[/]");
            testAssembly = CompileTestsInMemory(testFiles, project.Config.Project.Name);
        }

        var runnerOptions = new TestRunnerOptions
        {
            FailFast = failFastValue,
            FuzzRuns = fuzzRunsValue,
            Verbosity = verbosityLevel,
            Parallel = parallel,
            CollectCoverage = collectCoverage,
            OnTestCompleted = r => PrintTestResult(r, verbosityLevel)
        };

        var runner = new TestRunner(resolvedRpcUrl, runnerOptions);

        var stopwatch = Stopwatch.StartNew();
        var summary = await runner.RunWithFilterAsync(testAssembly, matchContract, match);
        stopwatch.Stop();

        PrintSummary(summary.Results.ToList(), stopwatch.Elapsed, collectCoverage, gasReport);

        if (collectCoverage)
        {
            var contracts = CoverageRegistry.Contracts;
            if (contracts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Coverage enabled but no contracts were deployed during tests.[/]");
                return summary.AllPassed ? 0 : 1;
            }

            await CoverageCliHelper.PrintCoverageAsync(project, contracts, coverageOut, resolvedRpcUrl);
        }

        return summary.AllPassed ? 0 : 1;
    }

    private static string? TryFindBuiltTestAssembly(FairyProject project, IReadOnlyList<string> testFiles)
    {
        // If user has a csproj and built tests, prefer its output.
        var testDir = project.TestDirectory;
        var csprojFiles = Directory.GetFiles(testDir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0)
        {
            csprojFiles = Directory.GetFiles(project.RootDirectory, "*.Test*.csproj", SearchOption.AllDirectories);
        }

        if (csprojFiles.Length == 0)
        {
            return null;
        }

        var testProject = csprojFiles[0];
        var projectName = Path.GetFileNameWithoutExtension(testProject);
        var tfms = new[] { "net10.0", "net9.0", "net8.0" };
        foreach (var tfm in tfms)
        {
            var outputDir = Path.Combine(Path.GetDirectoryName(testProject)!, "bin", "Debug", tfm);
            var dllPath = Path.Combine(outputDir, $"{projectName}.dll");
            if (File.Exists(dllPath)) return dllPath;
        }
        return null;
    }

    private static Assembly CompileTestsInMemory(IReadOnlyList<string> testFiles, string projectName)
    {
        var syntaxTrees = testFiles.Select(file =>
        {
            var code = File.ReadAllText(file);
            return CSharpSyntaxTree.ParseText(code, path: file);
        }).ToList();

        var references = CollectMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: $"{projectName}.Tests",
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

            AnsiConsole.MarkupLine("[red]Test compilation failed:[/]");
            foreach (var diag in diagnostics)
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {diag.EscapeMarkup()}");
            }

            throw new InvalidOperationException("Failed to compile tests. Fix errors and retry.");
        }

        peStream.Position = 0;
        return Assembly.Load(peStream.ToArray());
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
            typeof(Neo.Fairy.Testing.FairyTest).Assembly,
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

    private static void PrintTestResult(TestResult result, int verbosity)
    {
        var icon = result.Passed ? "[green]✓[/]" : "[red]✗[/]";
        var duration = $"{result.Duration.TotalMilliseconds:F0}ms";
        var gas = $"{result.GasConsumed / 100000000.0:F1} GAS";

        if (result.FuzzStats != null)
        {
            AnsiConsole.MarkupLine($"    {icon} {result.TestName} (runs: {result.FuzzStats.Runs}, μ: {gas})");
        }
        else
        {
            AnsiConsole.MarkupLine($"    {icon} {result.TestName} ({duration}, {gas})");
        }

        if (result.Failed && verbosity >= 2)
        {
            AnsiConsole.MarkupLine($"      [red]│ {result.FailureMessage?.EscapeMarkup()}[/]");
            if (result.Expected != null)
            {
                AnsiConsole.MarkupLine($"      [red]│ Expected: {result.Expected.EscapeMarkup()}[/]");
            }
            if (result.Actual != null)
            {
                AnsiConsole.MarkupLine($"      [red]│ Actual: {result.Actual.EscapeMarkup()}[/]");
            }
            if (verbosity >= 3 && result.StackTrace != null)
            {
                AnsiConsole.MarkupLine($"      [grey]│ {result.StackTrace.EscapeMarkup()}[/]");
            }
        }
    }

    private static void PrintSummary(
        List<TestResult> results,
        TimeSpan duration,
        bool coverage,
        bool gasReport)
    {
        var passed = results.Count(r => r.Passed);
        var failed = results.Count(r => r.Failed);
        var skipped = results.Count(r => r.Status == TestStatus.Skipped);
        var total = results.Count;
        var passRate = total > 0 ? (double)passed / total * 100 : 0;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[grey]Test Summary[/]").LeftJustified());

        if (failed == 0)
        {
            AnsiConsole.MarkupLine($"[green]Test Summary: {passed} passed[/] ({passRate:F1}%)");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Test Summary: {passed} passed, {failed} failed[/] ({passRate:F1}%)");
        }

        if (skipped > 0)
        {
            AnsiConsole.MarkupLine($"[grey]Skipped: {skipped}[/]");
        }

        if (coverage)
        {
            AnsiConsole.MarkupLine("[grey]Coverage enabled.[/]");
        }

        if (gasReport)
        {
            var totalGas = results.Sum(r => r.GasConsumed);
            var avgGas = results.Count > 0 ? totalGas / results.Count : 0;
            AnsiConsole.MarkupLine($"[grey]Total GAS: {totalGas / 100000000.0:F2}, Average: {avgGas / 100000000.0:F2}[/]");
        }

        AnsiConsole.MarkupLine($"[grey]Total time: {duration.TotalSeconds:F1}s[/]");
    }

}
