// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Neo.Fairy.Testing.Cheatcodes;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Neo.Fairy.Testing.Tests.Cheatcodes;

public class CheatcodesTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly FairyRpcClient _rpcClient;
    private readonly FairySessionAdapter _session;
    private readonly FairyCheatcodes _cheatcodes;

    public CheatcodesTests()
    {
        _server = WireMockServer.Start();

        // Setup default response for any RPC call
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":{}}"));

        _rpcClient = new FairyRpcClient(_server.Url!);
        _session = new FairySessionAdapter("test-session", _rpcClient);
        _cheatcodes = new FairyCheatcodes(_session, _rpcClient);
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }

    #region Prank Tests

    [Fact]
    public void Prank_SetsPrankAccount()
    {
        // Act
        _cheatcodes.Prank("0x1234");
        var account = _cheatcodes.GetPrankAccount();

        // Assert
        account.Should().Be("0x1234");
    }

    [Fact]
    public void Prank_ClearsAfterSingleUse()
    {
        // Arrange
        _cheatcodes.Prank("0x1234");

        // Act
        var first = _cheatcodes.GetPrankAccount();
        var second = _cheatcodes.GetPrankAccount();

        // Assert
        first.Should().Be("0x1234");
        second.Should().BeNull();
    }

    [Fact]
    public void StartPrank_PersistsUntilStop()
    {
        // Arrange
        _cheatcodes.StartPrank("0x1234");

        // Act
        var first = _cheatcodes.GetPrankAccount();
        var second = _cheatcodes.GetPrankAccount();
        var third = _cheatcodes.GetPrankAccount();

        // Assert
        first.Should().Be("0x1234");
        second.Should().Be("0x1234");
        third.Should().Be("0x1234");
    }

    [Fact]
    public void StopPrank_ClearsPrankAccount()
    {
        // Arrange
        _cheatcodes.StartPrank("0x1234");
        _cheatcodes.GetPrankAccount(); // Use once

        // Act
        _cheatcodes.StopPrank();
        var account = _cheatcodes.GetPrankAccount();

        // Assert
        account.Should().BeNull();
    }

    #endregion

    #region Expectation Tests

    [Fact]
    public void ExpectRevert_SetsExpectation()
    {
        // Act
        _cheatcodes.ExpectRevert();

        // Assert
        _cheatcodes.IsExpectingRevert.Should().BeTrue();
    }

    [Fact]
    public void ExpectRevert_WithMessage_SetsExpectation()
    {
        // Act
        _cheatcodes.ExpectRevert("Insufficient balance");

        // Assert
        _cheatcodes.IsExpectingRevert.Should().BeTrue();
    }

    [Fact]
    public void ValidateExpectations_WhenExpectingRevert_AndSucceeds_Throws()
    {
        // Arrange
        _cheatcodes.ExpectRevert();
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        // Act & Assert
        var action = () => _cheatcodes.ValidateExpectations(result);
        action.Should().Throw<AssertionFailedException>()
            .WithMessage("*Expected revert*");
    }

    [Fact]
    public void ValidateExpectations_WhenExpectingRevert_AndFaults_Passes()
    {
        // Arrange
        _cheatcodes.ExpectRevert();
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Some error"
        };

        // Act & Assert (should not throw)
        _cheatcodes.ValidateExpectations(result);
        _cheatcodes.IsExpectingRevert.Should().BeFalse();
    }

    [Fact]
    public void ValidateExpectations_WhenExpectingRevertWithMessage_AndWrongMessage_Throws()
    {
        // Arrange
        _cheatcodes.ExpectRevert("Insufficient balance");
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Not authorized"
        };

        // Act & Assert
        var action = () => _cheatcodes.ValidateExpectations(result);
        action.Should().Throw<AssertionFailedException>()
            .WithMessage("*Expected revert with*");
    }

    [Fact]
    public void ValidateExpectations_WhenExpectingRevertWithMessage_AndCorrectMessage_Passes()
    {
        // Arrange
        _cheatcodes.ExpectRevert("Insufficient balance");
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Insufficient balance"
        };

        // Act & Assert (should not throw)
        _cheatcodes.ValidateExpectations(result);
    }

    [Fact]
    public void ExpectEmit_ValidatesEventPresence()
    {
        // Arrange
        _cheatcodes.ExpectEmit("Transfer");
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>
            {
                new NotificationInfo { ContractHash = "0x1", EventName = "Transfer" }
            }
        };

        // Act & Assert (should not throw)
        _cheatcodes.ValidateExpectations(result);
    }

    [Fact]
    public void ExpectEmit_WhenEventMissing_Throws()
    {
        // Arrange
        _cheatcodes.ExpectEmit("Transfer");
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>
            {
                new NotificationInfo { ContractHash = "0x1", EventName = "Mint" }
            }
        };

        // Act & Assert
        var action = () => _cheatcodes.ValidateExpectations(result);
        action.Should().Throw<AssertionFailedException>()
            .WithMessage("*Expected event*");
    }

    #endregion

    #region Fuzz Helper Tests

    [Fact]
    public void Assume_WhenTrue_DoesNotThrow()
    {
        // Act & Assert (should not throw)
        _cheatcodes.Assume(true);
        _cheatcodes.Assume(1 == 1);
    }

    [Fact]
    public void Assume_WhenFalse_ThrowsAssumeViolation()
    {
        // Act & Assert
        var action = () => _cheatcodes.Assume(false);
        action.Should().Throw<AssumeViolationException>();
    }

    [Fact]
    public void Bound_ReturnsValueWithinRange()
    {
        // Act
        var result1 = _cheatcodes.Bound(5, 1, 10);
        var result2 = _cheatcodes.Bound(0, 1, 10);
        var result3 = _cheatcodes.Bound(15, 1, 10);

        // Assert
        result1.Should().Be(5);
        result2.Should().Be(1); // Clamped to min
        result3.Should().Be(10); // Clamped to max
    }

    [Fact]
    public void Bound_WorksWithDifferentTypes()
    {
        // Act
        var intResult = _cheatcodes.Bound(50, 0, 100);
        var longResult = _cheatcodes.Bound(50L, 0L, 100L);
        var doubleResult = _cheatcodes.Bound(50.0, 0.0, 100.0);

        // Assert
        intResult.Should().Be(50);
        longResult.Should().Be(50L);
        doubleResult.Should().Be(50.0);
    }

    #endregion

    #region Label Tests

    [Fact]
    public void Label_StoresAndRetrievesLabel()
    {
        // Act
        _cheatcodes.Label("0x1234", "Alice");
        _cheatcodes.Label("0x5678", "Bob");

        // Assert
        _cheatcodes.GetLabel("0x1234").Should().Be("Alice");
        _cheatcodes.GetLabel("0x5678").Should().Be("Bob");
        _cheatcodes.GetLabel("0x9999").Should().BeNull();
    }

    #endregion

    #region Recording Tests

    [Fact]
    public void StartRecording_EnablesRecording()
    {
        // Act
        _cheatcodes.StartRecording();
        _cheatcodes.RecordAccess("0x1234", new byte[] { 1, 2, 3 }, true);
        _cheatcodes.RecordAccess("0x1234", new byte[] { 4, 5, 6 }, false, new byte[] { 7, 8, 9 });
        var accesses = _cheatcodes.StopRecording();

        // Assert
        accesses.Should().HaveCount(2);
        accesses[0].IsRead.Should().BeTrue();
        accesses[1].IsWrite.Should().BeTrue();
        accesses[1].Value.Should().BeEquivalentTo(new byte[] { 7, 8, 9 });
    }

    [Fact]
    public void StopRecording_DisablesRecording()
    {
        // Arrange
        _cheatcodes.StartRecording();
        _cheatcodes.RecordAccess("0x1234", new byte[] { 1 }, true);
        _cheatcodes.StopRecording();

        // Act - record after stopping
        _cheatcodes.RecordAccess("0x1234", new byte[] { 2 }, true);
        _cheatcodes.StartRecording();
        var accesses = _cheatcodes.StopRecording();

        // Assert - should be empty since we started fresh
        accesses.Should().BeEmpty();
    }

    #endregion
}
