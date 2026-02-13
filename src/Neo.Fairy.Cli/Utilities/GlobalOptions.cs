// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;

namespace Neo.Fairy.Cli.Utilities;

internal static class GlobalOptions
{
    public static readonly Option<bool> Json = new(
        name: "--json",
        description: "Output machine-readable JSON (suppresses formatted console output)");

    public static bool IsJson(InvocationContext context)
    {
        return context.ParseResult.GetValueForOption(Json);
    }
}

