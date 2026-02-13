// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Interfaces;

namespace Neo.Fairy.Core.Models;

/// <summary>
/// Represents the result of a smart contract execution.
/// </summary>
public sealed class ExecutionResult
{
    /// <summary>
    /// Gets the execution state (HALT, FAULT, etc.).
    /// </summary>
    public required ExecutionState State { get; init; }

    /// <summary>
    /// Gets the GAS consumed by the execution.
    /// </summary>
    public required long GasConsumed { get; init; }

    /// <summary>
    /// Gets the result stack items.
    /// </summary>
    public IReadOnlyList<StackItem> Stack { get; init; } = Array.Empty<StackItem>();

    /// <summary>
    /// Gets the notifications emitted during execution.
    /// </summary>
    public IReadOnlyList<NotificationInfo> Notifications { get; init; } = Array.Empty<NotificationInfo>();

    /// <summary>
    /// Gets the exception message if execution faulted.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Gets the detailed traceback if execution faulted.
    /// </summary>
    public string? Traceback { get; init; }

    /// <summary>
    /// Gets the script that was executed.
    /// </summary>
    public byte[]? Script { get; init; }

    /// <summary>
    /// Gets the transaction hash if relayed to chain.
    /// </summary>
    public string? TransactionHash { get; init; }

    /// <summary>
    /// Gets the network fee if calculated.
    /// </summary>
    public long? NetworkFee { get; init; }

    /// <summary>
    /// Gets any additional notes about the execution (e.g., pending multisig signature).
    /// </summary>
    public string? Note { get; init; }

    /// <summary>
    /// Gets the system fee (same as GasConsumed for virtual execution).
    /// </summary>
    public long SystemFee => GasConsumed;

    /// <summary>
    /// Gets whether the execution was successful (HALT state).
    /// </summary>
    public bool IsSuccess => State == ExecutionState.Halt;

    /// <summary>
    /// Gets whether the execution faulted.
    /// </summary>
    public bool IsFault => State == ExecutionState.Fault;

    /// <summary>
    /// Gets the first stack item as a specific type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <returns>The typed value.</returns>
    public T? GetResult<T>()
    {
        if (Stack.Count == 0) return default;
        return Stack[0].GetValue<T>();
    }

    /// <summary>
    /// Throws if the execution faulted.
    /// </summary>
    public ExecutionResult EnsureSuccess()
    {
        if (IsFault)
        {
            throw new ExecutionFaultException(Exception ?? "Execution faulted", Traceback);
        }
        return this;
    }
}

/// <summary>
/// Represents a stack item from execution result.
/// </summary>
public sealed class StackItem
{
    /// <summary>
    /// Gets the item type.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the raw value.
    /// </summary>
    public required object? Value { get; init; }

    /// <summary>
    /// Gets the value as a specific type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (Value is null) return default;
        if (Value is T typed) return typed;

        // Handle common conversions
        return typeof(T) switch
        {
            var t when t == typeof(string) => (T)(object)Value.ToString()!,
            var t when t == typeof(long) => (T)(object)Convert.ToInt64(Value),
            var t when t == typeof(int) => (T)(object)Convert.ToInt32(Value),
            var t when t == typeof(bool) => (T)(object)Convert.ToBoolean(Value),
            var t when t == typeof(byte[]) => Value switch
            {
                string s => (T)(object)Convert.FromBase64String(s),
                byte[] b => (T)(object)b,
                _ => default
            },
            _ => default
        };
    }
}

/// <summary>
/// Represents a notification emitted during execution.
/// </summary>
public sealed class NotificationInfo
{
    /// <summary>
    /// Gets the contract that emitted the notification.
    /// </summary>
    public required string ContractHash { get; init; }

    /// <summary>
    /// Gets the contract name.
    /// </summary>
    public string? ContractName { get; init; }

    /// <summary>
    /// Gets the event name.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the event arguments.
    /// </summary>
    public IReadOnlyList<StackItem> State { get; init; } = Array.Empty<StackItem>();
}

/// <summary>
/// Exception thrown when contract execution faults.
/// </summary>
public sealed class ExecutionFaultException : Exception
{
    /// <summary>
    /// Gets the execution traceback.
    /// </summary>
    public string? Traceback { get; }

    public ExecutionFaultException(string message, string? traceback = null)
        : base(message)
    {
        Traceback = traceback;
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(Traceback)
            ? base.ToString()
            : $"{base.ToString()}\n\nTraceback:\n{Traceback}";
    }
}
