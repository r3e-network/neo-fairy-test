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
public sealed class FairyEngineAdapter : IFairyEngine, IDisposable
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

    /// <summary>
    /// Disposes the underlying RPC client.
    /// </summary>
    public void Dispose()
    {
        _rpcClient.Dispose();
    }
}

/// <summary>
/// Client for communicating with Fairy RPC endpoints.
/// </summary>
public sealed class FairyRpcClient : IDisposable
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
    /// Deploys a contract to a session, while setting a friendly alias on the returned result.
    /// The server still keys the deployed hash by session name; this overload only affects the client-side Alias field.
    /// </summary>
    public async Task<DeploymentResult> VirtualDeployAsync(
        string session,
        string alias,
        byte[] nefBytes,
        string manifestJson,
        object? initData = null,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var result = await VirtualDeployAsync(session, nefBytes, manifestJson, initData, signers);
        return new DeploymentResult
        {
            Alias = alias,
            ContractHash = result.ContractHash,
            State = result.State,
            GasConsumed = result.GasConsumed,
            NetworkFee = result.NetworkFee,
            TransactionHash = result.TransactionHash,
            Exception = result.Exception,
            AlreadyExists = result.AlreadyExists,
            Note = result.Note
        };
    }

    /// <summary>
    /// Registers or updates a contract artifact in a Fairy workspace.
    /// This enables Foundry-style multi-contract deployments with alias resolution.
    /// </summary>
    public async Task UpsertWorkspaceContractAsync(
        string workspace,
        ContractArtifact artifact)
    {
        var parameters = new List<object>
        {
            workspace,
            artifact.Alias,
            Convert.ToBase64String(artifact.NefBytes),
            artifact.ManifestJson
        };

        if (!string.IsNullOrWhiteSpace(artifact.InitializationDataJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(artifact.InitializationDataJson);
                parameters.Add(doc.RootElement.Clone());
            }
            catch
            {
                // If init data isn't valid JSON, ignore and let deploy proceed without it.
            }
        }

        if (artifact.DefaultSigners != null && artifact.DefaultSigners.Count > 0)
        {
            parameters.Add(ConvertSigners(artifact.DefaultSigners));
        }

        await SendRequestAsync("upsertWorkspaceContract", parameters.ToArray());
    }

    /// <summary>
    /// Deploys all (or filtered) contracts from a workspace into a virtual Fairy session.
    /// </summary>
    public async Task<IReadOnlyList<DeploymentResult>> VirtualDeployWorkspaceAsync(
        string workspace,
        string session,
        IReadOnlyCollection<string>? aliasFilter = null,
        IReadOnlyList<SignerInfo>? overrideSigners = null,
        bool stopOnFault = true)
    {
        var parameters = new List<object> { workspace, session };

        if (aliasFilter != null)
        {
            parameters.Add(aliasFilter.ToArray());
        }

        if (overrideSigners != null && overrideSigners.Count > 0)
        {
            parameters.Add(ConvertSigners(overrideSigners));
        }

        if (!stopOnFault)
        {
            parameters.Add(false);
        }

        var response = await SendRequestAsync("virtualDeployWorkspace", parameters.ToArray());
        if (response.TryGetValue("deployments", out var deploymentsObj) &&
            deploymentsObj is List<object?> deployments)
        {
            var results = new List<DeploymentResult>();
            foreach (var deploymentObj in deployments)
            {
                if (deploymentObj is not Dictionary<string, object?> deployment)
                    continue;

                var alias = deployment.GetValueOrDefault("alias")?.ToString() ?? string.Empty;
                var hash = deployment.GetValueOrDefault("hash")?.ToString()
                           ?? deployment.GetValueOrDefault("contracthash")?.ToString()
                           ?? string.Empty;
                var stateStr = deployment.GetValueOrDefault("state")?.ToString() ?? "FAULT";
                var state = stateStr.ToUpperInvariant() == "HALT"
                    ? ExecutionState.Halt
                    : ExecutionState.Fault;

                var gasStr = deployment.GetValueOrDefault("gasconsumed")?.ToString() ?? "0";
                var gas = decimal.TryParse(gasStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gd) ? (long)gd : (long.TryParse(gasStr, out var g) ? g : 0);

                var networkFeeStr = deployment.GetValueOrDefault("networkfee")?.ToString();
                long? networkFee = long.TryParse(networkFeeStr, out var nf) ? nf : null;

                var exception = deployment.GetValueOrDefault("exception")?.ToString();
                var note = deployment.GetValueOrDefault("note")?.ToString();

                results.Add(new DeploymentResult
                {
                    Alias = alias,
                    ContractHash = hash,
                    State = state,
                    GasConsumed = gas,
                    NetworkFee = networkFee,
                    Exception = exception,
                    AlreadyExists = string.Equals(note, "Already exists", StringComparison.OrdinalIgnoreCase),
                    Note = note
                });
            }

            return results;
        }

        return Array.Empty<DeploymentResult>();
    }

    /// <summary>
    /// Invokes a workspace contract by alias within a session.
    /// </summary>
    public async Task<ExecutionResult> InvokeWorkspaceFunctionWithSessionAsync(
        string workspace,
        string alias,
        string session,
        string method,
        object[] args,
        bool writeSnapshot,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var parameters = new List<object>
        {
            workspace,
            alias,
            session,
            writeSnapshot,
            method,
            ConvertArgs(args)
        };

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        var response = await SendRequestAsync("invokeWorkspaceFunctionWithSession", parameters.ToArray());
        return ParseExecutionResult(response);
    }

    /// <summary>
    /// Gets the last deployed contract hashes for a workspace.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetWorkspaceContractHashesAsync(string workspace)
    {
        var response = await SendRequestAsync("getWorkspaceContractHashes", new object[] { workspace });
        return response.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lists all known workspaces registered on the Fairy node.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListWorkspacesAsync()
    {
        var response = await SendRequestAsync("listWorkspaces", Array.Empty<object>());
        if (response.TryGetValue("value", out var value) && value is List<object?> list)
        {
            return list.Select(v => v?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return Array.Empty<string>();
    }

    /// <summary>
    /// Lists contracts registered in a workspace.
    /// When verbose is false, returns aliases only. When true, returns objects with metadata.
    /// </summary>
    public async Task<IReadOnlyList<object?>> ListWorkspaceContractsAsync(string workspace, bool verbose = false)
    {
        var response = await SendRequestAsync(
            "listWorkspaceContracts",
            new object[] { workspace, verbose });

        return response.TryGetValue("value", out var value) && value is List<object?> list
            ? list
            : Array.Empty<object?>();
    }

    /// <summary>
    /// Clears an entire workspace or removes a single alias from a workspace.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> ClearWorkspaceAsync(string workspace, string? alias = null)
    {
        var parameters = alias == null
            ? new object[] { workspace }
            : new object[] { workspace, alias };

        return await SendRequestAsync("clearWorkspace", parameters);
    }

    /// <summary>
    /// Sets the signing wallet for a session using one or more WIF keys.
    /// Required for relay (on-chain) deploy/invoke operations.
    /// </summary>
    public async Task SetSessionWalletWithWifAsync(string session, params string[] wifs)
    {
        if (wifs.Length == 0)
            throw new ArgumentException("At least one WIF is required.", nameof(wifs));

        var parameters = new List<object> { session };
        parameters.AddRange(wifs.Cast<object>());
        await SendRequestAsync("setSessionFairyWalletWithWif", parameters.ToArray());
    }

    /// <summary>
    /// Sets the signing wallet for a session using NEP2 keys and password.
    /// Required for relay (on-chain) deploy/invoke operations.
    /// </summary>
    public async Task SetSessionWalletWithNep2Async(string session, IReadOnlyList<string> nep2Keys, string password)
    {
        if (nep2Keys.Count == 0)
            throw new ArgumentException("At least one NEP2 key is required.", nameof(nep2Keys));

        var parameters = new List<object> { session };
        foreach (var nep2 in nep2Keys)
        {
            parameters.Add(nep2);
            parameters.Add(password);
        }

        await SendRequestAsync("setSessionFairyWalletWithNep2", parameters.ToArray());
    }

    /// <summary>
    /// Relay deploy a single contract to chain.
    /// The session must already have a signing wallet configured.
    /// </summary>
    public async Task<DeploymentResult> RelayDeployContractAsync(
        string session,
        string alias,
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

        var response = await SendRequestAsync("relayDeployContract", parameters.ToArray());

        var contractHash = response.GetValueOrDefault("contracthash")?.ToString() ?? string.Empty;
        var txHash = response.GetValueOrDefault("hash")?.ToString();

        var sysFeeStr = response.GetValueOrDefault("sysfee")?.ToString() ?? "0";
        var gas = decimal.TryParse(sysFeeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sfd) ? (long)sfd : (long.TryParse(sysFeeStr, out var sf) ? sf : 0);

        var networkFeeStr = response.GetValueOrDefault("networkfee")?.ToString();
        long? networkFee = long.TryParse(networkFeeStr, out var nf) ? nf : null;

        var pendingSig = response.ContainsKey("pendingsignature");
        var note = pendingSig ? "Pending signature" : null;

        return new DeploymentResult
        {
            Alias = alias,
            ContractHash = contractHash,
            State = ExecutionState.Halt,
            GasConsumed = gas,
            NetworkFee = networkFee,
            TransactionHash = txHash,
            Note = note
        };
    }

    /// <summary>
    /// Relay deploy transactions for all or selected workspace contracts to the connected network.
    /// The session must already have a signing wallet configured.
    /// </summary>
    public async Task<IReadOnlyList<DeploymentResult>> RelayDeployWorkspaceAsync(
        string workspace,
        string session,
        IReadOnlyCollection<string>? aliasFilter = null,
        IReadOnlyList<SignerInfo>? overrideSigners = null,
        bool stopOnPending = true)
    {
        var parameters = new List<object> { workspace, session };

        if (aliasFilter != null)
        {
            parameters.Add(aliasFilter.ToArray());
        }

        if (overrideSigners != null && overrideSigners.Count > 0)
        {
            parameters.Add(ConvertSigners(overrideSigners));
        }

        if (!stopOnPending)
        {
            parameters.Add(false);
        }

        var response = await SendRequestAsync("relayDeployWorkspace", parameters.ToArray());
        if (response.TryGetValue("deployments", out var deploymentsObj) &&
            deploymentsObj is List<object?> deployments)
        {
            var results = new List<DeploymentResult>();
            foreach (var deploymentObj in deployments)
            {
                if (deploymentObj is not Dictionary<string, object?> deployment)
                    continue;

                var alias = deployment.GetValueOrDefault("alias")?.ToString() ?? string.Empty;
                var contractHash = deployment.GetValueOrDefault("contracthash")?.ToString() ?? string.Empty;
                var txHash = deployment.GetValueOrDefault("hash")?.ToString();

                var sysFeeStr = deployment.GetValueOrDefault("sysfee")?.ToString() ?? "0";
                var gas = decimal.TryParse(sysFeeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sfd) ? (long)sfd : (long.TryParse(sysFeeStr, out var sf) ? sf : 0);

                var networkFeeStr = deployment.GetValueOrDefault("networkfee")?.ToString();
                long? networkFee = long.TryParse(networkFeeStr, out var nf) ? nf : null;

                var pendingSig = deployment.ContainsKey("pendingsignature");
                var note = pendingSig ? "Pending signature" : null;

                results.Add(new DeploymentResult
                {
                    Alias = alias,
                    ContractHash = contractHash,
                    State = ExecutionState.Halt,
                    GasConsumed = gas,
                    NetworkFee = networkFee,
                    TransactionHash = txHash,
                    Note = note
                });
            }

            return results;
        }

        return Array.Empty<DeploymentResult>();
    }

    /// <summary>
    /// Relay an invocation to chain. The session must have a signing wallet configured.
    /// </summary>
    public async Task<ExecutionResult> RelayInvokeFunctionAsync(
        string session,
        string contractHash,
        string method,
        object[] args,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var parameters = new List<object>
        {
            session,
            contractHash,
            method,
            ConvertArgs(args)
        };

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        var response = await SendRequestAsync("relayInvokeFunction", parameters.ToArray());

        // Relay invoke results don't include VM stack/state; treat as successful tx submission.
        var pendingSig = response.ContainsKey("pendingsignature");
        return new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = decimal.TryParse(response.GetValueOrDefault("sysfee")?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sfd) ? (long)sfd : (long.TryParse(response.GetValueOrDefault("sysfee")?.ToString(), out var sf) ? sf : 0),
            TransactionHash = response.GetValueOrDefault("hash")?.ToString(),
            NetworkFee = long.TryParse(response.GetValueOrDefault("networkfee")?.ToString(), out var nf) ? nf : null,
            Note = pendingSig ? "Pending signature" : null
        };
    }

    /// <summary>
    /// Relay a workspace alias invocation to chain. The session must have a signing wallet configured.
    /// </summary>
    public async Task<ExecutionResult> RelayInvokeWorkspaceFunctionAsync(
        string workspace,
        string alias,
        string session,
        string method,
        object[] args,
        IReadOnlyList<SignerInfo>? signers = null)
    {
        var parameters = new List<object>
        {
            workspace,
            alias,
            session,
            method,
            ConvertArgs(args)
        };

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        var response = await SendRequestAsync("relayInvokeWorkspaceFunction", parameters.ToArray());
        var pendingSig = response.ContainsKey("pendingsignature");
        return new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = decimal.TryParse(response.GetValueOrDefault("sysfee")?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var sfd) ? (long)sfd : (long.TryParse(response.GetValueOrDefault("sysfee")?.ToString(), out var sf) ? sf : 0),
            TransactionHash = response.GetValueOrDefault("hash")?.ToString(),
            NetworkFee = long.TryParse(response.GetValueOrDefault("networkfee")?.ToString(), out var nf) ? nf : null,
            Note = pendingSig ? "Pending signature" : null
        };
    }

    /// <summary>
    /// Waits for a relayed transaction to be confirmed on-chain.
    /// Returns verbose transaction JSON when verbose is true.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> AwaitConfirmedTransactionAsync(
        string txHash,
        bool verbose = false,
        uint waitBlocks = 2)
    {
        var parameters = new object[]
        {
            txHash,
            verbose,
            waitBlocks.ToString()
        };

        return await SendRequestAsync("awaitConfirmedTransaction", parameters);
    }

    // -----------------
    // Debugging & Coverage
    // -----------------

    /// <summary>
    /// Registers debug information for a deployed contract.
    /// Requires a base64 encoded .nefdbgnfo file and dumpnef text.
    /// </summary>
    public async Task SetDebugInfoAsync(string contractHash, byte[] nefDbgNfoBytes, string? dumpNefText)
    {
        var parameters = new object[]
        {
            contractHash,
            Convert.ToBase64String(nefDbgNfoBytes),
            dumpNefText ?? string.Empty
        };

        await SendRequestAsync("setDebugInfo", parameters);
    }

    /// <summary>
    /// Starts a debug run for a contract function within a session.
    /// The returned result includes break reason, instruction pointer, and optional source mapping.
    /// </summary>
    public async Task<Dictionary<string, object?>> DebugFunctionWithSessionAsync(
        string session,
        bool writeSnapshot,
        string contractHash,
        string method,
        object[]? args = null,
        IReadOnlyList<SignerInfo>? signers = null,
        IReadOnlyList<object>? witnesses = null)
    {
        var parameters = new List<object>
        {
            session,
            writeSnapshot,
            contractHash,
            method
        };

        if (args != null)
        {
            parameters.Add(ConvertArgs(args));
        }

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        if (witnesses != null && witnesses.Count > 0)
        {
            parameters.Add(witnesses.ToArray());
        }

        return await SendRequestAsync("debugFunctionWithSession", parameters.ToArray());
    }

    /// <summary>
    /// Starts a debug run for an arbitrary script within a session.
    /// </summary>
    public async Task<Dictionary<string, object?>> DebugScriptWithSessionAsync(
        string session,
        bool writeSnapshot,
        byte[] script,
        IReadOnlyList<SignerInfo>? signers = null,
        IReadOnlyList<object>? witnesses = null)
    {
        var parameters = new List<object>
        {
            session,
            writeSnapshot,
            Convert.ToBase64String(script)
        };

        if (signers != null && signers.Count > 0)
        {
            parameters.Add(ConvertSigners(signers));
        }

        if (witnesses != null && witnesses.Count > 0)
        {
            parameters.Add(witnesses.ToArray());
        }

        return await SendRequestAsync("debugScriptWithSession", parameters.ToArray());
    }

    /// <summary>
    /// Continues execution from a breakpoint.
    /// </summary>
    public Task<Dictionary<string, object?>> DebugContinueAsync(string session)
        => SendRequestAsync("debugContinue", new object[] { session });

    /// <summary>
    /// Steps into the next instruction or call.
    /// </summary>
    public Task<Dictionary<string, object?>> DebugStepIntoAsync(string session)
        => SendRequestAsync("debugStepInto", new object[] { session });

    /// <summary>
    /// Steps over the next source line if debug info is registered.
    /// </summary>
    public Task<Dictionary<string, object?>> DebugStepOverSourceAsync(string session)
        => SendRequestAsync("debugStepOverSourceCode", new object[] { session });

    /// <summary>
    /// Steps over a single VM instruction (assembly-level).
    /// </summary>
    public Task<Dictionary<string, object?>> DebugStepOverAssemblyAsync(string session)
        => SendRequestAsync("debugStepOverAssembly", new object[] { session });

    /// <summary>
    /// Steps out of the current call frame.
    /// </summary>
    public Task<Dictionary<string, object?>> DebugStepOutAsync(string session)
        => SendRequestAsync("debugStepOut", new object[] { session });

    /// <summary>
    /// Sets assembly breakpoints for a contract.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, bool>> SetAssemblyBreakpointsAsync(string contractHash, params uint[] instructionPointers)
    {
        var parameters = new List<object> { contractHash };
        parameters.AddRange(instructionPointers.Select(ip => ip.ToString()).Cast<object>());

        var response = await SendRequestAsync("setAssemblyBreakpoints", parameters.ToArray());
        return ParseBooleanMap(response);
    }

    /// <summary>
    /// Sets source code breakpoints for a contract.
    /// </summary>
    public async Task<IReadOnlyList<(string File, uint Line)>> SetSourceCodeBreakpointsAsync(string contractHash, params (string File, uint Line)[] breakpoints)
    {
        var parameters = new List<object> { contractHash };
        foreach (var (file, line) in breakpoints)
        {
            parameters.Add(file);
            parameters.Add(line.ToString());
        }

        var response = await SendRequestAsync("setSourceCodeBreakpoints", parameters.ToArray());
        if (response.TryGetValue("value", out var value) && value is List<object?> list)
        {
            var parsed = new List<(string File, uint Line)>();
            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> bp)
                {
                    var file = bp.GetValueOrDefault("filename")?.ToString() ?? string.Empty;
                    var lineStr = bp.GetValueOrDefault("line")?.ToString() ?? "0";
                    if (uint.TryParse(lineStr, out var line))
                    {
                        parsed.Add((file, line));
                    }
                }
            }
            return parsed;
        }

        return Array.Empty<(string File, uint Line)>();
    }

    /// <summary>
    /// Lists current invocation stack frames for a debug session.
    /// </summary>
    public async Task<IReadOnlyList<object?>> GetInvocationStackAsync(string session)
    {
        var response = await SendRequestAsync("getInvocationStack", new object[] { session });
        return response.TryGetValue("value", out var value) && value is List<object?> list
            ? list
            : Array.Empty<object?>();
    }

    /// <summary>
    /// Lists current evaluation stack items for a debug session.
    /// </summary>
    public async Task<IReadOnlyList<object?>> GetEvaluationStackAsync(string session, int invocationStackIndex = 0)
    {
        var response = await SendRequestAsync("getEvaluationStack", new object[] { session, invocationStackIndex.ToString() });
        return response.TryGetValue("value", out var value) && value is List<object?> list
            ? list
            : Array.Empty<object?>();
    }

    /// <summary>
    /// Gets variable names and values at a given frame (requires debug info registration).
    /// </summary>
    public Task<Dictionary<string, object?>> GetVariableNamesAndValuesAsync(string session, int invocationStackIndex = 0)
        => SendRequestAsync("getVariableNamesAndValues", new object[] { session, invocationStackIndex.ToString() });

    /// <summary>
    /// Gets source code coverage for a contract (requires debug info registration).
    /// </summary>
    public Task<Dictionary<string, object?>> GetContractSourceCodeCoverageAsync(string contractHash)
        => SendRequestAsync("getContractSourceCodeCoverage", new object[] { contractHash });

    /// <summary>
    /// Clears opcode coverage for a contract.
    /// </summary>
    public Task<Dictionary<string, object?>> ClearContractOpCodeCoverageAsync(string contractHash)
        => SendRequestAsync("clearContractOpCodeCoverage", new object[] { contractHash });

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
        var parameters = timestamp.HasValue
            ? new object[] { session, timestamp.Value.ToString() }
            : new object[] { session };
        await SendRequestAsync("setSnapshotTimestamp", parameters);
    }

    /// <summary>
    /// Sets the random value for a session.
    /// </summary>
    public async Task SetRandomAsync(string session, BigInteger? random)
    {
        var parameters = random.HasValue
            ? new object[] { session, random.Value.ToString() }
            : new object[] { session };
        await SendRequestAsync("setSnapshotRandom", parameters);
    }

    /// <summary>
    /// Sets whether witness checks should return true.
    /// </summary>
    public async Task SetCheckWitnessAsync(string session, bool returnTrue)
    {
        await SendRequestAsync("setSnapshotCheckWitness", new object[] { session, returnTrue });
    }

    /// <summary>
    /// Directly stores a value in contract storage.
    /// </summary>
    /// <param name="session">The session ID.</param>
    /// <param name="contractHash">The target contract hash.</param>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The value to store.</param>
    public async Task SetStorageAsync(string session, string contractHash, byte[] key, byte[] value)
    {
        await SendRequestAsync("setStorage", new object[]
        {
            session,
            contractHash,
            Convert.ToBase64String(key),
            Convert.ToBase64String(value)
        });
    }

    /// <summary>
    /// Directly loads a value from contract storage.
    /// </summary>
    /// <param name="session">The session ID.</param>
    /// <param name="contractHash">The target contract hash.</param>
    /// <param name="key">The storage key.</param>
    /// <returns>The stored value, or null if not found.</returns>
    public async Task<byte[]?> GetStorageAsync(string session, string contractHash, byte[] key)
    {
        var response = await SendRequestAsync("getStorage", new object[]
        {
            session,
            contractHash,
            Convert.ToBase64String(key)
        });

        if (response.TryGetValue("value", out var value) && value is string base64)
        {
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch
            {
                return null;
            }
        }

        return null;
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

    /// <summary>
    /// Returns basic node status information from HelloFairy.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> HelloFairyAsync()
    {
        return await SendRequestAsync("helloFairy", Array.Empty<object>());
    }

    /// <summary>
    /// Creates new snapshots (sessions) from the current system state.
    /// This is useful to explicitly create a session before it is referenced elsewhere.
    /// </summary>
    public async Task NewSnapshotsFromCurrentSystemAsync(params string[] sessions)
    {
        if (sessions.Length == 0) return;
        await SendRequestAsync("newSnapshotsFromCurrentSystem", sessions.Cast<object>().ToArray());
    }

    /// <summary>
    /// Copies an existing snapshot (session) to a new snapshot name.
    /// Equivalent to cloning session state on the Fairy server.
    /// </summary>
    public async Task CopySnapshotAsync(string fromSession, string toSession)
    {
        await SendRequestAsync("copySnapshot", new object[] { fromSession, toSession });
    }

    /// <summary>
    /// Renames a snapshot (session) on the server.
    /// </summary>
    public async Task RenameSnapshotAsync(string fromSession, string toSession)
    {
        await SendRequestAsync("renameSnapshot", new object[] { fromSession, toSession });
    }

    /// <summary>
    /// Deletes snapshots (sessions) on the server.
    /// </summary>
    public async Task DeleteSnapshotsAsync(params string[] sessions)
    {
        if (sessions.Length == 0) return;
        await SendRequestAsync("deleteSnapshots", sessions.Cast<object>().ToArray());
    }

    /// <summary>
    /// Lists all snapshots (sessions) currently stored on the Fairy server.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListSnapshotsAsync()
    {
        var response = await SendRequestAsync("listSnapshots", Array.Empty<object>());
        if (response.TryGetValue("value", out var value) && value is List<object?> list)
        {
            return list.Select(v => v?.ToString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        return Array.Empty<string>();
    }

    /// <summary>
    /// Reads the current snapshot timestamp for one or more sessions.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ulong?>> GetSnapshotTimestampAsync(params string[] sessions)
    {
        var response = await SendRequestAsync("getSnapshotTimeStamp", sessions.Cast<object>().ToArray());
        var result = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in response)
        {
            if (kvp.Value == null)
            {
                result[kvp.Key] = null;
                continue;
            }

            if (ulong.TryParse(kvp.Value.ToString(), out var ts))
            {
                result[kvp.Key] = ts;
            }
            else
            {
                result[kvp.Key] = null;
            }
        }
        return result;
    }

    /// <summary>
    /// Reads the designated random value for one or more sessions.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ulong?>> GetSnapshotRandomAsync(params string[] sessions)
    {
        var response = await SendRequestAsync("getSnapshotRandom", sessions.Cast<object>().ToArray());
        var result = new Dictionary<string, ulong?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in response)
        {
            if (kvp.Value == null)
            {
                result[kvp.Key] = null;
                continue;
            }

            if (BigInteger.TryParse(kvp.Value.ToString(), out var bi))
            {
                result[kvp.Key] = bi >= 0 && bi <= ulong.MaxValue ? (ulong)bi : null;
            }
            else
            {
                result[kvp.Key] = null;
            }
        }
        return result;
    }

    /// <summary>
    /// Reads the CheckWitnessReturnTrue flag for one or more sessions.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, bool>> GetSnapshotCheckWitnessAsync(params string[] sessions)
    {
        var response = await SendRequestAsync("getSnapshotCheckWitness", sessions.Cast<object>().ToArray());
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in response)
        {
            if (kvp.Value is bool b)
            {
                result[kvp.Key] = b;
            }
            else if (bool.TryParse(kvp.Value?.ToString(), out var parsed))
            {
                result[kvp.Key] = parsed;
            }
        }
        return result;
    }

    private Task<Dictionary<string, object?>> SendRequestAsync(string method, object[] parameters)
    {
        return SendRequestAsync(method, parameters, allowLowercaseFallback: true);
    }

    private async Task<Dictionary<string, object?>> SendRequestAsync(string method, object[] parameters, bool allowLowercaseFallback)
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
            if (allowLowercaseFallback &&
                error is System.Text.Json.JsonElement errorElement &&
                errorElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                errorElement.TryGetProperty("code", out var codeElement) &&
                codeElement.TryGetInt32(out var code) &&
                code == -32601)
            {
                var lower = method.ToLowerInvariant();
                if (!string.Equals(lower, method, StringComparison.Ordinal))
                {
                    try
                    {
                        return await SendRequestAsync(lower, parameters, allowLowercaseFallback: false);
                    }
                    catch (InvalidOperationException)
                    {
                        throw new InvalidOperationException(
                            $"RPC Error: Method not found. Tried '{method}' and '{lower}', neither is supported by the server.");
                    }
                }
            }

            throw new InvalidOperationException($"RPC Error: {error}");
        }

        if (result.TryGetValue("result", out var resultValue) && resultValue is System.Text.Json.JsonElement element)
        {
            var parsed = GetJsonValue(element);
            if (parsed is Dictionary<string, object?> dict)
            {
                return dict;
            }

            // Non-object results (arrays/scalars) are wrapped under a "value" key.
            return new Dictionary<string, object?> { ["value"] = parsed };
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
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : (element.TryGetDecimal(out var dec) ? (object)dec : element.GetDouble()),
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
        var gasConsumed = decimal.TryParse(gasStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gd) ? (long)gd : (long.TryParse(gasStr, out var g) ? g : 0);

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
        var gasConsumed = decimal.TryParse(gasStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gd) ? (long)gd : (long.TryParse(gasStr, out var g) ? g : 0);

        // Contract hash is returned with session name as key.
        // Try known keys first, then fall back to first non-metadata key.
        var contractHash = response.GetValueOrDefault("contracthash")?.ToString()
            ?? response.GetValueOrDefault("hash")?.ToString()
            ?? "";
        if (string.IsNullOrEmpty(contractHash))
        {
            var metadataKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "state", "gasconsumed", "networkfee", "exception", "traceback", "notifications", "stack" };
            foreach (var kvp in response)
            {
                if (!metadataKeys.Contains(kvp.Key))
                {
                    contractHash = kvp.Value?.ToString() ?? "";
                    break;
                }
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

    private static IReadOnlyDictionary<string, bool> ParseBooleanMap(Dictionary<string, object?> response)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in response)
        {
            if (kvp.Value is bool b)
            {
                result[kvp.Key] = b;
            }
            else if (bool.TryParse(kvp.Value?.ToString(), out var parsed))
            {
                result[kvp.Key] = parsed;
            }
        }
        return result;
    }

    private static List<object> ConvertSigners(IReadOnlyList<SignerInfo> signers)
    {
        return signers.Select(s =>
        {
            var dict = new Dictionary<string, object>
            {
                ["account"] = s.Account,
                ["scopes"] = s.Scopes
            };
            if (s.AllowedContracts != null && s.AllowedContracts.Count > 0)
            {
                dict["allowedcontracts"] = s.AllowedContracts.ToArray();
            }
            return dict;
        }).Cast<object>().ToList();
    }

    private static List<object> ConvertArgs(object[] args)
    {
        return args.Select(ConvertArg).ToList();
    }

    private static object ConvertArg(object arg)
    {
        if (arg is string s)
        {
            var colon = s.IndexOf(':');
            if (colon > 0)
            {
                var prefix = s[..colon].Trim().ToLowerInvariant();
                var raw = s[(colon + 1)..];

                switch (prefix)
                {
                    case "int":
                    case "integer":
                    case "bigint":
                        return new { type = "Integer", value = raw };
                    case "bool":
                    case "boolean":
                        return bool.TryParse(raw, out var b)
                            ? new { type = "Boolean", value = b }
                            : new { type = "Boolean", value = raw };
                    case "hash160":
                    case "uint160":
                    case "address":
                        return new { type = "Hash160", value = raw };
                    case "hash256":
                    case "uint256":
                        return new { type = "Hash256", value = raw };
                    case "bytes":
                    case "bytearray":
                    case "hex":
                        var bytes = TryParseHex(raw);
                        return new
                        {
                            type = "ByteArray",
                            value = bytes != null ? Convert.ToBase64String(bytes) : raw
                        };
                    case "string":
                        return new { type = "String", value = raw };
                }
            }

            return new { type = "String", value = s };
        }

        return arg switch
        {
            int i => new { type = "Integer", value = i.ToString() },
            long l => new { type = "Integer", value = l.ToString() },
            BigInteger bi => new { type = "Integer", value = bi.ToString() },
            bool b => new { type = "Boolean", value = b },
            byte[] bytes => new { type = "ByteArray", value = Convert.ToBase64String(bytes) },
            _ => new { type = "String", value = arg.ToString() }
        };
    }

    private static byte[]? TryParseHex(string value)
    {
        var trimmed = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        if (trimmed.Length == 0 || trimmed.Length % 2 != 0)
        {
            return null;
        }

        try
        {
            return Convert.FromHexString(trimmed);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Disposes the underlying HttpClient.
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
