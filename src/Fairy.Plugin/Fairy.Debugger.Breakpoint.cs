// Copyright (C) 2015-2025 The Neo Project.
//
// Fairy.Debugger.Breakpoint.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.SmartContract.Native;
using System.Collections.Concurrent;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        readonly ConcurrentDictionary<UInt160, HashSet<uint>> contractScriptHashToAssemblyBreakpoints = new();
        readonly ConcurrentDictionary<UInt160, HashSet<SourceFilenameAndLineNum>> contractScriptHashToSourceCodeBreakpoints = new();

        [FairyRpcMethod]
        protected virtual JToken SetAssemblyBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0]!.AsString());
            //if (!contractScriptHashToInstructionPointerToOpCode.ContainsKey(scriptHash))
            //{
            //    string? contractName = NativeContract.ContractManagement.GetContract(system.StoreView, scriptHash)?.Manifest.Name;
            //    throw new ArgumentException($"Scripthash {scriptHash} {contractName} not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            //}
            var assemblyBreakpoints = contractScriptHashToAssemblyBreakpoints.GetOrAdd(scriptHash, _ => new HashSet<uint>());
            JObject json = new();
            for (int i = 1; i < _params.Count; i++)
            {
                string breakpointInstructionPointerStr = _params[i]!.AsString();
                if (!uint.TryParse(breakpointInstructionPointerStr, out uint breakpointInstructionPointer))
                    throw new ArgumentException($"Invalid instruction pointer: {_params[i]}");
                if (contractScriptHashToInstructionPointerToOpCode.TryGetValue(scriptHash, out Dictionary<uint, VM.OpCode>? instructionPointerToOpCode))
                    // A contract that has registered debuginfo
                    if (!instructionPointerToOpCode.ContainsKey(breakpointInstructionPointer))
                        throw new ArgumentException($"No instruction at InstructionPointer={breakpointInstructionPointer}");
                // For contracts without debug info: breakpoint address validation is skipped.
                // Future enhancement: validate address against NEF script length.
                json[breakpointInstructionPointerStr] = assemblyBreakpoints.Add(breakpointInstructionPointer);
            }
            return json;
        }

        [FairyRpcMethod]
        protected virtual JToken ListAssemblyBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0]!.AsString());
            if (!contractScriptHashToInstructionPointerToOpCode.ContainsKey(scriptHash))
            {
                string? contractName = NativeContract.ContractManagement.GetContract(system.StoreView, scriptHash)?.Manifest.Name;
                throw new ArgumentException($"Scripthash {scriptHash} {contractName} not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            if (!contractScriptHashToAssemblyBreakpoints.TryGetValue(scriptHash, out var bps))
                return new JArray();
            List<uint> assemblyBreakpoints = bps.ToList();
            assemblyBreakpoints.Sort();
            JArray breakpointList = [.. assemblyBreakpoints];
            return breakpointList;
        }

        [FairyRpcMethod]
        protected virtual JToken DeleteAssemblyBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0]!.AsString());
            if (!contractScriptHashToInstructionPointerToOpCode.ContainsKey(scriptHash))
            {
                string? contractName = NativeContract.ContractManagement.GetContract(system.StoreView, scriptHash)?.Manifest.Name;
                throw new ArgumentException($"Scripthash {scriptHash} {contractName} not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            JObject json = new();
            if (!contractScriptHashToAssemblyBreakpoints.TryGetValue(scriptHash, out HashSet<uint>? assemblyBreakpointsSet))
                return json;
            if (_params.Count == 1)  // delete all breakpoints
            {
                List<uint> assemblyBreakpoints = assemblyBreakpointsSet.ToList();
                assemblyBreakpoints.Sort();
                foreach (uint breakpointInstructionPointer in assemblyBreakpoints)
                    json[breakpointInstructionPointer.ToString()] = true;
                assemblyBreakpointsSet.Clear();
            }
            else
                for (int i = 1; i < _params.Count; i++)
                {
                    string breakpointInstructionPointerStr = _params[i]!.AsString();
                    if (!uint.TryParse(breakpointInstructionPointerStr, out uint breakpointInstructionPointer))
                        throw new ArgumentException($"Invalid instruction pointer: {_params[i]}");
                    json[breakpointInstructionPointerStr] = assemblyBreakpointsSet.Remove(breakpointInstructionPointer);
                }
            return json;
        }

        [FairyRpcMethod]
        protected virtual JToken SetSourceCodeBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0]!.AsString());
            if (!contractScriptHashToAllSourceLineNums.TryGetValue(scriptHash, out HashSet<SourceFilenameAndLineNum>? sourceCodeLines))
            {
                string? contractName = NativeContract.ContractManagement.GetContract(system.StoreView, scriptHash)?.Manifest.Name;
                throw new ArgumentException($"Scripthash {scriptHash} {contractName} not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            var sourceCodeBreakpoints = contractScriptHashToSourceCodeBreakpoints.GetOrAdd(scriptHash, _ => new HashSet<SourceFilenameAndLineNum>());
            JArray breakpointList = new();
            int i = 1;
            while (_params.Count > i)
            {
                string sourceCodeFilename = _params[i]!.AsString();
                i++;
                if (!uint.TryParse(_params[i]!.AsString(), out uint sourceCodeBreakpointLineNum))
                    throw new ArgumentException($"Invalid line number: {_params[i]}");
                i++;
                JObject json = new();
                SourceFilenameAndLineNum breakpoint = new SourceFilenameAndLineNum { sourceFilename = sourceCodeFilename, lineNum = sourceCodeBreakpointLineNum };
                if (sourceCodeLines.Contains(breakpoint))
                {
                    sourceCodeBreakpoints.Add(breakpoint);
                    json["filename"] = sourceCodeFilename;
                    json["line"] = sourceCodeBreakpointLineNum;
                    breakpointList.Add(json);
                }
                else
                    throw new ArgumentException($"No code at filename={sourceCodeFilename}, line={sourceCodeBreakpointLineNum}");
            }
            return breakpointList;
        }

        [FairyRpcMethod]
        protected virtual JToken ListSourceCodeBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0]!.AsString());
            if (!contractScriptHashToAllSourceLineNums.ContainsKey(scriptHash))
            {
                string? contractName = NativeContract.ContractManagement.GetContract(system.StoreView, scriptHash)?.Manifest.Name;
                throw new ArgumentException($"Scripthash {scriptHash} {contractName} not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            JArray breakpointList = new();
            if (!contractScriptHashToSourceCodeBreakpoints.TryGetValue(scriptHash, out HashSet<SourceFilenameAndLineNum>? sourceCodeBreakpointsInSet))
                return breakpointList;
            List<SourceFilenameAndLineNum> sourceCodeBreakpoints = sourceCodeBreakpointsInSet.OrderBy(p => p.sourceFilename).ThenBy(p => p.lineNum).ToList();
            foreach (SourceFilenameAndLineNum sourceCodeBreakpointLineNum in sourceCodeBreakpoints)
            {
                JObject breakpoint = new JObject();
                breakpoint["filename"] = sourceCodeBreakpointLineNum.sourceFilename;
                breakpoint["line"] = sourceCodeBreakpointLineNum.lineNum;
                breakpointList.Add(breakpoint);
            }
            return breakpointList;
        }

        [FairyRpcMethod]
        protected virtual JToken DeleteSourceCodeBreakpoints(JArray _params)
        {
            UInt160 scriptHash = UInt160.Parse(_params[0]!.AsString());
            if (!contractScriptHashToAllSourceLineNums.ContainsKey(scriptHash))
            {
                string? contractName = NativeContract.ContractManagement.GetContract(system.StoreView, scriptHash)?.Manifest.Name;
                throw new ArgumentException($"Scripthash {scriptHash} {contractName} not registered for debugging. Call SetDebugInfo(scriptHash, nefDbgNfo, dumpNef) first");
            }
            JArray breakpointList = new();
            if (!contractScriptHashToSourceCodeBreakpoints.TryGetValue(scriptHash, out HashSet<SourceFilenameAndLineNum>? sourceCodeBreakpointsSet))
                return breakpointList;
            if (_params.Count == 1)  // delete all breakpoints
            {
                List<SourceFilenameAndLineNum> sourceCodeBreakpoints = sourceCodeBreakpointsSet.OrderBy(p => p.sourceFilename).ThenBy(p => p.lineNum).ToList();
                foreach (SourceFilenameAndLineNum sourceCodeBreakpointLineNum in sourceCodeBreakpoints)
                {
                    JObject json = new();
                    json["filename"] = sourceCodeBreakpointLineNum.sourceFilename;
                    json["line"] = sourceCodeBreakpointLineNum.lineNum;
                    breakpointList.Add(json);
                }
                sourceCodeBreakpointsSet.Clear();
            }
            else
            {
                HashSet<SourceFilenameAndLineNum> sourceCodeBreakpoints = sourceCodeBreakpointsSet;
                int i = 1;
                while (_params.Count > i)
                {
                    string sourceCodeBreakpointFilename = _params[i]!.AsString();
                    i++;
                    if (!uint.TryParse(_params[i]!.AsString(), out uint sourceCodeBreakpointLineNum))
                        throw new ArgumentException($"Invalid line number: {_params[i]}");
                    i++;
                    if (sourceCodeBreakpoints.Remove(new SourceFilenameAndLineNum { sourceFilename = sourceCodeBreakpointFilename, lineNum = sourceCodeBreakpointLineNum }))
                    {
                        JObject json = new();
                        json["filename"] = sourceCodeBreakpointFilename;
                        json["line"] = sourceCodeBreakpointLineNum;
                        breakpointList.Add(json);
                    }
                }
            }
            return breakpointList;
        }
    }
}
