using Neo.Fairy.Core.Configuration;
using Xunit;

namespace Neo.Fairy.Core.Tests.Configuration;

public class NetworkResolverTests
{
    private static IDisposable SuppressFairyEnv()
    {
        var keys = new[]
        {
            "FAIRY_RPC_URL",
            "FAIRY_MAINNET_RPC",
            "FAIRY_TESTNET_RPC",
            "FAIRY_EXPRESS_RPC"
        };

        var previous = keys.ToDictionary(k => k, k => Environment.GetEnvironmentVariable(k));
        foreach (var k in keys)
        {
            Environment.SetEnvironmentVariable(k, null);
        }

        return new DisposableAction(() =>
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        });
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _dispose;

        public DisposableAction(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose() => _dispose();
    }

    [Fact]
    public void Resolves_Mainnet_Defaults()
    {
        using var _ = SuppressFairyEnv();
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("mainnet", cfg);
        Assert.Equal("mainnet", name);
        Assert.Equal(cfg.RpcUrl, rpc);
    }

    [Fact]
    public void Resolves_Testnet_Defaults()
    {
        using var _ = SuppressFairyEnv();
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("testnet", cfg);
        Assert.Equal("testnet", name);
        Assert.Equal(cfg.RpcUrl, rpc);
    }

    [Fact]
    public void Resolves_NeoExpress_Default()
    {
        using var _ = SuppressFairyEnv();
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("neo-express", cfg);
        Assert.Equal("neo-express", name);
        Assert.Contains("localhost", rpc);
    }

    [Fact]
    public void Resolves_CustomRpc_ExplicitUrl()
    {
        using var _ = SuppressFairyEnv();
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("http://custom:30333", cfg);
        Assert.Equal("http://custom:30333", name);
        Assert.Equal("http://custom:30333", rpc);
    }

    [Fact]
    public void Uses_Config_Overrides()
    {
        using var _ = SuppressFairyEnv();
        var cfg = new FairyRuntimeConfig
        {
            MainnetRpcUrl = "https://mainnet.example.org",
            TestnetRpcUrl = "https://testnet.example.org",
            NeoExpressRpcUrl = "http://neoexpress.example.org"
        };

        var (_, mainnetRpc) = NetworkResolver.Resolve("mainnet", cfg);
        var (_, testnetRpc) = NetworkResolver.Resolve("testnet", cfg);
        var (_, expressRpc) = NetworkResolver.Resolve("neo-express", cfg);

        Assert.Equal("https://mainnet.example.org", mainnetRpc);
        Assert.Equal("https://testnet.example.org", testnetRpc);
        Assert.Equal("http://neoexpress.example.org", expressRpc);
    }

    [Fact]
    public void Uses_FairyRpcUrl_As_GlobalFallback()
    {
        using var _ = SuppressFairyEnv();

        Environment.SetEnvironmentVariable("FAIRY_RPC_URL", "http://fairy.local:16868");
        var cfg = new FairyRuntimeConfig();

        var (_, mainnetRpc) = NetworkResolver.Resolve("mainnet", cfg);
        var (_, testnetRpc) = NetworkResolver.Resolve("testnet", cfg);
        var (_, otherRpc) = NetworkResolver.Resolve("private", cfg);

        Assert.Equal("http://fairy.local:16868", mainnetRpc);
        Assert.Equal("http://fairy.local:16868", testnetRpc);
        Assert.Equal("http://fairy.local:16868", otherRpc);
    }
}
