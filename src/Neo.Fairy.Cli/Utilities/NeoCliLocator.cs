// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Cli.Utilities;

internal static class NeoCliLocator
{
    public static (string? NeoRoot, string? NeoCliPath) ResolveNeoCli(
        string? explicitNeoRoot,
        string? explicitNeoCli,
        string targetFramework,
        string? startDirectory = null)
    {
        string? resolvedNeoCli = null;
        if (!string.IsNullOrWhiteSpace(explicitNeoCli))
        {
            var full = Path.GetFullPath(explicitNeoCli);
            if (Directory.Exists(full))
            {
                var dllInDir = Path.Combine(full, "neo-cli.dll");
                if (File.Exists(dllInDir))
                    full = dllInDir;
            }

            if (File.Exists(full))
                resolvedNeoCli = full;
        }

        var resolvedNeoRoot = ResolveNeoRoot(explicitNeoRoot, startDirectory);

        if (resolvedNeoCli == null && resolvedNeoRoot != null)
        {
            resolvedNeoCli = ResolveNeoCliPath(resolvedNeoRoot, explicitNeoCli, targetFramework);
        }

        if (resolvedNeoRoot == null && resolvedNeoCli != null)
        {
            resolvedNeoRoot = TryInferNeoRootFromNeoCliPath(resolvedNeoCli);
        }

        return (resolvedNeoRoot, resolvedNeoCli);
    }

    public static string? TryInferNeoRootFromNeoCliPath(string neoCliPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(neoCliPath));
            if (string.IsNullOrWhiteSpace(dir))
                return null;

            var current = dir;
            for (var i = 0; i < 10; i++)
            {
                if (IsNeoRoot(current))
                    return current;

                var parent = Directory.GetParent(current);
                if (parent == null)
                    break;
                current = parent.FullName;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveNeoRoot(string? explicitNeoRoot, string? startDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitNeoRoot))
        {
            var full = Path.GetFullPath(explicitNeoRoot);
            return IsNeoRoot(full) ? full : null;
        }

        var env = Environment.GetEnvironmentVariable("NEOROOT")
                  ?? Environment.GetEnvironmentVariable("NeoRoot");

        if (!string.IsNullOrWhiteSpace(env))
        {
            var full = Path.GetFullPath(env);
            if (IsNeoRoot(full)) return full;
        }

        startDirectory ??= Directory.GetCurrentDirectory();

        var direct = Path.GetFullPath(Path.Combine(startDirectory, "..", "neo"));
        if (IsNeoRoot(direct)) return direct;

        var dir = startDirectory;
        while (true)
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;

            var sibling = Path.Combine(parent.FullName, "neo");
            if (IsNeoRoot(sibling)) return sibling;

            dir = parent.FullName;
        }

        return null;
    }

    public static bool IsNeoRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Source checkout (the most reliable marker).
        if (File.Exists(Path.Combine(path, "src", "Neo", "Neo.csproj")))
            return true;

        if (File.Exists(Path.Combine(path, "core", "src", "Neo", "Neo.csproj")) &&
            File.Exists(Path.Combine(path, "node", "src", "Neo.ConsoleService", "Neo.ConsoleService.csproj")))
            return true;

        // Binary-only layouts (e.g., extracted Neo.CLI artifacts).
        var knownTfms = new[] { "net10.0", "net9.0", "net8.0", "net7.0", "net6.0" };
        foreach (var tfm in knownTfms)
        {
            if (File.Exists(Path.Combine(path, "bin", "Neo.CLI", tfm, "neo-cli.dll")))
                return true;

            if (File.Exists(Path.Combine(path, "neo-cli", "bin", "Release", tfm, "neo-cli.dll")))
                return true;

            if (File.Exists(Path.Combine(path, "neo-cli", "bin", "Debug", tfm, "neo-cli.dll")))
                return true;

            if (File.Exists(Path.Combine(path, "node", "bin", "Neo.CLI", tfm, "neo-cli.dll")))
                return true;

            if (File.Exists(Path.Combine(path, "node", "src", "Neo.CLI", "bin", "Release", tfm, "neo-cli.dll")))
                return true;

            if (File.Exists(Path.Combine(path, "node", "src", "Neo.CLI", "bin", "Debug", tfm, "neo-cli.dll")))
                return true;
        }

        return false;
    }

    public static string? ResolveNeoCliPath(string neoRoot, string? explicitNeoCli, string targetFramework)
    {
        if (!string.IsNullOrWhiteSpace(explicitNeoCli))
        {
            var full = Path.GetFullPath(explicitNeoCli);
            if (Directory.Exists(full))
            {
                var dllInDir = Path.Combine(full, "neo-cli.dll");
                if (File.Exists(dllInDir))
                    return dllInDir;
            }

            if (File.Exists(full))
                return full;
        }

        var candidates = new[]
        {
            Path.Combine(neoRoot, "bin", "Neo.CLI", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "neo-cli", "bin", "Release", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "neo-cli", "bin", "Debug", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "node", "bin", "Neo.CLI", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "node", "src", "Neo.CLI", "bin", "Release", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "node", "src", "Neo.CLI", "bin", "Debug", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "src", "Neo.CLI", "bin", "Release", targetFramework, "neo-cli.dll"),
            Path.Combine(neoRoot, "src", "Neo.CLI", "bin", "Debug", targetFramework, "neo-cli.dll")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public static string? ResolveNeoCliConfigPath(string neoCliDirectory, string network)
    {
        if (network.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var direct = Path.IsPathRooted(network)
                ? network
                : Path.Combine(neoCliDirectory, network);

            if (File.Exists(direct))
                return Path.GetFullPath(direct);
        }

        var configFile = network.ToLowerInvariant() switch
        {
            "mainnet" => "config.mainnet.json",
            "testnet" => "config.testnet.json",
            "private" => "config.json",
            "privatenet" => "config.json",
            _ => network
        };

        var resolved = Path.Combine(neoCliDirectory, configFile);
        return File.Exists(resolved) ? resolved : null;
    }

    public static string? FindFairyRepoRoot(string? startDirectory = null)
    {
        var dir = startDirectory ?? Directory.GetCurrentDirectory();
        while (true)
        {
            var candidate = Path.Combine(dir, "src", "Fairy.Plugin", "Fairy.csproj");
            if (File.Exists(candidate))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;
            dir = parent.FullName;
        }

        return null;
    }
}
