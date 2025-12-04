// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Diagnostics;
using Neo.Fairy.Core.CodeGen;
using Neo.Fairy.Core.Configuration;
using Neo.Fairy.Core.Models;

namespace Neo.Fairy.Cli.Services;

/// <summary>
/// Service for building/compiling smart contracts.
/// Extracted from BuildCommand for reuse in TestCommand.
/// </summary>
public sealed class BuildService
{
    /// <summary>
    /// Result of a contract build operation.
    /// </summary>
    public sealed record BuildResult
    {
        public required string ContractName { get; init; }
        public required bool Success { get; init; }
        public string? Error { get; init; }
        public long NefSize { get; init; }
        public string? NefPath { get; init; }
        public string? ManifestPath { get; init; }
    }

    /// <summary>
    /// Result of a full project build.
    /// </summary>
    public sealed record ProjectBuildResult
    {
        public required IReadOnlyList<BuildResult> Results { get; init; }
        public required TimeSpan Duration { get; init; }
        public int SuccessCount => Results.Count(r => r.Success);
        public int FailCount => Results.Count(r => !r.Success);
        public bool AllSucceeded => FailCount == 0;
    }

    /// <summary>
    /// Builds all contracts in a project.
    /// </summary>
    public async Task<ProjectBuildResult> BuildProjectAsync(
        FairyProject project,
        bool debug = true,
        bool optimize = false,
        IProgress<(string Contract, int Current, int Total)>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<BuildResult>();
        var contracts = project.Config.Contracts;

        for (int i = 0; i < contracts.Count; i++)
        {
            var contract = contracts[i];
            progress?.Report((contract.Name, i + 1, contracts.Count));

            var result = await BuildContractAsync(project, contract, debug, optimize);
            results.Add(result);
        }

        stopwatch.Stop();

        return new ProjectBuildResult
        {
            Results = results,
            Duration = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Builds a single contract.
    /// </summary>
    public async Task<BuildResult> BuildContractAsync(
        FairyProject project,
        ContractConfig contract,
        bool debug = true,
        bool optimize = false)
    {
        var sourcePath = Path.Combine(project.RootDirectory, contract.Path);

        if (!File.Exists(sourcePath))
        {
            return new BuildResult
            {
                ContractName = contract.Name,
                Success = false,
                Error = $"Source file not found: {contract.Path}"
            };
        }

        var compiler = project.Config.Compiler.Path;
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
                return new BuildResult
                {
                    ContractName = contract.Name,
                    Success = false,
                    Error = $"Failed to start compiler: {compiler}"
                };
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                return new BuildResult
                {
                    ContractName = contract.Name,
                    Success = false,
                    Error = error.Trim()
                };
            }

            // Check output files
            var nefPath = Path.Combine(outputDir, $"{contract.Name}.nef");
            var manifestPath = Path.Combine(outputDir, $"{contract.Name}.manifest.json");

            if (File.Exists(nefPath))
            {
                var size = new FileInfo(nefPath).Length;
                return new BuildResult
                {
                    ContractName = contract.Name,
                    Success = true,
                    NefSize = size,
                    NefPath = nefPath,
                    ManifestPath = File.Exists(manifestPath) ? manifestPath : null
                };
            }

            return new BuildResult
            {
                ContractName = contract.Name,
                Success = false,
                Error = "NEF file not generated"
            };
        }
        catch (Exception ex)
        {
            return new BuildResult
            {
                ContractName = contract.Name,
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Checks if contracts need to be rebuilt.
    /// </summary>
    public bool NeedsBuild(FairyProject project)
    {
        foreach (var contract in project.Config.Contracts)
        {
            var sourcePath = Path.Combine(project.RootDirectory, contract.Path);
            var nefPath = Path.Combine(project.OutputDirectory, $"{contract.Name}.nef");

            if (!File.Exists(nefPath))
                return true;

            if (!File.Exists(sourcePath))
                continue;

            var sourceTime = File.GetLastWriteTimeUtc(sourcePath);
            var nefTime = File.GetLastWriteTimeUtc(nefPath);

            if (sourceTime > nefTime)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds test projects using dotnet build.
    /// </summary>
    public async Task<BuildResult> BuildTestProjectAsync(FairyProject project)
    {
        var testDir = project.TestDirectory;

        if (!Directory.Exists(testDir))
        {
            return new BuildResult
            {
                ContractName = "Tests",
                Success = false,
                Error = $"Test directory not found: {testDir}"
            };
        }

        // Find test project file
        var csprojFiles = Directory.GetFiles(testDir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0)
        {
            // Try to find in parent or subdirectories
            csprojFiles = Directory.GetFiles(project.RootDirectory, "*.Test*.csproj", SearchOption.AllDirectories);
        }

        if (csprojFiles.Length == 0)
        {
            return new BuildResult
            {
                ContractName = "Tests",
                Success = false,
                Error = "No test project (.csproj) found"
            };
        }

        var testProject = csprojFiles[0];

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{testProject}\" -c Debug",
                WorkingDirectory = project.RootDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return new BuildResult
                {
                    ContractName = "Tests",
                    Success = false,
                    Error = "Failed to start dotnet build"
                };
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                return new BuildResult
                {
                    ContractName = "Tests",
                    Success = false,
                    Error = error.Trim()
                };
            }

            // Find output assembly
            var projectName = Path.GetFileNameWithoutExtension(testProject);
            var outputPath = Path.Combine(
                Path.GetDirectoryName(testProject)!,
                "bin", "Debug", "net10.0", $"{projectName}.dll");

            return new BuildResult
            {
                ContractName = "Tests",
                Success = true,
                NefPath = File.Exists(outputPath) ? outputPath : null
            };
        }
        catch (Exception ex)
        {
            return new BuildResult
            {
                ContractName = "Tests",
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Generates contract interface code for all compiled contracts.
    /// </summary>
    /// <param name="project">The project to generate interfaces for.</param>
    /// <param name="outputDirectory">Optional output directory. Defaults to project's Generated folder.</param>
    /// <returns>List of generated files.</returns>
    public async Task<List<InterfaceGenerationResult>> GenerateContractInterfacesAsync(
        FairyProject project,
        string? outputDirectory = null)
    {
        var results = new List<InterfaceGenerationResult>();
        var outDir = outputDirectory ?? Path.Combine(project.RootDirectory, "Generated");

        Directory.CreateDirectory(outDir);

        var generator = new ContractInterfaceGenerator(new GeneratorOptions
        {
            Namespace = $"{project.Config.Project.Name}.Contracts"
        });

        foreach (var contract in project.Config.Contracts)
        {
            var manifestPath = Path.Combine(project.OutputDirectory, $"{contract.Name}.manifest.json");

            if (!File.Exists(manifestPath))
            {
                results.Add(new InterfaceGenerationResult
                {
                    ContractName = contract.Name,
                    Success = false,
                    Error = "Manifest file not found. Build the contract first."
                });
                continue;
            }

            try
            {
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var generated = generator.Generate(manifestJson, contract.Alias);

                var outputPath = Path.Combine(outDir, generated.FileName);
                await File.WriteAllTextAsync(outputPath, generated.Content);

                results.Add(new InterfaceGenerationResult
                {
                    ContractName = contract.Name,
                    Success = true,
                    GeneratedFile = outputPath,
                    ClassName = generated.ClassName
                });
            }
            catch (Exception ex)
            {
                results.Add(new InterfaceGenerationResult
                {
                    ContractName = contract.Name,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Generates a single contract interface from manifest.
    /// </summary>
    public GeneratedCode GenerateContractInterface(string manifestJson, string? alias = null, string? namespaceName = null)
    {
        var generator = new ContractInterfaceGenerator(new GeneratorOptions
        {
            Namespace = namespaceName ?? "Neo.Fairy.Generated"
        });

        return generator.Generate(manifestJson, alias);
    }
}

/// <summary>
/// Result of interface generation for a contract.
/// </summary>
public sealed record InterfaceGenerationResult
{
    public required string ContractName { get; init; }
    public required bool Success { get; init; }
    public string? GeneratedFile { get; init; }
    public string? ClassName { get; init; }
    public string? Error { get; init; }
}
