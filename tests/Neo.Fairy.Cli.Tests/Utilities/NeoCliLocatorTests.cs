// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Cli.Utilities;
using FluentAssertions;
using Xunit;

namespace Neo.Fairy.Cli.Tests.Utilities;

public class NeoCliLocatorTests
{
    [Fact]
    public void IsNeoRoot_WithInvalidPath_ReturnsFalse()
    {
        NeoCliLocator.IsNeoRoot(null!).Should().BeFalse();
        NeoCliLocator.IsNeoRoot("").Should().BeFalse();
        NeoCliLocator.IsNeoRoot("   ").Should().BeFalse();
    }

    [Fact]
    public void IsNeoRoot_WithNonExistentPath_ReturnsFalse()
    {
        NeoCliLocator.IsNeoRoot("/nonexistent/path/abc123").Should().BeFalse();
    }

    [Fact]
    public void ResolveNeoCliConfigPath_WithMainnet_ReturnsMainnetConfig()
    {
        // Create temp directory with config files
        var tempDir = Path.Combine(Path.GetTempPath(), "fairy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var configPath = Path.Combine(tempDir, "config.mainnet.json");
            File.WriteAllText(configPath, "{}");

            var result = NeoCliLocator.ResolveNeoCliConfigPath(tempDir, "mainnet");
            result.Should().Be(configPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveNeoCliConfigPath_WithTestnet_ReturnsTestnetConfig()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fairy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var configPath = Path.Combine(tempDir, "config.testnet.json");
            File.WriteAllText(configPath, "{}");

            var result = NeoCliLocator.ResolveNeoCliConfigPath(tempDir, "testnet");
            result.Should().Be(configPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData("private")]
    [InlineData("privatenet")]
    public void ResolveNeoCliConfigPath_WithPrivate_ReturnsDefaultConfig(string network)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fairy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var configPath = Path.Combine(tempDir, "config.json");
            File.WriteAllText(configPath, "{}");

            var result = NeoCliLocator.ResolveNeoCliConfigPath(tempDir, network);
            result.Should().Be(configPath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveNeoCliConfigPath_WithDirectJsonPath_ReturnsDirectPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fairy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var customConfig = Path.Combine(tempDir, "custom.json");
            File.WriteAllText(customConfig, "{}");

            var result = NeoCliLocator.ResolveNeoCliConfigPath(tempDir, customConfig);
            result.Should().Be(customConfig);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveNeoCliConfigPath_WithMissingConfig_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fairy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = NeoCliLocator.ResolveNeoCliConfigPath(tempDir, "mainnet");
            result.Should().BeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindFairyRepoRoot_FromProjectRoot_FindsRoot()
    {
        // This test runs from within the neo-fairy-test repo
        var currentDir = Directory.GetCurrentDirectory();
        var result = NeoCliLocator.FindFairyRepoRoot(currentDir);

        // Should find the repo root since we're running inside it
        if (result != null)
        {
            File.Exists(Path.Combine(result, "src", "Fairy.Plugin", "Fairy.csproj")).Should().BeTrue();
        }
    }

    [Fact]
    public void FindFairyRepoRoot_FromNonFairyDirectory_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fairy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var result = NeoCliLocator.FindFairyRepoRoot(tempDir);
            result.Should().BeNull();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryInferNeoRootFromNeoCliPath_WithInvalidPath_ReturnsNull()
    {
        var result = NeoCliLocator.TryInferNeoRootFromNeoCliPath("/nonexistent/path/neo-cli.dll");
        result.Should().BeNull();
    }
}
