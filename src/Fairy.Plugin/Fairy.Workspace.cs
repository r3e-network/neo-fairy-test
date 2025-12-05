using System.Collections.Concurrent;
using Neo.Json;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;
using Akka.Actor;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        private readonly ConcurrentDictionary<string, WorkspaceDefinition> workspaces = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, UInt160>> workspaceDeployments = new(StringComparer.OrdinalIgnoreCase);

        private WorkspaceDefinition GetOrCreateWorkspace(string name)
        {
            return workspaces.GetOrAdd(name, workspaceName => new WorkspaceDefinition(workspaceName));
        }

        private WorkspaceDefinition RequireWorkspace(string name)
        {
            if (!workspaces.TryGetValue(name, out WorkspaceDefinition? workspace))
                throw new ArgumentException($"Workspace `{name}` not found.");
            return workspace;
        }

        private void TrackDeployment(string workspaceName, string alias, string hashString)
        {
            if (!UInt160.TryParse(hashString, out UInt160? parsed) || parsed is null)
                return;
            var aliasMap = workspaceDeployments.GetOrAdd(workspaceName, _ => new ConcurrentDictionary<string, UInt160>(StringComparer.OrdinalIgnoreCase));
            aliasMap[alias] = parsed;
        }

        private UInt160 ResolveWorkspaceAlias(string workspaceName, string alias)
        {
            if (workspaceDeployments.TryGetValue(workspaceName, out var aliasMap) && aliasMap.TryGetValue(alias, out UInt160? hash) && hash != null)
                return hash;
            throw new ArgumentException($"No deployment recorded for workspace `{workspaceName}` alias `{alias}`. Deploy it first via VirtualDeployWorkspace or RelayDeployWorkspace.");
        }

        internal bool TryGetWorkspaceAlias(UInt160 scriptHash, out string workspaceName, out string alias)
        {
            foreach (var workspaceEntry in workspaceDeployments)
            {
                foreach (var aliasEntry in workspaceEntry.Value)
                {
                    if (aliasEntry.Value == scriptHash)
                    {
                        workspaceName = workspaceEntry.Key;
                        alias = aliasEntry.Key;
                        return true;
                    }
                }
            }
            workspaceName = string.Empty;
            alias = string.Empty;
            return false;
        }

        private static IReadOnlyCollection<string>? ParseAliasFilter(JArray? aliasArray)
        {
            if (aliasArray == null) return null;
            List<string> aliases = new();
            foreach (var alias in aliasArray)
                aliases.Add(alias!.AsString());
            return aliases;
        }

        [FairyRpcMethod]
        protected virtual JToken ListWorkspaces(JArray _params)
        {
            JArray json = new();
            foreach (string key in workspaces.Keys)
                json.Add(key);
            return json;
        }

        [FairyRpcMethod]
        protected virtual JObject GetWorkspaceContractHashes(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            JObject json = new();
            if (workspaceDeployments.TryGetValue(workspaceName, out var aliasMap))
            {
                foreach (var kvp in aliasMap)
                    json[kvp.Key] = kvp.Value.ToString();
            }
            return json;
        }

        /// <summary>
        /// Register or update a contract artifact in a workspace slot (Foundry-style bundle).
        /// Params: [workspaceName, alias, nefBase64, manifestJson, data? (JObject), defaultSigners? (JArray signers)]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject UpsertWorkspaceContract(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string alias = _params[1]!.AsString();
            byte[] nefBytes = Convert.FromBase64String(_params[2]!.AsString());
            string manifestJson = _params[3]!.AsString();
            ContractManifest manifest = ContractManifest.Parse(manifestJson);

            string? dataJson = null;
            int signerIndex = 4;
            if (_params.Count > 4 && _params[4] is JObject dataObj)
            {
                ContractParameter.FromJson(dataObj);
                dataJson = dataObj.ToString();
                signerIndex = 5;
            }

            Signer[]? defaultSigners = _params.Count > signerIndex ? SignersFromJson((JArray)_params[signerIndex]!, system.Settings) : null;

            WorkspaceDefinition workspace = GetOrCreateWorkspace(workspaceName);
            workspace.Upsert(new WorkspaceContract(alias, nefBytes, manifest, dataJson, defaultSigners));

            return new JObject
            {
                ["workspace"] = workspaceName,
                ["alias"] = alias,
                ["manifestname"] = manifest.Name,
                ["hasdata"] = dataJson != null,
                ["signers"] = defaultSigners?.Length ?? 0
            };
        }

        [FairyRpcMethod]
        protected virtual JToken ListWorkspaceContracts(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            bool verbose = _params.Count >= 2 && _params[1]!.AsBoolean();
            WorkspaceDefinition workspace = RequireWorkspace(workspaceName);
            return workspace.ToJson(verbose);
        }

        /// <summary>
        /// Remove an alias or an entire workspace.
        /// Params: [workspaceName, alias?]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject ClearWorkspace(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            if (_params.Count > 1 && _params[1] != null)
            {
                string alias = _params[1]!.AsString();
                WorkspaceDefinition workspace = RequireWorkspace(workspaceName);
                bool removed = workspace.Remove(alias);
                if (workspaceDeployments.TryGetValue(workspaceName, out var aliasMap))
                {
                    aliasMap.TryRemove(alias, out _);
                    if (aliasMap.IsEmpty)
                        workspaceDeployments.TryRemove(workspaceName, out _);
                }
                return new JObject { ["workspace"] = workspaceName, ["alias"] = alias, ["removed"] = removed };
            }

            bool removedWorkspace = workspaces.TryRemove(workspaceName, out _);
            workspaceDeployments.TryRemove(workspaceName, out _);
            return new JObject { ["workspace"] = workspaceName, ["removed"] = removedWorkspace };
        }

        /// <summary>
        /// Deploy all or selected contracts from a workspace into a Fairy session snapshot.
        /// Params: [workspaceName, session, aliasFilter?(JArray), overrideSigners?(JArray), stopOnFault?(bool, default true)]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject VirtualDeployWorkspace(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string session = _params[1]!.AsString();
            int paramIndex = 2;
            IReadOnlyCollection<string>? aliasFilter = null;
            if (_params.Count > paramIndex && _params[paramIndex] is JArray aliasArray)
            {
                aliasFilter = ParseAliasFilter((JArray)aliasArray!);
                paramIndex++;
            }

            Signer[]? overrideSigners = _params.Count > paramIndex ? SignersFromJson((JArray)_params[paramIndex]!, system.Settings) : null;
            bool stopOnFault = _params.Count > paramIndex + 1 ? _params[paramIndex + 1]!.AsBoolean() : true;

            WorkspaceDefinition workspace = RequireWorkspace(workspaceName);
            IReadOnlyList<WorkspaceContract> contracts = workspace.GetContracts(aliasFilter);
            if (contracts.Count == 0)
                throw new ArgumentException($"Workspace `{workspaceName}` has no contracts.");

            FairySession testSession = GetOrCreateFairySession(session);
            Wallet wallet = GetSigningWallet(testSession);

            JArray deployments = new();
            foreach (WorkspaceContract contract in contracts)
            {
                ContractParameter? data = contract.BuildData();
                Signer[] signers = PrepareDeploySigners(overrideSigners ?? contract.BuildSigners(), wallet);
                JObject entry = new() { ["alias"] = contract.Alias };
                try
                {
                    VirtualDeployResult result = ExecuteVirtualDeploy(testSession, session, contract.Nef, contract.Manifest, data, signers, wallet);
                    entry["hash"] = result.ContractHash.ToString();
                    entry["gasconsumed"] = result.GasConsumed.ToString();
                    entry["networkfee"] = result.NetworkFee.ToString();
                    entry["state"] = result.State.ToString();
                    if (result.Exception != null)
                        entry["exception"] = result.Exception;
                    if (result.State == VMState.HALT)
                        TrackDeployment(workspaceName, contract.Alias, result.ContractHash.ToString());
                    deployments.Add(entry);
                    if (stopOnFault && result.State == VMState.FAULT)
                        break;
                }
                catch (InvalidOperationException ex)
                {
                    if (ex.InnerException != null && ex.InnerException.Message.StartsWith("Contract Already Exists: "))
                    {
                        entry["hash"] = ex.InnerException.Message[^42..];
                        entry["state"] = VMState.HALT.ToString();
                        entry["note"] = "Already exists";
                        TrackDeployment(workspaceName, contract.Alias, entry["hash"]!.AsString());
                        deployments.Add(entry);
                        continue;
                    }
                    throw;
                }
            }

            return new JObject
            {
                ["workspace"] = workspaceName,
                ["session"] = session,
                ["deployments"] = deployments
            };
        }

        /// <summary>
        /// Relay deploy transactions for all or selected workspace contracts to the connected network.
        /// Params: [workspaceName, session|null, aliasFilter?(JArray), overrideSigners?(JArray), stopOnPending?(bool, default true)]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject RelayDeployWorkspace(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string? sessionName = _params[1]?.AsString();
            int paramIndex = 2;
            IReadOnlyCollection<string>? aliasFilter = null;
            if (_params.Count > paramIndex && _params[paramIndex] is JArray aliasArray)
            {
                aliasFilter = ParseAliasFilter((JArray)aliasArray!);
                paramIndex++;
            }

            Signer[]? overrideSigners = _params.Count > paramIndex ? SignersFromJson((JArray)_params[paramIndex]!, system.Settings) : null;
            bool stopOnPending = _params.Count > paramIndex + 1 ? _params[paramIndex + 1]!.AsBoolean() : true;

            WorkspaceDefinition workspace = RequireWorkspace(workspaceName);
            IReadOnlyList<WorkspaceContract> contracts = workspace.GetContracts(aliasFilter);
            if (contracts.Count == 0)
                throw new ArgumentException($"Workspace `{workspaceName}` has no contracts.");

            FairySession? session = sessionName == null ? null : GetOrCreateFairySession(sessionName);
            Wallet wallet = GetSigningWallet(session, allowDefaultWallet: false);

            JArray deployments = new();
            foreach (WorkspaceContract contract in contracts)
            {
                ContractParameter? data = contract.BuildData();
                Signer[] signers = PrepareDeploySigners(overrideSigners ?? contract.BuildSigners(), wallet);
                RelayDeployResult result = ExecuteRelayDeploy(contract.Nef, contract.Manifest, data, signers, wallet);

                JObject entry = result.Transaction.ToJson(system.Settings);
                entry["alias"] = contract.Alias;
                entry["contracthash"] = result.ContractHash.ToString();
                entry["tx"] = Convert.ToBase64String(result.Transaction.ToArray());
                entry["hash"] = result.Transaction.Hash.ToString();
                entry["networkfee"] = result.NetworkFee;
                entry["sysfee"] = result.SystemFee;
                if (result.Context != null)
                    entry["pendingsignature"] = result.Context.ToJson();
                TrackDeployment(workspaceName, contract.Alias, result.ContractHash.ToString());
                deployments.Add(entry);
                if (stopOnPending && result.Context != null)
                    break;
            }

            return new JObject
            {
                ["workspace"] = workspaceName,
                ["session"] = sessionName,
                ["deployments"] = deployments
            };
        }

        /// <summary>
        /// Invoke a deployed workspace contract by alias within a Fairy session snapshot.
        /// Params: [workspaceName, alias, session, writeSnapshot, operation, args?, signers?, witnesses?]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject InvokeWorkspaceFunctionWithSession(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string alias = _params[1]!.AsString();
            string session = _params[2]!.AsString();
            bool writeSnapshot = _params[3]!.AsBoolean();
            string operation = _params[4]!.AsString();
            ContractParameter[] args = _params.Count >= 6 ? ((JArray)_params[5]!).Select(p => ContractParameter.FromJson((JObject)p!)).ToArray() : System.Array.Empty<ContractParameter>();
            Signer[]? signers = _params.Count >= 7 ? SignersFromJson((JArray)_params[6]!, system.Settings) : null;
            Witness[]? witnesses = _params.Count >= 8 ? WitnessesFromJson((JArray)_params[7]!) : null;

            UInt160 contractHash = ResolveWorkspaceAlias(workspaceName, alias);
            byte[] script;
            using (ScriptBuilder sb = new())
            {
                script = sb.EmitDynamicCall(contractHash, operation, args).ToArray();
            }
            JObject result = GetInvokeResultWithSession(session, writeSnapshot, script, signers, witnesses);
            result["workspace"] = workspaceName;
            result["alias"] = alias;
            result["hash"] = contractHash.ToString();
            return result;
        }

        /// <summary>
        /// Invoke multiple workspace aliases in one script within a Fairy session snapshot.
        /// Params: [workspaceName, session, writeSnapshot, calls(JArray of [alias, operation, args?]), signers?, witnesses?]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject InvokeWorkspaceManyWithSession(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string session = _params[1]!.AsString();
            bool writeSnapshot = _params[2]!.AsBoolean();
            if (_params[3] is not JArray calls)
                throw new ArgumentException("Calls must be a JSON array of [alias, operation, args?].");
            Signer[]? signers = _params.Count >= 5 && _params[4] is JArray signerArray ? SignersFromJson(signerArray, system.Settings) : null;
            Witness[]? witnesses = _params.Count >= 6 && _params[5] is JArray witnessArray ? WitnessesFromJson(witnessArray) : null;

            byte[] script;
            JArray callDetails = new();
            using (ScriptBuilder sb = new())
            {
                foreach (JToken? callToken in calls)
                {
                    if (callToken is null)
                        throw new ArgumentException("Call entry cannot be null.");
                    if (callToken is not JArray call)
                        throw new ArgumentException("Each call must be [alias, operation, args?].");
                    if (call.Count < 2)
                        throw new ArgumentException("Each call must be [alias, operation, args?].");
                    string alias = call[0]!.AsString();
                    string op = call[1]!.AsString();
                    ContractParameter[] args = System.Array.Empty<ContractParameter>();
                    if (call.Count >= 3 && call[2] != null)
                    {
                        if (call[2] is not JArray arr)
                            throw new ArgumentException("Args must be a JSON array of ContractParameter objects.");
                        args = arr.Select(p => ContractParameter.FromJson((JObject)p!)).ToArray();
                    }
                    UInt160 contractHash = ResolveWorkspaceAlias(workspaceName, alias);
                    callDetails.Add(new JObject { ["alias"] = alias, ["operation"] = op, ["hash"] = contractHash.ToString() });
                    sb.EmitDynamicCall(contractHash, op, args);
                }
                script = sb.ToArray();
            }

            JObject result = GetInvokeResultWithSession(session, writeSnapshot, script, signers, witnesses);
            result["workspace"] = workspaceName;
            result["callcount"] = calls.Count;
            result["calls"] = callDetails;
            return result;
        }

        /// <summary>
        /// Relay an invocation to chain using a workspace alias.
        /// Params: [workspaceName, alias, session|null, operation, args?, signers?]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject RelayInvokeWorkspaceFunction(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string alias = _params[1]!.AsString();
            string? sessionName = _params[2]?.AsString();
            string operation = _params[3]!.AsString();
            ContractParameter[] args = _params.Count >= 5 ? ((JArray)_params[4]!).Select(p => ContractParameter.FromJson((JObject)p!)).ToArray() : System.Array.Empty<ContractParameter>();
            Signer[] signers = _params.Count >= 6 ? SignersFromJson((JArray)_params[5]!, system.Settings) : System.Array.Empty<Signer>();

            UInt160 contractHash = ResolveWorkspaceAlias(workspaceName, alias);
            FairySession? session = sessionName == null ? null : GetOrCreateFairySession(sessionName);
            Wallet wallet = GetSigningWallet(session, allowDefaultWallet: false);

            if (signers.Length == 0)
            {
                signers = new[]
                {
                    new Signer
                    {
                        Account = wallet.GetAccounts().First().ScriptHash,
                        Scopes = WitnessScope.CalledByEntry
                    }
                };
            }

            byte[] script;
            using (ScriptBuilder sb = new())
            {
                script = sb.EmitDynamicCall(contractHash, operation, args).ToArray();
            }

            DataCache snapshot = system.GetSnapshotCache();
            Transaction tx = wallet.MakeTransaction(snapshot, script, sender: signers[0].Account, signers, maxGas: settings.MaxGasInvoke);

            ContractParametersContext context = new(snapshot, tx, system.Settings.Network);
            wallet.Sign(context);
            if (context.Completed)
                tx.Witnesses = context.GetWitnesses();

            system.Blockchain.Tell(tx, ActorRefs.NoSender);

            JObject json = tx.ToJson(system.Settings);
            json["workspace"] = workspaceName;
            json["alias"] = alias;
            json["hash"] = tx.Hash.ToString();
            json["contracthash"] = contractHash.ToString();
            json["networkfee"] = tx.NetworkFee;
            json["sysfee"] = tx.SystemFee;
            json["tx"] = Convert.ToBase64String(tx.ToArray());
            if (!context.Completed)
                json["pendingsignature"] = context.ToJson();
            return json;
        }

        /// <summary>
        /// Relay multiple workspace alias invocations in a single transaction.
        /// Params: [workspaceName, session|null, calls(JArray of [alias, operation, args?]), signers?]
        /// </summary>
        [FairyRpcMethod]
        protected virtual JObject RelayInvokeWorkspaceMany(JArray _params)
        {
            string workspaceName = _params[0]!.AsString();
            string? sessionName = _params[1]?.AsString();
            if (_params[2] is not JArray calls)
                throw new ArgumentException("Calls must be a JSON array of [alias, operation, args?].");
            Signer[] signers = _params.Count >= 4 && _params[3] is JArray signerArray ? SignersFromJson(signerArray, system.Settings) : System.Array.Empty<Signer>();

            FairySession? session = sessionName == null ? null : GetOrCreateFairySession(sessionName);
            Wallet wallet = GetSigningWallet(session, allowDefaultWallet: false);
            if (signers.Length == 0)
            {
                signers = new[]
                {
                    new Signer
                    {
                        Account = wallet.GetAccounts().First().ScriptHash,
                        Scopes = WitnessScope.CalledByEntry
                    }
                };
            }

            byte[] script;
            JArray callDetails = new();
            using (ScriptBuilder sb = new())
            {
                foreach (JToken? callToken in calls)
                {
                    if (callToken is null)
                        throw new ArgumentException("Call entry cannot be null.");
                    if (callToken is not JArray call)
                        throw new ArgumentException("Each call must be [alias, operation, args?].");
                    if (call.Count < 2)
                        throw new ArgumentException("Each call must be [alias, operation, args?].");
                    string alias = call[0]!.AsString();
                    string op = call[1]!.AsString();
                    ContractParameter[] args = call.Count >= 3 && call[2] != null ? ((JArray)call[2]!).Select(p => ContractParameter.FromJson((JObject)p!)).ToArray() : System.Array.Empty<ContractParameter>();
                    UInt160 contractHash = ResolveWorkspaceAlias(workspaceName, alias);
                    callDetails.Add(new JObject { ["alias"] = alias, ["operation"] = op, ["hash"] = contractHash.ToString() });
                    sb.EmitDynamicCall(contractHash, op, args);
                }
                script = sb.ToArray();
            }

            DataCache snapshot = system.GetSnapshotCache();
            Transaction tx = wallet.MakeTransaction(snapshot, script, sender: signers[0].Account, signers, maxGas: settings.MaxGasInvoke);

            ContractParametersContext context = new(snapshot, tx, system.Settings.Network);
            wallet.Sign(context);
            if (context.Completed)
                tx.Witnesses = context.GetWitnesses();

            system.Blockchain.Tell(tx, ActorRefs.NoSender);

            JObject json = tx.ToJson(system.Settings);
            json["workspace"] = workspaceName;
            json["hash"] = tx.Hash.ToString();
            json["networkfee"] = tx.NetworkFee;
            json["sysfee"] = tx.SystemFee;
            json["tx"] = Convert.ToBase64String(tx.ToArray());
            json["callcount"] = calls.Count;
            json["calls"] = callDetails;
            if (!context.Completed)
                json["pendingsignature"] = context.ToJson();
            return json;
        }
    }

    internal sealed class WorkspaceContract
    {
        private readonly byte[] nefBytes;
        private readonly string? dataJson;
        private readonly Signer[]? defaultSigners;
        private NefFile? nef;

        public WorkspaceContract(string alias, byte[] nefBytes, ContractManifest manifest, string? dataJson, Signer[]? defaultSigners)
        {
            Alias = alias;
            this.nefBytes = nefBytes;
            Manifest = manifest;
            this.dataJson = dataJson;
            this.defaultSigners = defaultSigners is { Length: > 0 } ? Fairy.CloneSigners(defaultSigners) : null;
        }

        public string Alias { get; }
        public ContractManifest Manifest { get; }
        public NefFile Nef => nef ??= nefBytes.AsSerializable<NefFile>();
        public bool HasData => dataJson != null;
        public int DefaultSignerCount => defaultSigners?.Length ?? 0;

        public ContractParameter? BuildData() => dataJson == null ? null : ContractParameter.FromJson((JObject)JObject.Parse(dataJson)!);

        public Signer[]? BuildSigners() => defaultSigners == null ? null : Fairy.CloneSigners(defaultSigners);
    }

    internal sealed class WorkspaceDefinition
    {
        private readonly Dictionary<string, WorkspaceContract> contracts = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> order = new();
        private readonly object syncRoot = new();

        public WorkspaceDefinition(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void Upsert(WorkspaceContract contract)
        {
            lock (syncRoot)
            {
                contracts[contract.Alias] = contract;
                if (!order.Any(a => string.Equals(a, contract.Alias, StringComparison.OrdinalIgnoreCase)))
                    order.Add(contract.Alias);
            }
        }

        public bool Remove(string alias)
        {
            lock (syncRoot)
            {
                bool removed = contracts.Remove(alias);
                order.RemoveAll(a => string.Equals(a, alias, StringComparison.OrdinalIgnoreCase));
                return removed;
            }
        }

        public IReadOnlyList<WorkspaceContract> GetContracts(IReadOnlyCollection<string>? aliases)
        {
            lock (syncRoot)
            {
                if (contracts.Count == 0)
                    return Array.Empty<WorkspaceContract>();
                IEnumerable<string> source = aliases == null || aliases.Count == 0 ? order : aliases;
                List<WorkspaceContract> result = new();
                foreach (string alias in source)
                {
                    if (!contracts.TryGetValue(alias, out WorkspaceContract? contract))
                        throw new ArgumentException($"Contract alias `{alias}` not found in workspace `{Name}`.");
                    result.Add(contract);
                }
                return result;
            }
        }

        public JToken ToJson(bool verbose)
        {
            JArray json = new();
            lock (syncRoot)
            {
                foreach (string alias in order)
                {
                    WorkspaceContract contract = contracts[alias];
                    if (verbose)
                    {
                        JObject obj = new()
                        {
                            ["alias"] = contract.Alias,
                            ["manifestname"] = contract.Manifest.Name,
                            ["hasdata"] = contract.HasData,
                            ["signers"] = contract.DefaultSignerCount
                        };
                        json.Add(obj);
                    }
                    else
                    {
                        json.Add(contract.Alias);
                    }
                }
            }
            return json;
        }
    }
}
