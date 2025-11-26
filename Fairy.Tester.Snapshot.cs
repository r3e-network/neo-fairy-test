// Copyright (C) 2015-2025 The Neo Project.
//
// Fairy.Tester.Snapshot.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System.Collections.Concurrent;

namespace Neo.Plugins
{
    #pragma warning disable CS8601
    public partial class Fairy
    {
        public readonly ConcurrentDictionary<string, FairySession> sessionStringToFairySession = new();

        public FairySession GetOrCreateFairySession(string session)
        {
            NeoSystem sys = system ?? throw new InvalidOperationException("System not initialized.");
            var value = sessionStringToFairySession.GetOrAdd(session, _ => NewFairySession(sys, this));
            return value!;
        }

        private bool TryGetFairySession(string session, out FairySession fairySession)
        {
            return sessionStringToFairySession.TryGetValue(session, out fairySession);
        }

        private FairyEngine BuildSnapshotWithDummyScript(FairyEngine? engine = null)
        {
            return FairyEngine.Run(new byte[] { 0x40 }, engine != null ? engine.SnapshotCache.CloneCache() : system.StoreView, this, settings: system.Settings, gas: settings.MaxGasInvoke, oldEngine: engine, copyRuntimeArgs: true);
        }

        [FairyRpcMethod]
        protected virtual JToken NewSnapshotsFromCurrentSystem(JArray _params)
        {
            JObject json = new();
            foreach (var param in _params)
            {
                string session = param!.AsString();
                if (TryGetFairySession(session, out _))
                    json[session] = true;
                else
                    json[session] = false;
                sessionStringToFairySession[session] = NewFairySession(system, this);
            }
            return json;
        }

        [FairyRpcMethod]
        protected virtual JToken DeleteSnapshots(JArray _params)
        {
            JObject json = new();
            foreach (var s in _params)
            {
                string str = s!.AsString();
                json[str] = sessionStringToFairySession.TryRemove(str, out _);
            }
            return json;
        }

        [FairyRpcMethod]
        protected virtual JToken ListSnapshots(JArray _params)
        {
            JArray session = new JArray();
            foreach (string s in sessionStringToFairySession.Keys)
            {
                session.Add(s);
            }
            return session;
        }

        [FairyRpcMethod]
        protected virtual JToken RenameSnapshot(JArray _params)
        {
            string from = _params[0]!.AsString();
            string to = _params[1]!.AsString();
            if (!TryGetFairySession(from, out FairySession? source))
                throw new ArgumentException($"Snapshot `{from}` not found.");
            sessionStringToFairySession[to] = source;
            sessionStringToFairySession.TryRemove(from, out _);
            JObject json = new();
            json[to] = from;
            return json;
        }

        [FairyRpcMethod]
        protected virtual JToken CopySnapshot(JArray _params)
        {
            string from = _params[0]!.AsString();
            string to = _params[1]!.AsString();
            if (!TryGetFairySession(from, out FairySession? source))
                throw new ArgumentException($"Snapshot `{from}` not found.");
            FairySession testSessionTo = NewFairySession(system, this);
            testSessionTo.engine = BuildSnapshotWithDummyScript(source.engine);
            testSessionTo.debugEngine = null;
            sessionStringToFairySession[to] = testSessionTo;
            JObject json = new();
            json[to] = from;
            return json;
        }
    }
}
#pragma warning restore CS8601
