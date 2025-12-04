// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Tomlyn;
using Tomlyn.Model;

namespace Neo.Fairy.Core.Configuration;

/// <summary>
/// Represents the fairy.toml project configuration.
/// Provides Foundry-style project configuration for Neo smart contract development.
/// </summary>
public sealed class FairyConfig
{
    /// <summary>
    /// Gets or sets the project configuration.
    /// </summary>
    public ProjectConfig Project { get; set; } = new();

    /// <summary>
    /// Gets or sets the compiler configuration.
    /// </summary>
    public CompilerConfig Compiler { get; set; } = new();

    /// <summary>
    /// Gets or sets the Fairy runtime configuration.
    /// </summary>
    public FairyRuntimeConfig Fairy { get; set; } = new();

    /// <summary>
    /// Gets or sets the deployment configuration.
    /// </summary>
    public DeployConfig Deploy { get; set; } = new();

    /// <summary>
    /// Gets or sets the test configuration.
    /// </summary>
    public TestConfig Test { get; set; } = new();

    /// <summary>
    /// Gets or sets the contract definitions.
    /// </summary>
    public List<ContractConfig> Contracts { get; set; } = new();

    /// <summary>
    /// Loads configuration from a fairy.toml file.
    /// </summary>
    /// <param name="path">Path to the fairy.toml file.</param>
    /// <returns>The parsed configuration.</returns>
    public static FairyConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Configuration file not found: {path}");
        }

        var toml = File.ReadAllText(path);
        return Parse(toml);
    }

    /// <summary>
    /// Loads configuration from the current directory or parent directories.
    /// </summary>
    /// <returns>The parsed configuration, or default if not found.</returns>
    public static FairyConfig? LoadFromCurrentDirectory()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var configPath = Path.Combine(dir, "fairy.toml");
            if (File.Exists(configPath))
            {
                return Load(configPath);
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    /// <summary>
    /// Parses configuration from TOML string.
    /// </summary>
    public static FairyConfig Parse(string toml)
    {
        var model = Toml.ToModel(toml);
        var config = new FairyConfig();

        if (model.TryGetValue("project", out var projectObj) && projectObj is TomlTable project)
        {
            config.Project = ParseProjectConfig(project);
        }

        if (model.TryGetValue("compiler", out var compilerObj) && compilerObj is TomlTable compiler)
        {
            config.Compiler = ParseCompilerConfig(compiler);
        }

        if (model.TryGetValue("fairy", out var fairyObj) && fairyObj is TomlTable fairy)
        {
            config.Fairy = ParseFairyConfig(fairy);
        }

        if (model.TryGetValue("deploy", out var deployObj) && deployObj is TomlTable deploy)
        {
            config.Deploy = ParseDeployConfig(deploy);
        }

        if (model.TryGetValue("test", out var testObj) && testObj is TomlTable test)
        {
            config.Test = ParseTestConfig(test);
        }

        if (model.TryGetValue("contracts", out var contractsObj) && contractsObj is TomlTableArray contracts)
        {
            foreach (TomlTable contract in contracts)
            {
                config.Contracts.Add(ParseContractConfig(contract));
            }
        }

        return config;
    }

    /// <summary>
    /// Saves configuration to a fairy.toml file.
    /// </summary>
    public void Save(string path)
    {
        var toml = ToToml();
        File.WriteAllText(path, toml);
    }

    /// <summary>
    /// Converts configuration to TOML string.
    /// </summary>
    public string ToToml()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("[project]");
        sb.AppendLine($"name = \"{Project.Name}\"");
        sb.AppendLine($"version = \"{Project.Version}\"");
        sb.AppendLine($"src = \"{Project.Src}\"");
        sb.AppendLine($"test = \"{Project.Test}\"");
        sb.AppendLine($"script = \"{Project.Script}\"");
        sb.AppendLine($"out = \"{Project.Out}\"");
        sb.AppendLine();

        sb.AppendLine("[compiler]");
        sb.AppendLine($"path = \"{Compiler.Path}\"");
        sb.AppendLine($"debug = {Compiler.Debug.ToString().ToLower()}");
        sb.AppendLine($"assembly = {Compiler.Assembly.ToString().ToLower()}");
        sb.AppendLine($"optimize = {Compiler.Optimize.ToString().ToLower()}");
        sb.AppendLine();

        sb.AppendLine("[fairy]");
        sb.AppendLine($"rpc_url = \"{Fairy.RpcUrl}\"");
        sb.AppendLine($"network = \"{Fairy.Network}\"");
        sb.AppendLine($"gas_limit = {Fairy.GasLimit}");
        sb.AppendLine($"session_timeout = {Fairy.SessionTimeout}");
        sb.AppendLine();

        sb.AppendLine("[deploy]");
        sb.AppendLine($"default_wallet = \"{Deploy.DefaultWallet}\"");
        sb.AppendLine($"verify = {Deploy.Verify.ToString().ToLower()}");
        sb.AppendLine();

        sb.AppendLine("[test]");
        sb.AppendLine($"verbosity = {Test.Verbosity}");
        sb.AppendLine($"coverage = {Test.Coverage.ToString().ToLower()}");
        sb.AppendLine($"parallel = {Test.Parallel.ToString().ToLower()}");
        sb.AppendLine($"fail_fast = {Test.FailFast.ToString().ToLower()}");
        sb.AppendLine($"fuzz_runs = {Test.FuzzRuns}");
        sb.AppendLine();

        foreach (var contract in Contracts)
        {
            sb.AppendLine("[[contracts]]");
            sb.AppendLine($"name = \"{contract.Name}\"");
            sb.AppendLine($"path = \"{contract.Path}\"");
            sb.AppendLine($"alias = \"{contract.Alias}\"");
            if (contract.Depends.Count > 0)
            {
                sb.AppendLine($"depends = [{string.Join(", ", contract.Depends.Select(d => $"\"{d}\""))}]");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a default configuration for a new project.
    /// </summary>
    public static FairyConfig CreateDefault(string projectName)
    {
        return new FairyConfig
        {
            Project = new ProjectConfig { Name = projectName },
            Contracts = new List<ContractConfig>
            {
                new ContractConfig
                {
                    Name = "Counter",
                    Path = "src/Counter.cs",
                    Alias = "counter"
                }
            }
        };
    }

    private static ProjectConfig ParseProjectConfig(TomlTable table)
    {
        return new ProjectConfig
        {
            Name = table.GetString("name") ?? "unnamed",
            Version = table.GetString("version") ?? "1.0.0",
            Src = table.GetString("src") ?? "src",
            Test = table.GetString("test") ?? "test",
            Script = table.GetString("script") ?? "script",
            Out = table.GetString("out") ?? "out"
        };
    }

    private static CompilerConfig ParseCompilerConfig(TomlTable table)
    {
        return new CompilerConfig
        {
            Path = table.GetString("path") ?? "nccs",
            Debug = table.GetBool("debug") ?? true,
            Assembly = table.GetBool("assembly") ?? true,
            Optimize = table.GetBool("optimize") ?? false
        };
    }

    private static FairyRuntimeConfig ParseFairyConfig(TomlTable table)
    {
        return new FairyRuntimeConfig
        {
            RpcUrl = table.GetString("rpc_url") ?? "http://localhost:16868",
            Network = table.GetString("network") ?? "mainnet",
            GasLimit = (int)(table.GetLong("gas_limit") ?? 200),
            SessionTimeout = (int)(table.GetLong("session_timeout") ?? 86400)
        };
    }

    private static DeployConfig ParseDeployConfig(TomlTable table)
    {
        return new DeployConfig
        {
            DefaultWallet = table.GetString("default_wallet") ?? "fairy.json",
            Verify = table.GetBool("verify") ?? true
        };
    }

    private static TestConfig ParseTestConfig(TomlTable table)
    {
        return new TestConfig
        {
            Verbosity = (int)(table.GetLong("verbosity") ?? 2),
            Coverage = table.GetBool("coverage") ?? true,
            Parallel = table.GetBool("parallel") ?? true,
            FailFast = table.GetBool("fail_fast") ?? false,
            FuzzRuns = (int)(table.GetLong("fuzz_runs") ?? 256)
        };
    }

    private static ContractConfig ParseContractConfig(TomlTable table)
    {
        var config = new ContractConfig
        {
            Name = table.GetString("name") ?? "",
            Path = table.GetString("path") ?? "",
            Alias = table.GetString("alias") ?? ""
        };

        if (table.TryGetValue("depends", out var dependsObj) && dependsObj is TomlArray depends)
        {
            config.Depends = depends.Select(d => d?.ToString() ?? "").Where(d => !string.IsNullOrEmpty(d)).ToList();
        }

        return config;
    }
}

/// <summary>
/// Project configuration section.
/// </summary>
public sealed class ProjectConfig
{
    public string Name { get; set; } = "unnamed";
    public string Version { get; set; } = "1.0.0";
    public string Src { get; set; } = "src";
    public string Test { get; set; } = "test";
    public string Script { get; set; } = "script";
    public string Out { get; set; } = "out";
}

/// <summary>
/// Compiler configuration section.
/// </summary>
public sealed class CompilerConfig
{
    public string Path { get; set; } = "nccs";
    public bool Debug { get; set; } = true;
    public bool Assembly { get; set; } = true;
    public bool Optimize { get; set; } = false;
}

/// <summary>
/// Fairy runtime configuration section.
/// </summary>
public sealed class FairyRuntimeConfig
{
    public string RpcUrl { get; set; } = "http://localhost:16868";
    public string Network { get; set; } = "mainnet";
    public int GasLimit { get; set; } = 200;
    public int SessionTimeout { get; set; } = 86400;
}

/// <summary>
/// Deployment configuration section.
/// </summary>
public sealed class DeployConfig
{
    public string DefaultWallet { get; set; } = "fairy.json";
    public bool Verify { get; set; } = true;
}

/// <summary>
/// Test configuration section.
/// </summary>
public sealed class TestConfig
{
    public int Verbosity { get; set; } = 2;
    public bool Coverage { get; set; } = true;
    public bool Parallel { get; set; } = true;
    public bool FailFast { get; set; } = false;
    public int FuzzRuns { get; set; } = 256;
}

/// <summary>
/// Contract definition configuration.
/// </summary>
public sealed class ContractConfig
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Alias { get; set; } = "";
    public List<string> Depends { get; set; } = new();
}

/// <summary>
/// Extension methods for TOML table parsing.
/// </summary>
internal static class TomlTableExtensions
{
    public static string? GetString(this TomlTable table, string key)
    {
        return table.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    public static bool? GetBool(this TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is bool b)
            return b;
        return null;
    }

    public static long? GetLong(this TomlTable table, string key)
    {
        if (table.TryGetValue(key, out var value) && value is long l)
            return l;
        return null;
    }
}
