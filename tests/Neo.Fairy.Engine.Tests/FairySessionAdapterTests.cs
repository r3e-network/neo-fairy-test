// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Engine;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Neo.Fairy.Engine.Tests;

public class FairySessionAdapterTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly FairyRpcClient _client;

    public FairySessionAdapterTests()
    {
        _server = WireMockServer.Start();
        _client = new FairyRpcClient(_server.Url!);

        // Setup default response for any RPC call
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        // Act
        var session = new FairySessionAdapter("test-session", _client);

        // Assert
        session.Id.Should().Be("test-session");
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RegisterContract_StoresContractHash()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);

        // Act
        session.RegisterContract("token", "0x1234567890abcdef");
        session.RegisterContract("router", "0xfedcba0987654321");

        // Assert
        session.GetContractHash("token").Should().Be("0x1234567890abcdef");
        session.GetContractHash("router").Should().Be("0xfedcba0987654321");
        session.GetContractHash("unknown").Should().BeNull();
    }

    [Fact]
    public void RegisterContract_IsCaseInsensitive()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);

        // Act
        session.RegisterContract("Token", "0x1234");

        // Assert
        session.GetContractHash("token").Should().Be("0x1234");
        session.GetContractHash("TOKEN").Should().Be("0x1234");
        session.GetContractHash("ToKeN").Should().Be("0x1234");
    }

    [Fact]
    public void Touch_UpdatesLastActivityAt()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);
        var initialTime = session.LastActivityAt;

        // Act
        Thread.Sleep(10);
        session.Touch();

        // Assert
        session.LastActivityAt.Should().BeAfter(initialTime);
    }

    [Fact]
    public void CreateSnapshot_ReturnsUniqueId()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);

        // Act
        var snap1 = session.CreateSnapshot();
        var snap2 = session.CreateSnapshot();
        var snap3 = session.CreateSnapshot();

        // Assert
        snap1.Should().NotBe(snap2);
        snap2.Should().NotBe(snap3);
        snap1.Should().Contain("test-session");
    }

    [Fact]
    public void RevertToSnapshot_ReturnsTrueForValidSnapshot()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);
        var snapshotId = session.CreateSnapshot();

        // Act
        var result = session.RevertToSnapshot(snapshotId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RevertToSnapshot_ReturnsFalseForInvalidSnapshot()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);

        // Act
        var result = session.RevertToSnapshot("invalid-snapshot-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Clone_CreatesNewSessionWithSameContracts()
    {
        // Arrange
        var session = new FairySessionAdapter("original", _client);
        session.RegisterContract("token", "0x1234");
        session.RegisterContract("router", "0x5678");

        // Act
        var cloned = (FairySessionAdapter)session.Clone("cloned");

        // Assert
        cloned.Id.Should().Be("cloned");
        cloned.GetContractHash("token").Should().Be("0x1234");
        cloned.GetContractHash("router").Should().Be("0x5678");
    }

    [Fact]
    public void GetAllContracts_ReturnsAllRegisteredContracts()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);
        session.RegisterContract("token", "0x1234");
        session.RegisterContract("router", "0x5678");
        session.RegisterContract("pool", "0x9abc");

        // Act
        var contracts = session.GetAllContracts();

        // Assert
        contracts.Should().HaveCount(3);
        contracts.Should().ContainKey("token");
        contracts.Should().ContainKey("router");
        contracts.Should().ContainKey("pool");
    }

    [Fact]
    public void Dispose_ClearsState()
    {
        // Arrange
        var session = new FairySessionAdapter("test-session", _client);
        session.RegisterContract("token", "0x1234");
        session.CreateSnapshot();

        // Act
        session.Dispose();

        // Assert
        session.GetAllContracts().Should().BeEmpty();
    }
}

public class FairySessionFactoryTests : IDisposable
{
    private readonly WireMockServer _server;

    public FairySessionFactoryTests()
    {
        _server = WireMockServer.Start();

        // Setup default response
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public void CreateSession_ReturnsNewSession()
    {
        // Arrange
        var factory = new FairySessionFactory(_server.Url!);

        // Act
        var session = factory.CreateSession();

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().StartWith("fairy_session_");
    }

    [Fact]
    public void CreateSession_WithId_UsesProvidedId()
    {
        // Arrange
        var factory = new FairySessionFactory(_server.Url!);

        // Act
        var session = factory.CreateSession("my-custom-session");

        // Assert
        session.Id.Should().Be("my-custom-session");
    }

    [Fact]
    public void CreateSession_GeneratesUniqueIds()
    {
        // Arrange
        var factory = new FairySessionFactory(_server.Url!);

        // Act
        var session1 = factory.CreateSession();
        var session2 = factory.CreateSession();
        var session3 = factory.CreateSession();

        // Assert
        session1.Id.Should().NotBe(session2.Id);
        session2.Id.Should().NotBe(session3.Id);
    }

    [Fact]
    public void RpcClient_IsAccessible()
    {
        // Arrange
        var factory = new FairySessionFactory(_server.Url!);

        // Assert
        factory.RpcClient.Should().NotBeNull();
    }
}
