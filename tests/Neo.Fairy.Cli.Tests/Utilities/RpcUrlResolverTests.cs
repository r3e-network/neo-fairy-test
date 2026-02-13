// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Cli.Utilities;
using FluentAssertions;
using Xunit;

namespace Neo.Fairy.Cli.Tests.Utilities;

public class RpcUrlResolverTests
{
    private const string DefaultUrl = "http://localhost:16868";
    private const string EnvVarName = "FAIRY_RPC_URL";

    [Fact]
    public void Resolve_WithExplicitUrl_ReturnsExplicitUrl()
    {
        var explicitUrl = "http://custom:8080";
        var result = RpcUrlResolver.Resolve(explicitUrl, null);
        result.Should().Be(explicitUrl);
    }

    [Fact]
    public void Resolve_WithWhitespaceUrl_FallsToDefault()
    {
        var result = RpcUrlResolver.Resolve("   ", null);
        result.Should().Be(DefaultUrl);
    }

    [Fact]
    public void Resolve_WithNullUrl_AndNoEnvVar_ReturnsDefault()
    {
        var originalEnv = Environment.GetEnvironmentVariable(EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, null);
            var result = RpcUrlResolver.Resolve(null, null);
            result.Should().Be(DefaultUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, originalEnv);
        }
    }

    [Fact]
    public void Resolve_WithNullUrl_AndEnvVar_ReturnsEnvVar()
    {
        var originalEnv = Environment.GetEnvironmentVariable(EnvVarName);
        var envUrl = "http://env-url:9999";
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, envUrl);
            var result = RpcUrlResolver.Resolve(null, null);
            result.Should().Be(envUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, originalEnv);
        }
    }

    [Fact]
    public void Resolve_ExplicitUrl_TakesPrecedenceOverEnvVar()
    {
        var originalEnv = Environment.GetEnvironmentVariable(EnvVarName);
        var envUrl = "http://env-url:9999";
        var explicitUrl = "http://explicit:7777";
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, envUrl);
            var result = RpcUrlResolver.Resolve(explicitUrl, null);
            result.Should().Be(explicitUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, originalEnv);
        }
    }

    [Fact]
    public void Resolve_WithCustomDefaultUrl_UsesCustomDefault()
    {
        var originalEnv = Environment.GetEnvironmentVariable(EnvVarName);
        var customDefault = "http://custom-default:5555";
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, null);
            var result = RpcUrlResolver.Resolve(null, null, customDefault);
            result.Should().Be(customDefault);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, originalEnv);
        }
    }
}
