// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Configuration;
using Xunit;

namespace Neo.Fairy.Core.Tests.Configuration;

public class FairyConfigTests
{
    [Fact]
    public void Parse_ValidToml_ReturnsConfig()
    {
        // Arrange
        var toml = """
            [project]
            name = "my-dex"
            version = "1.0.0"
            src = "contracts"
            test = "tests"
            script = "scripts"
            out = "build"

            [compiler]
            path = "nccs"
            debug = true
            assembly = true
            optimize = false

            [fairy]
            rpc_url = "http://localhost:16868"
            network = "testnet"
            gas_limit = 100
            session_timeout = 3600

            [deploy]
            default_wallet = "test.json"
            verify = false

            [test]
            verbosity = 3
            coverage = true
            parallel = false
            fail_fast = true
            fuzz_runs = 128

            [[contracts]]
            name = "Token"
            path = "contracts/Token.cs"
            alias = "token"

            [[contracts]]
            name = "Router"
            path = "contracts/Router.cs"
            alias = "router"
            depends = ["token"]
            """;

        // Act
        var config = FairyConfig.Parse(toml);

        // Assert
        config.Project.Name.Should().Be("my-dex");
        config.Project.Version.Should().Be("1.0.0");
        config.Project.Src.Should().Be("contracts");
        config.Project.Test.Should().Be("tests");
        config.Project.Script.Should().Be("scripts");
        config.Project.Out.Should().Be("build");

        config.Compiler.Path.Should().Be("nccs");
        config.Compiler.Debug.Should().BeTrue();
        config.Compiler.Assembly.Should().BeTrue();
        config.Compiler.Optimize.Should().BeFalse();

        config.Fairy.RpcUrl.Should().Be("http://localhost:16868");
        config.Fairy.Network.Should().Be("testnet");
        config.Fairy.GasLimit.Should().Be(100);
        config.Fairy.SessionTimeout.Should().Be(3600);

        config.Deploy.DefaultWallet.Should().Be("test.json");
        config.Deploy.Verify.Should().BeFalse();

        config.Test.Verbosity.Should().Be(3);
        config.Test.Coverage.Should().BeTrue();
        config.Test.Parallel.Should().BeFalse();
        config.Test.FailFast.Should().BeTrue();
        config.Test.FuzzRuns.Should().Be(128);

        config.Contracts.Should().HaveCount(2);
        config.Contracts[0].Name.Should().Be("Token");
        config.Contracts[0].Alias.Should().Be("token");
        config.Contracts[1].Name.Should().Be("Router");
        config.Contracts[1].Depends.Should().Contain("token");
    }

    [Fact]
    public void Parse_MinimalToml_UsesDefaults()
    {
        // Arrange
        var toml = """
            [project]
            name = "minimal"
            """;

        // Act
        var config = FairyConfig.Parse(toml);

        // Assert
        config.Project.Name.Should().Be("minimal");
        config.Project.Version.Should().Be("1.0.0");
        config.Project.Src.Should().Be("src");
        config.Project.Test.Should().Be("test");
        config.Project.Out.Should().Be("out");

        config.Compiler.Path.Should().Be("nccs");
        config.Fairy.RpcUrl.Should().Be("http://localhost:16868");
        config.Test.FuzzRuns.Should().Be(256);
    }

    [Fact]
    public void CreateDefault_ReturnsValidConfig()
    {
        // Act
        var config = FairyConfig.CreateDefault("test-project");

        // Assert
        config.Project.Name.Should().Be("test-project");
        config.Contracts.Should().HaveCount(1);
        config.Contracts[0].Name.Should().Be("Counter");
    }

    [Fact]
    public void ToToml_RoundTrip_PreservesValues()
    {
        // Arrange
        var original = FairyConfig.CreateDefault("roundtrip-test");
        original.Project.Version = "2.0.0";
        original.Fairy.Network = "mainnet";
        original.Test.FuzzRuns = 512;

        // Act
        var toml = original.ToToml();
        var parsed = FairyConfig.Parse(toml);

        // Assert
        parsed.Project.Name.Should().Be(original.Project.Name);
        parsed.Project.Version.Should().Be("2.0.0");
        parsed.Fairy.Network.Should().Be("mainnet");
        parsed.Test.FuzzRuns.Should().Be(512);
    }

    [Fact]
    public void Parse_EmptyToml_ReturnsDefaults()
    {
        // Arrange
        var toml = "";

        // Act
        var config = FairyConfig.Parse(toml);

        // Assert
        config.Project.Name.Should().Be("unnamed");
        config.Compiler.Path.Should().Be("nccs");
    }
}
