// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Neo.Fairy.Cli.Utilities;

internal static class FairyPluginInstaller
{
    public static async Task<(bool Success, string? Error)> BuildFromSourceAsync(
        string fairyRepoRoot,
        string neoRoot,
        string configuration)
    {
        var csproj = Path.Combine(fairyRepoRoot, "src", "Fairy.Plugin", "Fairy.csproj");
        if (!File.Exists(csproj))
            return (false, $"Fairy plugin project not found: {csproj}");

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{csproj}\" -c \"{configuration}\" --nologo -p:NeoRoot=\"{neoRoot}\"",
            WorkingDirectory = fairyRepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            return (false, "Failed to start dotnet process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
            return (true, null);

        var error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        return (false, error.Trim());
    }

    public static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
    }

    public static uint? TryReadNetworkMagic(string neoCliConfigPath)
    {
        try
        {
            var config = JsonNode.Parse(File.ReadAllText(neoCliConfigPath));
            var protocol = config?["ProtocolConfiguration"] as JsonObject;
            var network = protocol?["Network"];
            if (network == null)
                return null;

            if (network is JsonValue value)
            {
                if (value.TryGetValue<uint>(out var magic))
                    return magic;

                if (uint.TryParse(value.ToString(), out magic))
                    return magic;
            }

            if (uint.TryParse(network.ToString(), out var parsed))
                return parsed;

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static (bool Success, string? Error) PatchRpcServerConfig(
        string rpcServerJsonPath,
        string host,
        int port,
        uint? networkMagic)
    {
        try
        {
            var parsed = JsonNode.Parse(
                File.ReadAllText(rpcServerJsonPath),
                nodeOptions: null,
                documentOptions: new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            if (parsed is not JsonObject root)
                return (false, "RpcServer.json root is not a JSON object.");

            var pluginConfig = root["PluginConfiguration"] as JsonObject;
            if (pluginConfig == null)
            {
                pluginConfig = new JsonObject();
                root["PluginConfiguration"] = pluginConfig;
            }

            var servers = pluginConfig["Servers"] as JsonArray;
            if (servers == null)
            {
                servers = new JsonArray();
                pluginConfig["Servers"] = servers;
            }

            // Avoid dual-stack bind conflicts by keeping a single server entry.
            var hostIsIpv6 = host.Contains(':');
            var preferred = servers.FirstOrDefault(n =>
            {
                if (n is not JsonObject obj) return false;
                var addr = obj["BindAddress"]?.ToString() ?? string.Empty;
                var addrIsIpv6 = addr.Contains(':');
                return hostIsIpv6 == addrIsIpv6;
            }) ?? servers.FirstOrDefault();

            servers.Clear();

            var serverObj = preferred as JsonObject ?? new JsonObject();
            serverObj["BindAddress"] = host;
            serverObj["Port"] = port;
            if (networkMagic.HasValue)
            {
                serverObj["Network"] = networkMagic.Value;
            }
            servers.Add(serverObj);

            File.WriteAllText(
                rpcServerJsonPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}

