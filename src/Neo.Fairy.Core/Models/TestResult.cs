// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Core.Models;

/// <summary>
/// Represents the result of a test execution.
/// </summary>
public sealed record TestResult
{
    /// <summary>
    /// Gets the test name.
    /// </summary>
    public required string TestName { get; init; }

    /// <summary>
    /// Gets the test class name.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Gets the full test identifier.
    /// </summary>
    public string FullName => $"{ClassName}::{TestName}";

    /// <summary>
    /// Gets the test status.
    /// </summary>
    public required TestStatus Status { get; init; }

    /// <summary>
    /// Gets the execution duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the total GAS consumed.
    /// </summary>
    public long GasConsumed { get; init; }

    /// <summary>
    /// Gets the failure message if test failed.
    /// </summary>
    public string? FailureMessage { get; init; }

    /// <summary>
    /// Gets the stack trace if test failed.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// Gets the expected value for assertion failures.
    /// </summary>
    public string? Expected { get; init; }

    /// <summary>
    /// Gets the actual value for assertion failures.
    /// </summary>
    public string? Actual { get; init; }

    /// <summary>
    /// Gets the source file location.
    /// </summary>
    public string? SourceFile { get; init; }

    /// <summary>
    /// Gets the source line number.
    /// </summary>
    public int? SourceLine { get; init; }

    /// <summary>
    /// Gets fuzz test statistics if applicable.
    /// </summary>
    public FuzzStats? FuzzStats { get; init; }

    /// <summary>
    /// Gets whether the test passed.
    /// </summary>
    public bool Passed => Status == TestStatus.Passed;

    /// <summary>
    /// Gets whether the test failed.
    /// </summary>
    public bool Failed => Status == TestStatus.Failed;

    /// <summary>
    /// Creates a passed test result.
    /// </summary>
    public static TestResult Pass(
        string className,
        string testName,
        TimeSpan duration,
        long gasConsumed = 0)
    {
        return new TestResult
        {
            ClassName = className,
            TestName = testName,
            Status = TestStatus.Passed,
            Duration = duration,
            GasConsumed = gasConsumed
        };
    }

    /// <summary>
    /// Creates a failed test result.
    /// </summary>
    public static TestResult Fail(
        string className,
        string testName,
        TimeSpan duration,
        string failureMessage,
        string? stackTrace = null,
        string? expected = null,
        string? actual = null,
        long gasConsumed = 0)
    {
        return new TestResult
        {
            ClassName = className,
            TestName = testName,
            Status = TestStatus.Failed,
            Duration = duration,
            GasConsumed = gasConsumed,
            FailureMessage = failureMessage,
            StackTrace = stackTrace,
            Expected = expected,
            Actual = actual
        };
    }

    /// <summary>
    /// Creates a skipped test result.
    /// </summary>
    public static TestResult Skip(
        string className,
        string testName,
        string reason)
    {
        return new TestResult
        {
            ClassName = className,
            TestName = testName,
            Status = TestStatus.Skipped,
            Duration = TimeSpan.Zero,
            FailureMessage = reason
        };
    }
}

/// <summary>
/// Test execution status.
/// </summary>
public enum TestStatus
{
    /// <summary>Test passed successfully.</summary>
    Passed,

    /// <summary>Test failed.</summary>
    Failed,

    /// <summary>Test was skipped.</summary>
    Skipped,

    /// <summary>Test is pending execution.</summary>
    Pending
}

/// <summary>
/// Statistics for fuzz tests.
/// </summary>
public sealed class FuzzStats
{
    /// <summary>
    /// Gets the number of test runs.
    /// </summary>
    public required int Runs { get; init; }

    /// <summary>
    /// Gets the number of reverts encountered.
    /// </summary>
    public int Reverts { get; init; }

    /// <summary>
    /// Gets the seed used for random generation (for reproducibility).
    /// </summary>
    public int Seed { get; init; }

    /// <summary>
    /// Gets the average GAS consumed per run.
    /// </summary>
    public double AverageGas { get; init; }

    /// <summary>
    /// Gets the minimum GAS consumed.
    /// </summary>
    public long MinGas { get; init; }

    /// <summary>
    /// Gets the maximum GAS consumed.
    /// </summary>
    public long MaxGas { get; init; }

    /// <summary>
    /// Gets the failing input if any.
    /// </summary>
    public object[]? FailingInput { get; init; }
}

/// <summary>
/// Summary of test suite execution.
/// </summary>
public sealed class TestSummary
{
    /// <summary>
    /// Gets all test results.
    /// </summary>
    public required IReadOnlyList<TestResult> Results { get; init; }

    /// <summary>
    /// Gets the total execution time.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Gets the number of passed tests.
    /// </summary>
    public int Passed => Results.Count(r => r.Status == TestStatus.Passed);

    /// <summary>
    /// Gets the number of failed tests.
    /// </summary>
    public int Failed => Results.Count(r => r.Status == TestStatus.Failed);

    /// <summary>
    /// Gets the number of skipped tests.
    /// </summary>
    public int Skipped => Results.Count(r => r.Status == TestStatus.Skipped);

    /// <summary>
    /// Gets the total number of tests.
    /// </summary>
    public int Total => Results.Count;

    /// <summary>
    /// Gets the pass rate as a percentage.
    /// </summary>
    public double PassRate => Total > 0 ? (double)Passed / Total * 100 : 0;

    /// <summary>
    /// Gets whether all tests passed.
    /// </summary>
    public bool AllPassed => Failed == 0;

    /// <summary>
    /// Gets the total GAS consumed.
    /// </summary>
    public long TotalGasConsumed => Results.Sum(r => r.GasConsumed);
}
