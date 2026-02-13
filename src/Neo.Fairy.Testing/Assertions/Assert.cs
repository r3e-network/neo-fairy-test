// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using System.Numerics;

namespace Neo.Fairy.Testing.Assertions;

/// <summary>
/// Provides assertion methods for Fairy tests.
/// Inspired by Foundry's forge-std assertions.
/// </summary>
public sealed class Assert
{
    /// <summary>
    /// Singleton instance for static access.
    /// </summary>
    public static Assert Instance { get; } = new();

    private Assert() { }

    #region Value Assertions

    /// <summary>
    /// Asserts that two values are equal.
    /// </summary>
    public void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionFailedException(
                message ?? $"Expected {expected} but got {actual}",
                expected?.ToString(),
                actual?.ToString());
        }
    }

    /// <summary>
    /// Asserts that two values are not equal.
    /// </summary>
    public void NotEqual<T>(T expected, T actual, string? message = null)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionFailedException(
                message ?? $"Expected values to be different but both were {actual}");
        }
    }

    /// <summary>
    /// Asserts that a condition is true.
    /// </summary>
    public void True(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new AssertionFailedException(message ?? "Expected true but was false");
        }
    }

    /// <summary>
    /// Asserts that a condition is false.
    /// </summary>
    public void False(bool condition, string? message = null)
    {
        if (condition)
        {
            throw new AssertionFailedException(message ?? "Expected false but was true");
        }
    }

    /// <summary>
    /// Asserts that a value is null.
    /// </summary>
    public void Null<T>(T? value, string? message = null) where T : class
    {
        if (value != null)
        {
            throw new AssertionFailedException(message ?? $"Expected null but got {value}");
        }
    }

    /// <summary>
    /// Asserts that a value is not null.
    /// </summary>
    public void NotNull<T>(T? value, string? message = null) where T : class
    {
        if (value == null)
        {
            throw new AssertionFailedException(message ?? "Expected non-null value but got null");
        }
    }

    /// <summary>
    /// Asserts that a is greater than b.
    /// </summary>
    public void Greater<T>(T a, T b, string? message = null) where T : IComparable<T>
    {
        if (a.CompareTo(b) <= 0)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {a} > {b}",
                $"> {b}",
                a?.ToString());
        }
    }

    /// <summary>
    /// Asserts that a is greater than or equal to b.
    /// </summary>
    public void GreaterOrEqual<T>(T a, T b, string? message = null) where T : IComparable<T>
    {
        if (a.CompareTo(b) < 0)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {a} >= {b}",
                $">= {b}",
                a?.ToString());
        }
    }

    /// <summary>
    /// Asserts that a is less than b.
    /// </summary>
    public void Less<T>(T a, T b, string? message = null) where T : IComparable<T>
    {
        if (a.CompareTo(b) >= 0)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {a} < {b}",
                $"< {b}",
                a?.ToString());
        }
    }

    /// <summary>
    /// Asserts that a is less than or equal to b.
    /// </summary>
    public void LessOrEqual<T>(T a, T b, string? message = null) where T : IComparable<T>
    {
        if (a.CompareTo(b) > 0)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {a} <= {b}",
                $"<= {b}",
                a?.ToString());
        }
    }

    /// <summary>
    /// Asserts that a value is within a range.
    /// </summary>
    public void InRange<T>(T value, T min, T max, string? message = null) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {value} to be in range [{min}, {max}]",
                $"[{min}, {max}]",
                value?.ToString());
        }
    }

    /// <summary>
    /// Asserts approximate equality for decimals/BigInteger with tolerance.
    /// </summary>
    public void ApproxEqual(BigInteger expected, BigInteger actual, BigInteger tolerance, string? message = null)
    {
        var diff = BigInteger.Abs(expected - actual);
        if (diff > tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {actual} to be within {tolerance} of {expected} (diff: {diff})",
                expected.ToString(),
                actual.ToString());
        }
    }

    #endregion

    #region Execution State Assertions

    /// <summary>
    /// Asserts that execution completed successfully (HALT state).
    /// </summary>
    public void Halted(ExecutionResult result, string? message = null)
    {
        if (result.State != ExecutionState.Halt)
        {
            throw new AssertionFailedException(
                message ?? $"Expected HALT but got {result.State}: {result.Exception}",
                "HALT",
                result.State.ToString());
        }
    }

    /// <summary>
    /// Asserts that execution faulted.
    /// </summary>
    public void Faulted(ExecutionResult result, string? message = null)
    {
        if (result.State != ExecutionState.Fault)
        {
            throw new AssertionFailedException(
                message ?? $"Expected FAULT but got {result.State}",
                "FAULT",
                result.State.ToString());
        }
    }

    /// <summary>
    /// Asserts that execution reverted with a specific message.
    /// </summary>
    public void Reverted(ExecutionResult result, string? message = null)
    {
        Faulted(result, message);
    }

    /// <summary>
    /// Asserts that execution reverted with a specific error message.
    /// </summary>
    public void RevertedWith(ExecutionResult result, string expectedMessage, string? message = null)
    {
        Faulted(result);

        if (result.Exception == null || !result.Exception.Contains(expectedMessage))
        {
            throw new AssertionFailedException(
                message ?? $"Expected revert with '{expectedMessage}' but got '{result.Exception}'",
                expectedMessage,
                result.Exception);
        }
    }

    /// <summary>
    /// Asserts that GAS consumed is within expected range.
    /// </summary>
    public void GasUsed(ExecutionResult result, long expected, long tolerance = 0, string? message = null)
    {
        var diff = Math.Abs(result.GasConsumed - expected);
        if (diff > tolerance)
        {
            throw new AssertionFailedException(
                message ?? $"Expected GAS ~{expected} (±{tolerance}) but used {result.GasConsumed}",
                expected.ToString(),
                result.GasConsumed.ToString());
        }
    }

    /// <summary>
    /// Asserts that GAS consumed is less than a maximum.
    /// </summary>
    public void GasLessThan(ExecutionResult result, long maxGas, string? message = null)
    {
        if (result.GasConsumed >= maxGas)
        {
            throw new AssertionFailedException(
                message ?? $"Expected GAS < {maxGas} but used {result.GasConsumed}",
                $"< {maxGas}",
                result.GasConsumed.ToString());
        }
    }

    #endregion

    #region Event Assertions

    /// <summary>
    /// Asserts that a specific event was emitted.
    /// </summary>
    public void EmittedEvent(ExecutionResult result, string eventName, string? message = null)
    {
        var found = result.Notifications.Any(n =>
            string.Equals(n.EventName, eventName, StringComparison.OrdinalIgnoreCase));

        if (!found)
        {
            var emitted = string.Join(", ", result.Notifications.Select(n => n.EventName));
            throw new AssertionFailedException(
                message ?? $"Expected event '{eventName}' but got: [{emitted}]",
                eventName,
                emitted);
        }
    }

    /// <summary>
    /// Asserts that a specific event was emitted with expected arguments.
    /// </summary>
    public void EmittedEvent(ExecutionResult result, string eventName, params object[] expectedArgs)
    {
        var notification = result.Notifications.FirstOrDefault(n =>
            string.Equals(n.EventName, eventName, StringComparison.OrdinalIgnoreCase));

        if (notification == null)
        {
            var emitted = string.Join(", ", result.Notifications.Select(n => n.EventName));
            throw new AssertionFailedException(
                $"Expected event '{eventName}' but got: [{emitted}]",
                eventName,
                emitted);
        }

        // Verify arguments
        if (expectedArgs.Length > 0)
        {
            if (expectedArgs.Length > notification.State.Count)
            {
                throw new AssertionFailedException(
                    $"Event '{eventName}' has {notification.State.Count} arguments but expected {expectedArgs.Length}",
                    expectedArgs.Length.ToString(),
                    notification.State.Count.ToString());
            }
            for (int i = 0; i < expectedArgs.Length; i++)
            {
                var expected = expectedArgs[i];
                var actual = notification.State[i].Value;

                if (!ValuesEqual(expected, actual))
                {
                    throw new AssertionFailedException(
                        $"Event '{eventName}' argument {i}: expected {expected} but got {actual}",
                        expected?.ToString(),
                        actual?.ToString());
                }
            }
        }
    }

    /// <summary>
    /// Asserts that a specific number of events were emitted.
    /// </summary>
    public void EmittedEventCount(ExecutionResult result, string eventName, int expectedCount, string? message = null)
    {
        var count = result.Notifications.Count(n =>
            string.Equals(n.EventName, eventName, StringComparison.OrdinalIgnoreCase));

        if (count != expectedCount)
        {
            throw new AssertionFailedException(
                message ?? $"Expected {expectedCount} '{eventName}' events but got {count}",
                expectedCount.ToString(),
                count.ToString());
        }
    }

    /// <summary>
    /// Asserts that no events were emitted.
    /// </summary>
    public void NoEvents(ExecutionResult result, string? message = null)
    {
        if (result.Notifications.Count > 0)
        {
            var emitted = string.Join(", ", result.Notifications.Select(n => n.EventName));
            throw new AssertionFailedException(
                message ?? $"Expected no events but got: [{emitted}]",
                "none",
                emitted);
        }
    }

    #endregion

    #region Collection Assertions

    /// <summary>
    /// Asserts that a collection contains a specific item.
    /// </summary>
    public void Contains<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (!collection.Contains(item))
        {
            throw new AssertionFailedException(
                message ?? $"Collection does not contain {item}");
        }
    }

    /// <summary>
    /// Asserts that a collection does not contain a specific item.
    /// </summary>
    public void DoesNotContain<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (collection.Contains(item))
        {
            throw new AssertionFailedException(
                message ?? $"Collection should not contain {item}");
        }
    }

    /// <summary>
    /// Asserts that a collection is empty.
    /// </summary>
    public void Empty<T>(IEnumerable<T> collection, string? message = null)
    {
        var materialized = collection as ICollection<T> ?? collection.ToList();
        if (materialized.Count > 0)
        {
            throw new AssertionFailedException(
                message ?? $"Expected empty collection but had {materialized.Count} items");
        }
    }

    /// <summary>
    /// Asserts that a collection is not empty.
    /// </summary>
    public void NotEmpty<T>(IEnumerable<T> collection, string? message = null)
    {
        if (!collection.Any())
        {
            throw new AssertionFailedException(
                message ?? "Expected non-empty collection but was empty");
        }
    }

    #endregion

    #region Helpers

    private static bool ValuesEqual(object? expected, object? actual)
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;

        // Handle BigInteger comparisons
        if (expected is BigInteger expectedBi)
        {
            if (actual is BigInteger actualBi) return expectedBi == actualBi;
            if (actual is long actualLong) return expectedBi == actualLong;
            if (actual is int actualInt) return expectedBi == actualInt;
        }

        // Handle string comparisons
        if (expected is string expectedStr && actual is string actualStr)
        {
            return string.Equals(expectedStr, actualStr, StringComparison.Ordinal);
        }

        return expected.Equals(actual);
    }

    #endregion
}
