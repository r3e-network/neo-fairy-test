// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using Xunit;

namespace Neo.Fairy.Core.Tests.Models;

public class ExecutionResultTests
{
    [Fact]
    public void IsSuccess_WhenHalt_ReturnsTrue()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFault.Should().BeFalse();
    }

    [Fact]
    public void IsFault_WhenFault_ReturnsTrue()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "ASSERT failed"
        };

        // Assert
        result.IsFault.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Exception.Should().Be("ASSERT failed");
    }

    [Fact]
    public void GetResult_WithStack_ReturnsTypedValue()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Stack = new List<StackItem>
            {
                new StackItem { Type = "Integer", Value = 42L }
            }
        };

        // Act
        var value = result.GetResult<long>();

        // Assert
        value.Should().Be(42L);
    }

    [Fact]
    public void GetResult_EmptyStack_ReturnsDefault()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000,
            Stack = new List<StackItem>()
        };

        // Act
        var value = result.GetResult<long>();

        // Assert
        value.Should().Be(0L);
    }

    [Fact]
    public void EnsureSuccess_WhenHalt_ReturnsResult()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 100000
        };

        // Act
        var returned = result.EnsureSuccess();

        // Assert
        returned.Should().BeSameAs(result);
    }

    [Fact]
    public void EnsureSuccess_WhenFault_ThrowsException()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Fault,
            GasConsumed = 50000,
            Exception = "Contract error",
            Traceback = "at line 42"
        };

        // Act & Assert
        var action = () => result.EnsureSuccess();
        action.Should().Throw<ExecutionFaultException>()
            .WithMessage("Contract error")
            .Where(e => e.Traceback == "at line 42");
    }

    [Fact]
    public void SystemFee_EqualGasConsumed()
    {
        // Arrange
        var result = new ExecutionResult
        {
            State = ExecutionState.Halt,
            GasConsumed = 123456789
        };

        // Assert
        result.SystemFee.Should().Be(123456789);
    }
}

public class StackItemTests
{
    [Fact]
    public void GetValue_Integer_ConvertsCorrectly()
    {
        // Arrange
        var item = new StackItem { Type = "Integer", Value = 100L };

        // Assert
        item.GetValue<long>().Should().Be(100L);
        item.GetValue<int>().Should().Be(100);
        item.GetValue<string>().Should().Be("100");
    }

    [Fact]
    public void GetValue_String_ReturnsString()
    {
        // Arrange
        var item = new StackItem { Type = "ByteString", Value = "Hello" };

        // Assert
        item.GetValue<string>().Should().Be("Hello");
    }

    [Fact]
    public void GetValue_Boolean_ConvertsCorrectly()
    {
        // Arrange
        var trueItem = new StackItem { Type = "Boolean", Value = true };
        var falseItem = new StackItem { Type = "Boolean", Value = false };

        // Assert
        trueItem.GetValue<bool>().Should().BeTrue();
        falseItem.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public void GetValue_Null_ReturnsDefault()
    {
        // Arrange
        var item = new StackItem { Type = "Any", Value = null };

        // Assert
        item.GetValue<string>().Should().BeNull();
        item.GetValue<long>().Should().Be(0);
    }

    [Fact]
    public void GetValue_ByteArray_FromBase64()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3, 4 };
        var base64 = Convert.ToBase64String(bytes);
        var item = new StackItem { Type = "ByteArray", Value = base64 };

        // Act
        var result = item.GetValue<byte[]>();

        // Assert
        result.Should().BeEquivalentTo(bytes);
    }
}

public class NotificationInfoTests
{
    [Fact]
    public void Notification_StoresAllProperties()
    {
        // Arrange
        var notification = new NotificationInfo
        {
            ContractHash = "0x1234567890abcdef",
            ContractName = "MyToken",
            EventName = "Transfer",
            State = new List<StackItem>
            {
                new StackItem { Type = "Hash160", Value = "0xfrom" },
                new StackItem { Type = "Hash160", Value = "0xto" },
                new StackItem { Type = "Integer", Value = 1000L }
            }
        };

        // Assert
        notification.ContractHash.Should().Be("0x1234567890abcdef");
        notification.ContractName.Should().Be("MyToken");
        notification.EventName.Should().Be("Transfer");
        notification.State.Should().HaveCount(3);
    }
}
