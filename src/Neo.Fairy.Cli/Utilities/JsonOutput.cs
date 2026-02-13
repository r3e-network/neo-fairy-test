// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Text.Json;

namespace Neo.Fairy.Cli.Utilities;

internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Write(object value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, Options));
    }
}

