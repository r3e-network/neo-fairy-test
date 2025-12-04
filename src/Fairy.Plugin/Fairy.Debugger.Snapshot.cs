using Neo.Json;

namespace Neo.Plugins
{
    public partial class Fairy
    {
        [FairyRpcMethod]
        protected virtual JToken ListDebugSnapshots(JArray _params)
        {
            JArray session = new JArray();
            foreach (string s in sessionStringToFairySession.Keys)
            {
                if (sessionStringToFairySession[s].debugEngine != null)
                    session.Add(s);
            }
            return session;
        }

        [FairyRpcMethod]
        protected virtual JObject DeleteDebugSnapshots(JArray _params)
        {
            JObject json = new();
            foreach (var s in _params)
            {
                string session = s!.AsString();
                if (TryGetFairySession(session, out FairySession? fairySession) && fairySession.debugEngine != null)
                {
                    json[session] = true;
                    fairySession.debugEngine = null;
                }
                else
                    json[session] = false;
            }
            return json;
        }
    }
}
