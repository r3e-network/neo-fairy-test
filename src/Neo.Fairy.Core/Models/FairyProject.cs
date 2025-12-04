// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Configuration;

namespace Neo.Fairy.Core.Models;

/// <summary>
/// Represents a Fairy project with all its contracts and configuration.
/// </summary>
public sealed class FairyProject
{
    /// <summary>
    /// Gets the project root directory.
    /// </summary>
    public required string RootDirectory { get; init; }

    /// <summary>
    /// Gets the project configuration.
    /// </summary>
    public required FairyConfig Config { get; init; }

    /// <summary>
    /// Gets the loaded contract artifacts.
    /// </summary>
    public IReadOnlyList<ContractArtifact> Artifacts { get; private set; } = Array.Empty<ContractArtifact>();

    /// <summary>
    /// Gets the source directory path.
    /// </summary>
    public string SourceDirectory => Path.Combine(RootDirectory, Config.Project.Src);

    /// <summary>
    /// Gets the test directory path.
    /// </summary>
    public string TestDirectory => Path.Combine(RootDirectory, Config.Project.Test);

    /// <summary>
    /// Gets the script directory path.
    /// </summary>
    public string ScriptDirectory => Path.Combine(RootDirectory, Config.Project.Script);

    /// <summary>
    /// Gets the output directory path.
    /// </summary>
    public string OutputDirectory => Path.Combine(RootDirectory, Config.Project.Out);

    /// <summary>
    /// Gets the configuration file path.
    /// </summary>
    public string ConfigPath => Path.Combine(RootDirectory, "fairy.toml");

    /// <summary>
    /// Loads a project from a directory.
    /// </summary>
    /// <param name="directory">The project directory (or any subdirectory).</param>
    /// <returns>The loaded project.</returns>
    public static FairyProject Load(string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();

        // Find project root by looking for fairy.toml
        var rootDir = FindProjectRoot(directory);
        if (rootDir == null)
        {
            throw new InvalidOperationException(
                $"Could not find fairy.toml in {directory} or any parent directory. " +
                "Run 'fairy init' to create a new project.");
        }

        var configPath = Path.Combine(rootDir, "fairy.toml");
        var config = FairyConfig.Load(configPath);

        return new FairyProject
        {
            RootDirectory = rootDir,
            Config = config
        };
    }

    /// <summary>
    /// Creates a new project in the specified directory.
    /// </summary>
    /// <param name="directory">The target directory.</param>
    /// <param name="projectName">The project name.</param>
    /// <returns>The created project.</returns>
    public static FairyProject Create(string directory, string projectName)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var config = FairyConfig.CreateDefault(projectName);
        var project = new FairyProject
        {
            RootDirectory = directory,
            Config = config
        };

        // Create directory structure
        Directory.CreateDirectory(project.SourceDirectory);
        Directory.CreateDirectory(project.TestDirectory);
        Directory.CreateDirectory(project.ScriptDirectory);
        Directory.CreateDirectory(project.OutputDirectory);

        // Save configuration
        config.Save(project.ConfigPath);

        return project;
    }

    /// <summary>
    /// Loads all compiled contract artifacts from the output directory.
    /// </summary>
    public async Task LoadArtifactsAsync()
    {
        var artifacts = new List<ContractArtifact>();

        foreach (var contractConfig in Config.Contracts)
        {
            var nefPath = Path.Combine(OutputDirectory, $"{contractConfig.Name}.nef");
            var manifestPath = Path.Combine(OutputDirectory, $"{contractConfig.Name}.manifest.json");
            var debugInfoPath = Path.Combine(OutputDirectory, $"{contractConfig.Name}.nefdbgnfo");

            if (!File.Exists(nefPath) || !File.Exists(manifestPath))
            {
                continue; // Skip uncompiled contracts
            }

            var artifact = await ContractArtifact.LoadFromFilesAsync(
                contractConfig.Alias,
                nefPath,
                manifestPath,
                File.Exists(debugInfoPath) ? debugInfoPath : null);

            // Set dependencies from config
            artifact = artifact with { Dependencies = contractConfig.Depends };

            artifacts.Add(artifact);
        }

        Artifacts = artifacts;
    }

    /// <summary>
    /// Gets artifacts in dependency order for deployment.
    /// </summary>
    public IReadOnlyList<ContractArtifact> GetArtifactsInDependencyOrder()
    {
        var result = new List<ContractArtifact>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Visit(ContractArtifact artifact)
        {
            if (visited.Contains(artifact.Alias)) return;

            if (visiting.Contains(artifact.Alias))
            {
                throw new InvalidOperationException(
                    $"Circular dependency detected involving contract '{artifact.Alias}'");
            }

            visiting.Add(artifact.Alias);

            foreach (var dep in artifact.Dependencies)
            {
                var depArtifact = Artifacts.FirstOrDefault(a =>
                    string.Equals(a.Alias, dep, StringComparison.OrdinalIgnoreCase));

                if (depArtifact != null)
                {
                    Visit(depArtifact);
                }
            }

            visiting.Remove(artifact.Alias);
            visited.Add(artifact.Alias);
            result.Add(artifact);
        }

        foreach (var artifact in Artifacts)
        {
            Visit(artifact);
        }

        return result;
    }

    /// <summary>
    /// Gets a contract artifact by alias.
    /// </summary>
    public ContractArtifact? GetArtifact(string alias)
    {
        return Artifacts.FirstOrDefault(a =>
            string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a contract configuration by alias with resolved paths.
    /// </summary>
    /// <param name="alias">The contract alias.</param>
    /// <returns>Contract info with resolved paths, or null if not found.</returns>
    public ContractPathInfo? GetContractByAlias(string alias)
    {
        var contractConfig = Config.Contracts.FirstOrDefault(c =>
            string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Name, alias, StringComparison.OrdinalIgnoreCase));

        if (contractConfig == null)
            return null;

        // Resolve paths - check output directory first, then source
        var nefPath = Path.Combine(OutputDirectory, $"{contractConfig.Name}.nef");
        var manifestPath = Path.Combine(OutputDirectory, $"{contractConfig.Name}.manifest.json");

        return new ContractPathInfo
        {
            Alias = contractConfig.Alias,
            Name = contractConfig.Name,
            SourcePath = Path.Combine(RootDirectory, contractConfig.Path),
            NefPath = nefPath,
            ManifestPath = manifestPath,
            DebugInfoPath = Path.Combine(OutputDirectory, $"{contractConfig.Name}.nefdbgnfo"),
            Dependencies = contractConfig.Depends
        };
    }

    /// <summary>
    /// Validates the project structure.
    /// </summary>
    public ProjectValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check directories exist
        if (!Directory.Exists(SourceDirectory))
            warnings.Add($"Source directory not found: {SourceDirectory}");

        if (!Directory.Exists(TestDirectory))
            warnings.Add($"Test directory not found: {TestDirectory}");

        // Check contracts have source files
        foreach (var contract in Config.Contracts)
        {
            var sourcePath = Path.Combine(RootDirectory, contract.Path);
            if (!File.Exists(sourcePath))
            {
                errors.Add($"Contract source not found: {contract.Path}");
            }
        }

        // Check for circular dependencies
        try
        {
            GetArtifactsInDependencyOrder();
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        return new ProjectValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private static string? FindProjectRoot(string startDirectory)
    {
        var dir = startDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "fairy.toml")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}

/// <summary>
/// Result of project validation.
/// </summary>
public sealed class ProjectValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// Contract path information with resolved file paths.
/// </summary>
public sealed class ContractPathInfo
{
    /// <summary>
    /// Gets the contract alias.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the contract name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the source file path.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    /// Gets the compiled NEF file path.
    /// </summary>
    public required string NefPath { get; init; }

    /// <summary>
    /// Gets the manifest file path.
    /// </summary>
    public required string ManifestPath { get; init; }

    /// <summary>
    /// Gets the debug info file path.
    /// </summary>
    public required string DebugInfoPath { get; init; }

    /// <summary>
    /// Gets the contract dependencies.
    /// </summary>
    public required IReadOnlyList<string> Dependencies { get; init; }

    /// <summary>
    /// Gets whether the compiled artifacts exist.
    /// </summary>
    public bool IsCompiled => File.Exists(NefPath) && File.Exists(ManifestPath);
}
