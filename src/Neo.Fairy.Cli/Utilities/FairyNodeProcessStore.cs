// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Text.Json;

namespace Neo.Fairy.Cli.Utilities;

internal sealed record FairyNodeProcessInfo
{
    public required int Pid { get; init; }
    public required int Port { get; init; }
    public required string Host { get; init; }
    public required string RpcUrl { get; init; }
    public required string NeoCliPath { get; init; }
    public required string NeoCliConfigPath { get; init; }
    public required string WorkingDirectory { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

internal static class FairyNodeProcessStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetStateDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Directory.GetCurrentDirectory();

        var stateDir = Path.Combine(baseDir, "neo-fairy");
        Directory.CreateDirectory(stateDir);
        return stateDir;
    }

    public static string GetStatePath(int port)
    {
        return Path.Combine(GetStateDirectory(), $"node-{port}.json");
    }

    public static FairyNodeProcessInfo? TryRead(int port)
    {
        try
        {
            var path = GetStatePath(port);
            if (!File.Exists(path))
                return null;

            return JsonSerializer.Deserialize<FairyNodeProcessInfo>(
                File.ReadAllText(path),
                JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Write(FairyNodeProcessInfo info)
    {
        var path = GetStatePath(info.Port);
        File.WriteAllText(path, JsonSerializer.Serialize(info, JsonOptions));
    }

    public static void Delete(int port)
    {
        try
        {
            var path = GetStatePath(port);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort
        }
    }
}

