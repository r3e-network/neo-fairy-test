// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Models;

namespace Neo.Fairy.Cli.Utilities;

internal static class RpcUrlResolver
{
    public static string Resolve(string? rpcUrl, FairyProject? project, string defaultUrl = "http://localhost:16868")
    {
        if (!string.IsNullOrWhiteSpace(rpcUrl))
            return rpcUrl;

        var env = Environment.GetEnvironmentVariable("FAIRY_RPC_URL");
        if (!string.IsNullOrWhiteSpace(env))
            return env;

        return project?.Config.Fairy.RpcUrl ?? defaultUrl;
    }
}

