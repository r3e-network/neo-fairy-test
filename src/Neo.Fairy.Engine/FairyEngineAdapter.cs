// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using System.Numerics;

namespace Neo.Fairy.Engine;

/// <summary>
/// Adapter that implements IFairyEngine interface and bridges to the existing Fairy RPC.
/// This allows the new modular architecture to work with the existing Fairy plugin.
/// </summary>
public sealed class FairyEngineAdapter : IFairyEngine
{
    private readonly FairyRpcClient _rpcClient;
    private long _lastGasConsumed;
    private ExecutionState _lastState;

    /// <summary>
    /// Creates a new FairyEngineAdapter connected to a Fairy RPC endpoint.
    /// </summary>
    /// <param name="rpcUrl">The Fairy RPC endpoint URL.</param>
    public FairyEngineAdapter(string rpcUrl = "http://localhost:16868")
    {
        _rpcClient = new FairyRpcClient(rpcUrl);
    }

    /// <inheritdoc/>
    public long GasConsumed => _lastGasConsumed;

    /// <inheritdoc/>
    public ExecutionState State => _lastState;

    /// <inheritdoc/>
    public ExecutionResult Execute(IFairySession session, byte[] script, ExecutionOptions? options = null)
    {
        options ??= new ExecutionOptions();

        var result = _rpcClient.InvokeScriptWithSessionAsync(
            session.Id,
            script,
            options.PersistChanges,
            options.Signers).GetAwaiter().GetResult();

        _lastGasConsumed = result.GasConsumed;
        _lastState = result.State;

        return result;
    }

    /// <inheritdoc/>
    public ExecutionResult InvokeMethod(
        IFairySession session,
        string contractHash,
        string method,
        object[]? args = null,
        ExecutionOptions? options = null)
    {
        options ??= new ExecutionOptions();

        var result = _rpcClient.InvokeFunctionWithSessionAsync(
            session.Id,
            contractHash,
            method,
            args ?? Array.Empty<object>(),
            options.PersistChanges,
            options.Signers).GetAwaiter().GetResult();

        _lastGasConsumed = result.GasConsumed;
        _lastState = result.State;

        return result;
    }
}

/// <summary>
/// Client for communicating with Fairy RPC endpoints.
/// </summary>
public sealed class FairyRpcClient
{
    private readonly HttpClient _httpClient;
    private readonly string _rpcUrl;
    private int _requestId;

    public FairyRpcClient(string rpcUrl)
    {
        _rpcUrl = rpcUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    /// <summary>
    /// Invokes a script within a session.
    /// </summary>
    public async Task<ExecutionResult> InvokeScriptWithSessionAsync(
        string session,
        byte[] script,
        bool writeSnapshot,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var scriptBase64 = Convert.ToBase64String(script);
        var parameters = new List<object> { session, writeSnapshot, scriptBase64 };

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        var response = await SendRequestAsync("invokeScriptWithSession", parameters.ToArray());
        return ParseExecutionResult(response);
    }

    /// <summary>
    /// Invokes a contract function within a session.
    /// </summary>
    public async Task<ExecutionResult> InvokeFunctionWithSessionAsync(
        string session,
        string contractHash,
        string method,
        object[] args,
        bool writeSnapshot,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var parameters = new List<object>
        {
            session,
            writeSnapshot,
            contractHash,
            method,
            ConvertArgs(args)
        };

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        var response = await SendRequestAsync("invokeFunctionWithSession", parameters.ToArray());
        return ParseExecutionResult(response);
    }

    /// <summary>
    /// Deploys a contract to a session.
    /// </summary>
    public async Task<DeploymentResult> VirtualDeployAsync(
        string session,
        byte[] nefBytes,
        string manifestJson,
        object? initData = null,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var parameters = new List<object>
        {
            session,
            Convert.ToBase64String(nefBytes),
            manifestJson
        };

        if (initData != null)
        {
            parameters.Add(initData);
        }

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        var response = await SendRequestAsync("virtualDeploy", parameters.ToArray());
        return ParseDeploymentResult(response, session);
    }

    /// <summary>
    /// Sets the GAS balance for an account in a session.
    /// </summary>
    public async Task SetGasBalanceAsync(string session, string account, long balance)
    {
        await SendRequestAsync("setGasBalance", new object[] { session, account, balance.ToString() });
    }

    /// <summary>
    /// Sets the NEO balance for an account in a session.
    /// </summary>
    public async Task SetNeoBalanceAsync(string session, string account, long balance)
    {
        await SendRequestAsync("setNeoBalance", new object[] { session, account, balance.ToString() });
    }

    /// <summary>
    /// Sets the balance of any NEP-17 token for an account in a session.
    /// </summary>
    /// <param name="session">The session ID.</param>
    /// <param name="tokenContract">The token contract hash.</param>
    /// <param name="account">The account hash.</param>
    /// <param name="balance">The balance to set.</param>
    /// <param name="storagePrefix">The storage prefix for the balance key (default: 1).</param>
    public async Task SetNep17BalanceAsync(string session, string tokenContract, string account, long balance, byte storagePrefix = 1)
    {
        await SendRequestAsync("setNep17Balance", new object[] { session, tokenContract, account, balance.ToString(), storagePrefix.ToString() });
    }

    /// <summary>
    /// Sets the timestamp for a session.
    /// </summary>
    public async Task SetTimestampAsync(string session, ulong? timestamp)
    {
        await SendRequestAsync("setSnapshotTimestamp", new object[] { session, timestamp?.ToString()! });
    }

    /// <summary>
    /// Sets the random value for a session.
    /// </summary>
    public async Task SetRandomAsync(string session, BigInteger? random)
    {
        await SendRequestAsync("setSnapshotRandom", new object[] { session, random?.ToString()! });
    }

    /// <summary>
    /// Sets whether witness checks should return true.
    /// </summary>
    public async Task SetCheckWitnessAsync(string session, bool returnTrue)
    {
        await SendRequestAsync("setSnapshotCheckWitness", new object[] { session, returnTrue });
    }

    /// <summary>
    /// Health check - calls HelloFairy.
    /// </summary>
    public async Task<bool> PingAsync()
    {
        try
        {
            await SendRequestAsync("helloFairy", Array.Empty<object>());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<Dictionary<string, object?>> SendRequestAsync(string method, object[] parameters)
    {
        var requestId = Interlocked.Increment(ref _requestId);
        var request = new
        {
            jsonrpc = "2.0",
            method = method,
            @params = parameters,
            id = requestId
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_rpcUrl, content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(responseJson);

        if (result == null)
        {
            throw new InvalidOperationException("Empty response from RPC");
        }

        if (result.TryGetValue("error", out var error) && error != null)
        {
            throw new InvalidOperationException($"RPC Error: {error}");
        }

        if (result.TryGetValue("result", out var resultValue) && resultValue is System.Text.Json.JsonElement element)
        {
            return ParseJsonElement(element);
        }

        return new Dictionary<string, object?>();
    }

    private static Dictionary<string, object?> ParseJsonElement(System.Text.Json.JsonElement element)
    {
        var result = new Dictionary<string, object?>();

        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                result[property.Name] = GetJsonValue(property.Value);
            }
        }

        return result;
    }

    private static object? GetJsonValue(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(GetJsonValue).ToList(),
            System.Text.Json.JsonValueKind.Object => ParseJsonElement(element),
            _ => element.ToString()
        };
    }

    private static ExecutionResult ParseExecutionResult(Dictionary<string, object?> response)
    {
        var stateStr = response.GetValueOrDefault("state")?.ToString() ?? "FAULT";
        var state = stateStr.ToUpperInvariant() switch
        {
            "HALT" => ExecutionState.Halt,
            "FAULT" => ExecutionState.Fault,
            "BREAK" => ExecutionState.Break,
            _ => ExecutionState.None
        };

        var gasStr = response.GetValueOrDefault("gasconsumed")?.ToString() ?? "0";
        var gasConsumed = long.TryParse(gasStr, out var g) ? g : 0;

        var notifications = new List<NotificationInfo>();
        if (response.GetValueOrDefault("notifications") is List<object?> notifList)
        {
            foreach (var notif in notifList)
            {
                if (notif is Dictionary<string, object?> notifDict)
                {
                    notifications.Add(new NotificationInfo
                    {
                        ContractHash = notifDict.GetValueOrDefault("scripthash")?.ToString() ?? "",
                        ContractName = notifDict.GetValueOrDefault("contractname")?.ToString(),
                        EventName = notifDict.GetValueOrDefault("eventname")?.ToString() ?? ""
                    });
                }
            }
        }

        var stack = new List<StackItem>();
        if (response.GetValueOrDefault("stack") is List<object?> stackList)
        {
            foreach (var item in stackList)
            {
                if (item is Dictionary<string, object?> itemDict)
                {
                    stack.Add(new StackItem
                    {
                        Type = itemDict.GetValueOrDefault("type")?.ToString() ?? "Unknown",
                        Value = itemDict.GetValueOrDefault("value")
                    });
                }
            }
        }

        return new ExecutionResult
        {
            State = state,
            GasConsumed = gasConsumed,
            Exception = response.GetValueOrDefault("exception")?.ToString(),
            Traceback = response.GetValueOrDefault("traceback")?.ToString(),
            Notifications = notifications,
            Stack = stack
        };
    }

    private static DeploymentResult ParseDeploymentResult(Dictionary<string, object?> response, string alias)
    {
        var stateStr = response.GetValueOrDefault("state")?.ToString() ?? "HALT";
        var state = stateStr.ToUpperInvariant() == "HALT" ? ExecutionState.Halt : ExecutionState.Fault;

        var gasStr = response.GetValueOrDefault("gasconsumed")?.ToString() ?? "0";
        var gasConsumed = long.TryParse(gasStr, out var g) ? g : 0;

        // Contract hash is returned with session name as key
        var contractHash = "";
        foreach (var kvp in response)
        {
            if (kvp.Key != "state" && kvp.Key != "gasconsumed" && kvp.Key != "networkfee" && kvp.Key != "exception")
            {
                contractHash = kvp.Value?.ToString() ?? "";
                break;
            }
        }

        return new DeploymentResult
        {
            Alias = alias,
            ContractHash = contractHash,
            State = state,
            GasConsumed = gasConsumed,
            Exception = response.GetValueOrDefault("exception")?.ToString()
        };
    }

    private static List<object> ConvertSigners(IReadOnlyList<SignerInfo> signers)
    {
        return signers.Select(s => new Dictionary<string, object>
        {
            ["account"] = s.Account,
            ["scopes"] = s.Scopes
        }).Cast<object>().ToList();
    }

    private static List<object> ConvertArgs(object[] args)
    {
        return args.Select(ConvertArg).ToList();
    }

    private static object ConvertArg(object arg)
    {
        return arg switch
        {
            string s => new { type = "String", value = s },
            int i => new { type = "Integer", value = i.ToString() },
            long l => new { type = "Integer", value = l.ToString() },
            BigInteger bi => new { type = "Integer", value = bi.ToString() },
            bool b => new { type = "Boolean", value = b },
            byte[] bytes => new { type = "ByteArray", value = Convert.ToBase64String(bytes) },
            _ => new { type = "String", value = arg.ToString() }
        };
    }
}
