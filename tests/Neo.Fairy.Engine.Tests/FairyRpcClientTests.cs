// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Engine;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Neo.Fairy.Engine.Tests;

/// <summary>
/// Integration tests for FairyRpcClient using WireMock to simulate Fairy RPC.
/// </summary>
public class FairyRpcClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly FairyRpcClient _client;

    public FairyRpcClientTests()
    {
        _server = WireMockServer.Start();
        _client = new FairyRpcClient(_server.Url!);
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task PingAsync_WhenServerResponds_ReturnsTrue()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""Hello Fairy!""}"));

        // Act
        var result = await _client.PingAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task PingAsync_WhenServerDown_ReturnsFalse()
    {
        // Arrange - stop the server
        _server.Stop();

        // Act
        var result = await _client.PingAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeFunctionWithSessionAsync_ReturnsExecutionResult()
    {
        // Arrange
        var responseJson = @"{
            ""jsonrpc"": ""2.0"",
            ""id"": 1,
            ""result"": {
                ""state"": ""HALT"",
                ""gasconsumed"": ""1234567"",
                ""stack"": [
                    { ""type"": ""Integer"", ""value"": ""42"" }
                ],
                ""notifications"": [
                    {
                        ""scripthash"": ""0x1234567890abcdef"",
                        ""contractname"": ""TestContract"",
                        ""eventname"": ""Transfer""
                    }
                ]
            }
        }";

        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(responseJson));

        // Act
        var result = await _client.InvokeFunctionWithSessionAsync(
            "test-session",
            "0xcontract",
            "testMethod",
            new object[] { "arg1", 123 },
            true,
            null);

        // Assert
        result.State.Should().Be(ExecutionState.Halt);
        result.GasConsumed.Should().Be(1234567);
        result.IsSuccess.Should().BeTrue();
        result.Stack.Should().HaveCount(1);
        result.Notifications.Should().HaveCount(1);
        result.Notifications[0].EventName.Should().Be("Transfer");
    }

    [Fact]
    public async Task InvokeFunctionWithSessionAsync_WhenFault_ReturnsFailedResult()
    {
        // Arrange
        var responseJson = @"{
            ""jsonrpc"": ""2.0"",
            ""id"": 1,
            ""result"": {
                ""state"": ""FAULT"",
                ""gasconsumed"": ""500000"",
                ""exception"": ""ASSERT failed: Insufficient balance"",
                ""traceback"": ""at Contract.cs:42"",
                ""stack"": [],
                ""notifications"": []
            }
        }";

        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(responseJson));

        // Act
        var result = await _client.InvokeFunctionWithSessionAsync(
            "test-session",
            "0xcontract",
            "transfer",
            new object[] { },
            true,
            null);

        // Assert
        result.State.Should().Be(ExecutionState.Fault);
        result.IsFault.Should().BeTrue();
        result.Exception.Should().Contain("Insufficient balance");
        result.Traceback.Should().Contain("Contract.cs:42");
    }

    [Fact]
    public async Task VirtualDeployAsync_ReturnsDeploymentResult()
    {
        // Arrange
        var responseJson = @"{
            ""jsonrpc"": ""2.0"",
            ""id"": 1,
            ""result"": {
                ""state"": ""HALT"",
                ""gasconsumed"": ""10000000"",
                ""networkfee"": ""5000000"",
                ""test-session"": ""0xdeployedcontracthash""
            }
        }";

        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(responseJson));

        // Act
        var result = await _client.VirtualDeployAsync(
            "test-session",
            new byte[] { 0x00, 0x01, 0x02 },
            @"{""name"":""TestContract""}",
            null,
            null);

        // Assert
        result.State.Should().Be(ExecutionState.Halt);
        result.IsSuccess.Should().BeTrue();
        result.ContractHash.Should().Be("0xdeployedcontracthash");
        result.GasConsumed.Should().Be(10000000);
    }

    [Fact]
    public async Task SetGasBalanceAsync_SendsCorrectRequest()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));

        // Act
        await _client.SetGasBalanceAsync("test-session", "0xaccount", 100_00000000);

        // Assert - verify request was made
        _server.LogEntries.Should().HaveCount(1);
        var request = _server.LogEntries.First();
        request.RequestMessage.Body.Should().Contain("setGasBalance");
        request.RequestMessage.Body.Should().Contain("test-session");
        request.RequestMessage.Body.Should().Contain("0xaccount");
    }

    [Fact]
    public async Task SetTimestampAsync_SendsCorrectRequest()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));

        // Act
        await _client.SetTimestampAsync("test-session", 1700000000000);

        // Assert
        _server.LogEntries.Should().HaveCount(1);
        var request = _server.LogEntries.First();
        request.RequestMessage.Body.Should().Contain("setSnapshotTimestamp");
    }

    [Fact]
    public async Task SetRandomAsync_SendsCorrectRequest()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));

        // Act
        await _client.SetRandomAsync("test-session", 12345);

        // Assert
        _server.LogEntries.Should().HaveCount(1);
        var request = _server.LogEntries.First();
        request.RequestMessage.Body.Should().Contain("setSnapshotRandom");
    }

    [Fact]
    public async Task SetCheckWitnessAsync_SendsCorrectRequest()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));

        // Act
        await _client.SetCheckWitnessAsync("test-session", true);

        // Assert
        _server.LogEntries.Should().HaveCount(1);
        var request = _server.LogEntries.First();
        request.RequestMessage.Body.Should().Contain("setSnapshotCheckWitness");
        request.RequestMessage.Body.Should().Contain("true");
    }
}
