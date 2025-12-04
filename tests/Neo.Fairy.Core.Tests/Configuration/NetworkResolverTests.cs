using Neo.Fairy.Core.Configuration;
using Xunit;

namespace Neo.Fairy.Core.Tests.Configuration;

public class NetworkResolverTests
{
    [Fact]
    public void Resolves_Mainnet_Defaults()
    {
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("mainnet", cfg);
        Assert.Equal("mainnet", name);
        Assert.Contains("10331", rpc);
    }

    [Fact]
    public void Resolves_Testnet_Defaults()
    {
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("testnet", cfg);
        Assert.Equal("testnet", name);
        Assert.Contains("10331", rpc);
    }

    [Fact]
    public void Resolves_NeoExpress_Default()
    {
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("neo-express", cfg);
        Assert.Equal("neo-express", name);
        Assert.Contains("localhost", rpc);
    }

    [Fact]
    public void Resolves_CustomRpc_ExplicitUrl()
    {
        var cfg = new FairyRuntimeConfig();
        var (name, rpc) = NetworkResolver.Resolve("http://custom:30333", cfg);
        Assert.Equal("http://custom:30333", name);
        Assert.Equal("http://custom:30333", rpc);
    }

    [Fact]
    public void Uses_Config_Overrides()
    {
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
}
