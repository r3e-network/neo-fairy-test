// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Neo.Fairy.Testing;
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

    #region Mock Call Tests

    [Fact]
    public void MockCall_StoresMockData()
    {
        // Act
        _cheatcodes.MockCall("0x1234", "balanceOf", 1000L);
        var (isMocked, returnData, shouldRevert, revertMessage) = _cheatcodes.GetMock("0x1234", "balanceOf");

        // Assert
        isMocked.Should().BeTrue();
        returnData.Should().Be(1000L);
        shouldRevert.Should().BeFalse();
        revertMessage.Should().BeNull();
    }

    [Fact]
    public void MockCallRevert_StoresRevertInfo()
    {
        // Act
        _cheatcodes.MockCallRevert("0x1234", "transfer", "Insufficient balance");
        var (isMocked, returnData, shouldRevert, revertMessage) = _cheatcodes.GetMock("0x1234", "transfer");

        // Assert
        isMocked.Should().BeTrue();
        returnData.Should().BeNull();
        shouldRevert.Should().BeTrue();
        revertMessage.Should().Be("Insufficient balance");
    }

    [Fact]
    public void MockCallRevert_WithoutMessage_StoresNullMessage()
    {
        // Act
        _cheatcodes.MockCallRevert("0x1234", "transfer");
        var (isMocked, _, shouldRevert, revertMessage) = _cheatcodes.GetMock("0x1234", "transfer");

        // Assert
        isMocked.Should().BeTrue();
        shouldRevert.Should().BeTrue();
        revertMessage.Should().BeNull();
    }

    [Fact]
    public void ClearMockedCalls_ClearsAllMocks()
    {
        // Arrange
        _cheatcodes.MockCall("0x1234", "balanceOf", 1000L);
        _cheatcodes.MockCallRevert("0x5678", "transfer");

        // Act
        _cheatcodes.ClearMockedCalls();
        var (isMocked1, _, _, _) = _cheatcodes.GetMock("0x1234", "balanceOf");
        var (isMocked2, _, _, _) = _cheatcodes.GetMock("0x5678", "transfer");

        // Assert
        isMocked1.Should().BeFalse();
        isMocked2.Should().BeFalse();
    }

    [Fact]
    public void ClearMockedCalls_WithTarget_ClearsOnlyTargetMocks()
    {
        // Arrange
        _cheatcodes.MockCall("0x1234", "balanceOf", 1000L);
        _cheatcodes.MockCall("0x5678", "totalSupply", 50000L);

        // Act
        _cheatcodes.ClearMockedCalls("0x1234");
        var (isMocked1, _, _, _) = _cheatcodes.GetMock("0x1234", "balanceOf");
        var (isMocked2, returnData, _, _) = _cheatcodes.GetMock("0x5678", "totalSupply");

        // Assert
        isMocked1.Should().BeFalse();
        isMocked2.Should().BeTrue();
        returnData.Should().Be(50000L);
    }

    [Fact]
    public void GetMock_WhenNotMocked_ReturnsFalse()
    {
        // Act
        var (isMocked, returnData, shouldRevert, revertMessage) = _cheatcodes.GetMock("0x9999", "unknownMethod");

        // Assert
        isMocked.Should().BeFalse();
        returnData.Should().BeNull();
        shouldRevert.Should().BeFalse();
        revertMessage.Should().BeNull();
    }

    #endregion

    #region Broadcast Mode Tests

    [Fact]
    public void StartBroadcast_EnablesBroadcastMode()
    {
        // Act
        _cheatcodes.StartBroadcast();

        // Assert
        _cheatcodes.IsBroadcasting.Should().BeTrue();
        _cheatcodes.BroadcastSender.Should().BeNull();
    }

    [Fact]
    public void StartBroadcast_WithSender_SetsSender()
    {
        // Act
        _cheatcodes.StartBroadcast("0x1234");

        // Assert
        _cheatcodes.IsBroadcasting.Should().BeTrue();
        _cheatcodes.BroadcastSender.Should().Be("0x1234");
    }

    [Fact]
    public void StopBroadcast_DisablesBroadcastMode()
    {
        // Arrange
        _cheatcodes.StartBroadcast("0x1234");
        _cheatcodes.RecordBroadcastTx("0xabc");
        _cheatcodes.RecordBroadcastTx("0xdef");

        // Act
        var txs = _cheatcodes.StopBroadcast();

        // Assert
        _cheatcodes.IsBroadcasting.Should().BeFalse();
        _cheatcodes.BroadcastSender.Should().BeNull();
        txs.Should().HaveCount(2);
        txs.Should().Contain("0xabc");
        txs.Should().Contain("0xdef");
    }

    [Fact]
    public void RecordBroadcastTx_OnlyRecordsWhenBroadcasting()
    {
        // Act - record without broadcasting
        _cheatcodes.RecordBroadcastTx("0xabc");
        _cheatcodes.StartBroadcast();
        _cheatcodes.RecordBroadcastTx("0xdef");
        var txs = _cheatcodes.StopBroadcast();

        // Assert
        txs.Should().HaveCount(1);
        txs.Should().Contain("0xdef");
    }

    #endregion

    #region Environment Variable Tests

    [Fact]
    public void SetEnv_StoresValue()
    {
        // Act
        _cheatcodes.SetEnv("TEST_KEY", "test_value");
        var result = _cheatcodes.GetEnv("TEST_KEY");

        // Assert
        result.Should().Be("test_value");
    }

    [Fact]
    public void GetEnv_WhenNotSet_ReturnsNull()
    {
        // Act
        var result = _cheatcodes.GetEnv("NONEXISTENT_KEY_12345");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetEnvOr_WhenSet_ReturnsValue()
    {
        // Arrange
        _cheatcodes.SetEnv("MY_VAR", "my_value");

        // Act
        var result = _cheatcodes.GetEnvOr("MY_VAR", "default");

        // Assert
        result.Should().Be("my_value");
    }

    [Fact]
    public void GetEnvOr_WhenNotSet_ReturnsDefault()
    {
        // Act
        var result = _cheatcodes.GetEnvOr("NONEXISTENT_VAR", "default_value");

        // Assert
        result.Should().Be("default_value");
    }

    #endregion

    #region Block Manipulation Tests

    [Fact]
    public void Roll_SetsBlockNumber()
    {
        // Act
        _cheatcodes.Roll(12345);
        var blockNumber = _cheatcodes.GetBlockNumber();

        // Assert
        blockNumber.Should().Be(12345);
    }

    [Fact]
    public void GetBlockNumber_WhenNotSet_ReturnsZero()
    {
        // Act - use fresh cheatcodes
        var freshSession = new FairySessionAdapter("fresh-session", _rpcClient);
        var freshCheatcodes = new FairyCheatcodes(freshSession, _rpcClient);
        var blockNumber = freshCheatcodes.GetBlockNumber();

        // Assert
        blockNumber.Should().Be(0);
    }

    [Fact]
    public void ChainId_ReturnsNetworkMagic()
    {
        // Act
        var chainId = _cheatcodes.ChainId();

        // Assert - should return PrivateNetMagic as default
        chainId.Should().Be(TestDefaults.PrivateNetMagic);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public void Log_RecordsMessage()
    {
        // Act
        _cheatcodes.Log("Test message 1");
        _cheatcodes.Log("Test message 2");
        var logs = _cheatcodes.GetLogs();

        // Assert
        logs.Should().HaveCount(2);
        logs[0].Should().Contain("Test message 1");
        logs[1].Should().Contain("Test message 2");
    }

    [Fact]
    public void Log_WithFormat_FormatsMessage()
    {
        // Act
        _cheatcodes.Log("Value: {0}, Count: {1}", 42, 10);
        var logs = _cheatcodes.GetLogs();

        // Assert
        logs.Should().HaveCount(1);
        logs[0].Should().Contain("Value: 42, Count: 10");
    }

    [Fact]
    public void ClearLogs_RemovesAllLogs()
    {
        // Arrange
        _cheatcodes.Log("Test message");

        // Act
        _cheatcodes.ClearLogs();
        var logs = _cheatcodes.GetLogs();

        // Assert
        logs.Should().BeEmpty();
    }

    #endregion

    #region ExpectCall Tests

    [Fact]
    public void ExpectCall_AddsToExpectedList()
    {
        // Act
        _cheatcodes.ExpectCall("0x1234", "transfer");
        _cheatcodes.ExpectCall("0x5678", "balanceOf");

        // Assert - no assertion available publicly, but verify it doesn't throw
        // The validation would happen during actual execution
    }

    [Fact]
    public void ExpectCallCount_SetsExpectedCount()
    {
        // Act
        _cheatcodes.ExpectCallCount("0x1234", "transfer", 5);

        // Assert - no public accessor, but verify it doesn't throw
    }

    [Fact]
    public void ValidateExpectations_ExpectCall_MatchingCall_Passes()
    {
        // Arrange
        _cheatcodes.ExpectCall("0x1234", "transfer");
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        // Act & Assert (should not throw)
        _cheatcodes.ValidateExpectations(result, "0x1234", "transfer");
    }

    [Fact]
    public void ValidateExpectations_ExpectCall_MismatchingCall_Throws()
    {
        // Arrange
        _cheatcodes.ExpectCall("0x1234", "transfer");
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        // Act & Assert
        var action = () => _cheatcodes.ValidateExpectations(result, "0x1234", "balanceOf");
        action.Should().Throw<AssertionFailedException>()
            .WithMessage("*Expected call to 0x1234.transfer()*");
    }

    [Fact]
    public void ValidateFinalExpectations_UnconsumedExpectCall_Throws()
    {
        // Arrange
        _cheatcodes.ExpectCall("0x1234", "transfer");

        // Act & Assert
        var action = () => _cheatcodes.ValidateFinalExpectations();
        action.Should().Throw<AssertionFailedException>()
            .WithMessage("*Expected calls were never made*");
    }

    [Fact]
    public void ValidateFinalExpectations_AllCallsConsumed_Passes()
    {
        // Arrange
        _cheatcodes.ExpectCall("0x1234", "transfer");
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };
        _cheatcodes.ValidateExpectations(result, "0x1234", "transfer");

        // Act & Assert (should not throw)
        _cheatcodes.ValidateFinalExpectations();
    }

    [Fact]
    public void ValidateFinalExpectations_ExpectCallCount_CorrectCount_Passes()
    {
        // Arrange
        _cheatcodes.ExpectCallCount("0x1234", "transfer", 2);
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };
        _cheatcodes.ValidateExpectations(result, "0x1234", "transfer");
        _cheatcodes.ValidateExpectations(result, "0x1234", "transfer");

        // Act & Assert (should not throw)
        _cheatcodes.ValidateFinalExpectations();
    }

    [Fact]
    public void ValidateFinalExpectations_ExpectCallCount_WrongCount_Throws()
    {
        // Arrange
        _cheatcodes.ExpectCallCount("0x1234", "transfer", 3);
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };
        _cheatcodes.ValidateExpectations(result, "0x1234", "transfer");

        // Act & Assert
        var action = () => _cheatcodes.ValidateFinalExpectations();
        action.Should().Throw<AssertionFailedException>()
            .WithMessage("*Expected 3 call(s) to 0x1234.transfer()*got 1*");
    }

    #endregion

    #region Snapshot Tests

    [Fact]
    public void Snapshot_ReturnsValidId()
    {
        // Arrange - Setup mock response for snapshot
        _server.Given(Request.Create().WithPath("/").UsingPost()
            .WithBody(new WireMock.Matchers.JsonPartialMatcher(new { method = "virtualsnapshot" })))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""snapshot_123""}"));

        // Act
        var snapshotId = _cheatcodes.Snapshot();

        // Assert
        snapshotId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RevertTo_WithValidSnapshot_ReturnsTrue()
    {
        // Arrange - Setup mock responses
        _server.Given(Request.Create().WithPath("/").UsingPost()
            .WithBody(new WireMock.Matchers.JsonPartialMatcher(new { method = "virtualsnapshot" })))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":""snapshot_456""}"));

        _server.Given(Request.Create().WithPath("/").UsingPost()
            .WithBody(new WireMock.Matchers.JsonPartialMatcher(new { method = "virtualrevert" })))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":true}"));

        var snapshotId = _cheatcodes.Snapshot();

        // Act
        var result = _cheatcodes.RevertTo(snapshotId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RevertTo_WithInvalidSnapshot_ReturnsFalse()
    {
        // Arrange - Setup mock response for invalid revert
        _server.Given(Request.Create().WithPath("/").UsingPost()
            .WithBody(new WireMock.Matchers.JsonPartialMatcher(new { method = "virtualrevert" })))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(@"{""jsonrpc"":""2.0"",""id"":1,""result"":false}"));

        // Act
        var result = _cheatcodes.RevertTo("nonexistent_snapshot");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Fork Tests

    [Fact]
    public void CreateFork_ReturnsValidForkId()
    {
        // Act
        var forkId = _cheatcodes.CreateFork("http://localhost:10332");

        // Assert
        forkId.Should().NotBeNullOrEmpty();
        forkId.Should().StartWith("fork_");
    }

    [Fact]
    public void CreateFork_WithBlockNumber_StoresBlockNumber()
    {
        // Act
        var forkId = _cheatcodes.CreateFork("http://localhost:10332", 1000);
        var forkInfo = _cheatcodes.GetForkInfo(forkId);

        // Assert
        forkInfo.Should().NotBeNull();
        forkInfo!.BlockNumber.Should().Be(1000);
        forkInfo.RpcUrl.Should().Be("http://localhost:10332");
    }

    [Fact]
    public void CreateFork_AutoSelectsFirstFork()
    {
        // Act
        var forkId = _cheatcodes.CreateFork("http://localhost:10332");

        // Assert
        _cheatcodes.ActiveFork().Should().Be(forkId);
    }

    [Fact]
    public void SelectFork_SwitchesActiveFork()
    {
        // Arrange
        var fork1 = _cheatcodes.CreateFork("http://localhost:10332");
        var fork2 = _cheatcodes.CreateFork("http://localhost:10333");

        // Act
        _cheatcodes.SelectFork(fork1);
        var activeFork = _cheatcodes.ActiveFork();

        // Assert
        activeFork.Should().Be(fork1);
    }

    [Fact]
    public void SelectFork_WithInvalidId_Throws()
    {
        // Act & Assert
        var action = () => _cheatcodes.SelectFork("nonexistent_fork");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Fact]
    public void RollFork_UpdatesBlockNumber()
    {
        // Arrange
        var forkId = _cheatcodes.CreateFork("http://localhost:10332", 100);

        // Act
        _cheatcodes.RollFork(forkId, 500);
        var forkInfo = _cheatcodes.GetForkInfo(forkId);

        // Assert
        forkInfo!.BlockNumber.Should().Be(500);
    }

    [Fact]
    public void RollFork_WithoutId_UsesActiveFork()
    {
        // Arrange
        var forkId = _cheatcodes.CreateFork("http://localhost:10332", 100);

        // Act
        _cheatcodes.RollFork(200);
        var forkInfo = _cheatcodes.GetForkInfo(forkId);

        // Assert
        forkInfo!.BlockNumber.Should().Be(200);
    }

    [Fact]
    public void RollFork_WithoutActiveFork_Throws()
    {
        // Arrange - use fresh cheatcodes without any fork
        var freshSession = new FairySessionAdapter("fresh-session", _rpcClient);
        var freshCheatcodes = new FairyCheatcodes(freshSession, _rpcClient);

        // Act & Assert
        var action = () => freshCheatcodes.RollFork(100);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*No active fork*");
    }

    [Fact]
    public void MakePersistent_MarksContractsAsPersistent()
    {
        // Act
        _cheatcodes.MakePersistent("0x1234", "0x5678");

        // Assert
        _cheatcodes.IsPersistent("0x1234").Should().BeTrue();
        _cheatcodes.IsPersistent("0x5678").Should().BeTrue();
        _cheatcodes.IsPersistent("0x9999").Should().BeFalse();
    }

    [Fact]
    public void RevokePersistent_UnmarksContracts()
    {
        // Arrange
        _cheatcodes.MakePersistent("0x1234", "0x5678");

        // Act
        _cheatcodes.RevokePersistent("0x1234");

        // Assert
        _cheatcodes.IsPersistent("0x1234").Should().BeFalse();
        _cheatcodes.IsPersistent("0x5678").Should().BeTrue();
    }

    [Fact]
    public void GetAllForks_ReturnsAllCreatedForks()
    {
        // Arrange
        _cheatcodes.CreateFork("http://localhost:10332");
        _cheatcodes.CreateFork("http://localhost:10333");
        _cheatcodes.CreateFork("http://localhost:10334");

        // Act
        var forks = _cheatcodes.GetAllForks();

        // Assert
        forks.Should().HaveCount(3);
    }

    #endregion
}
