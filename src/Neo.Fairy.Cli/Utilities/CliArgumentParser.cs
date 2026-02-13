// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Numerics;

namespace Neo.Fairy.Cli.Utilities;

internal static class CliArgumentParser
{
    public static bool LooksLikeHash(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && value.Length >= 42;

    public static object ParseArgument(string input)
    {
        if (input.Contains(':', StringComparison.Ordinal))
            return input;

        if (bool.TryParse(input, out var b))
            return b;

        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return input;

        if (BigInteger.TryParse(input, out var bi))
            return bi;

        return input;
    }
}

