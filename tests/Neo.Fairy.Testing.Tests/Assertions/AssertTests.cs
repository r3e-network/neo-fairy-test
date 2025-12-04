// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Testing;
using Neo.Fairy.Testing.Assertions;
using System.Numerics;
using Xunit;
using Assert = Neo.Fairy.Testing.Assertions.Assert;

namespace Neo.Fairy.Testing.Tests.Assertions;

public class AssertTests
{
    private readonly Assert _assert = Assert.Instance;

    #region Value Assertions

    [Fact]
    public void Equal_SameValues_Passes()
    {
        // Should not throw
        _assert.Equal(42, 42);
        _assert.Equal("hello", "hello");
        _assert.Equal(true, true);
    }

    [Fact]
    public void Equal_DifferentValues_Throws()
    {
        var action = () => _assert.Equal(42, 43);
        action.Should().Throw<AssertionFailedException>()
            .Where(e => e.Expected == "42" && e.Actual == "43");
    }

    [Fact]
    public void NotEqual_DifferentValues_Passes()
    {
        _assert.NotEqual(42, 43);
        _assert.NotEqual("hello", "world");
    }

    [Fact]
    public void NotEqual_SameValues_Throws()
    {
        var action = () => _assert.NotEqual(42, 42);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void True_WhenTrue_Passes()
    {
        _assert.True(true);
        _assert.True(1 == 1);
    }

    [Fact]
    public void True_WhenFalse_Throws()
    {
        var action = () => _assert.True(false);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void False_WhenFalse_Passes()
    {
        _assert.False(false);
        _assert.False(1 == 2);
    }

    [Fact]
    public void False_WhenTrue_Throws()
    {
        var action = () => _assert.False(true);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void Greater_WhenGreater_Passes()
    {
        _assert.Greater(10, 5);
        _assert.Greater(100L, 50L);
    }

    [Fact]
    public void Greater_WhenNotGreater_Throws()
    {
        var action = () => _assert.Greater(5, 10);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void Less_WhenLess_Passes()
    {
        _assert.Less(5, 10);
        _assert.Less(50L, 100L);
    }

    [Fact]
    public void InRange_WhenInRange_Passes()
    {
        _assert.InRange(5, 1, 10);
        _assert.InRange(1, 1, 10); // Inclusive
        _assert.InRange(10, 1, 10); // Inclusive
    }

    [Fact]
    public void InRange_WhenOutOfRange_Throws()
    {
        var action = () => _assert.InRange(15, 1, 10);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void ApproxEqual_WithinTolerance_Passes()
    {
        _assert.ApproxEqual(new BigInteger(100), new BigInteger(105), new BigInteger(10));
        _assert.ApproxEqual(new BigInteger(1000), new BigInteger(1000), new BigInteger(0));
    }

    [Fact]
    public void ApproxEqual_OutsideTolerance_Throws()
    {
        var action = () => _assert.ApproxEqual(
            new BigInteger(100),
            new BigInteger(120),
            new BigInteger(10));
        action.Should().Throw<AssertionFailedException>();
    }

    #endregion

    #region Execution State Assertions

    [Fact]
    public void Halted_WhenHalt_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        _assert.Halted(result);
    }

    [Fact]
    public void Halted_WhenFault_Throws()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Error"
        };

        var action = () => _assert.Halted(result);
        action.Should().Throw<AssertionFailedException>()
            .Where(e => e.Expected == "HALT" && e.Actual == "Fault");
    }

    [Fact]
    public void Faulted_WhenFault_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000
        };

        _assert.Faulted(result);
    }

    [Fact]
    public void RevertedWith_MatchingMessage_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Insufficient balance"
        };

        _assert.RevertedWith(result, "Insufficient balance");
        _assert.RevertedWith(result, "balance"); // Partial match
    }

    [Fact]
    public void RevertedWith_NonMatchingMessage_Throws()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Insufficient balance"
        };

        var action = () => _assert.RevertedWith(result, "Not authorized");
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void GasUsed_WithinTolerance_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        _assert.GasUsed(result, 100000);
        _assert.GasUsed(result, 100500, tolerance: 1000);
    }

    [Fact]
    public void GasUsed_OutsideTolerance_Throws()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        var action = () => _assert.GasUsed(result, 200000);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void GasLessThan_WhenLess_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        _assert.GasLessThan(result, 200000);
    }

    #endregion

    #region Event Assertions

    [Fact]
    public void EmittedEvent_WhenEventExists_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>
            {
                new NotificationInfo
                {
                    ContractHash = "0x1234",
                    EventName = "Transfer"
                }
            }
        };

        _assert.EmittedEvent(result, "Transfer");
        _assert.EmittedEvent(result, "transfer"); // Case insensitive
    }

    [Fact]
    public void EmittedEvent_WhenEventMissing_Throws()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>
            {
                new NotificationInfo
                {
                    ContractHash = "0x1234",
                    EventName = "Mint"
                }
            }
        };

        var action = () => _assert.EmittedEvent(result, "Transfer");
        action.Should().Throw<AssertionFailedException>()
            .Where(e => e.Expected == "Transfer");
    }

    [Fact]
    public void EmittedEventCount_CorrectCount_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>
            {
                new NotificationInfo { ContractHash = "0x1", EventName = "Transfer" },
                new NotificationInfo { ContractHash = "0x2", EventName = "Transfer" },
                new NotificationInfo { ContractHash = "0x3", EventName = "Mint" }
            }
        };

        _assert.EmittedEventCount(result, "Transfer", 2);
        _assert.EmittedEventCount(result, "Mint", 1);
    }

    [Fact]
    public void NoEvents_WhenEmpty_Passes()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>()
        };

        _assert.NoEvents(result);
    }

    [Fact]
    public void NoEvents_WhenHasEvents_Throws()
    {
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Notifications = new List<NotificationInfo>
            {
                new NotificationInfo { ContractHash = "0x1", EventName = "Transfer" }
            }
        };

        var action = () => _assert.NoEvents(result);
        action.Should().Throw<AssertionFailedException>();
    }

    #endregion

    #region Collection Assertions

    [Fact]
    public void Contains_WhenContains_Passes()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        _assert.Contains(list, 3);
    }

    [Fact]
    public void Contains_WhenNotContains_Throws()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var action = () => _assert.Contains(list, 10);
        action.Should().Throw<AssertionFailedException>();
    }

    [Fact]
    public void Empty_WhenEmpty_Passes()
    {
        var list = new List<int>();
        _assert.Empty(list);
    }

    [Fact]
    public void NotEmpty_WhenNotEmpty_Passes()
    {
        var list = new List<int> { 1 };
        _assert.NotEmpty(list);
    }

    #endregion
}
