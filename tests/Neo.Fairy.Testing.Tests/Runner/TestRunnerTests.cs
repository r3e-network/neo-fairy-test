// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Testing;
using Neo.Fairy.Testing.Runner;
using Xunit;

namespace Neo.Fairy.Testing.Tests.Runner;

public class TestRunnerOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new TestRunnerOptions();

        // Assert
        options.FailFast.Should().BeFalse();
        options.FuzzRuns.Should().Be(256);
        options.FuzzSeed.Should().BeNull();
        options.Verbosity.Should().Be(TestDefaults.DefaultVerbosity);
        options.CollectCoverage.Should().BeFalse();
        options.OnTestCompleted.Should().BeNull();
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        // Arrange & Act
        var options = new TestRunnerOptions
        {
            FailFast = true,
            FuzzRuns = 1000,
            FuzzSeed = 42,
            Verbosity = 4,
            CollectCoverage = true
        };

        // Assert
        options.FailFast.Should().BeTrue();
        options.FuzzRuns.Should().Be(1000);
        options.FuzzSeed.Should().Be(42);
        options.Verbosity.Should().Be(4);
        options.CollectCoverage.Should().BeTrue();
    }

    [Fact]
    public void OnTestCompleted_CanBeSet()
    {
        // Arrange
        var callCount = 0;
        var options = new TestRunnerOptions
        {
            OnTestCompleted = result => callCount++
        };

        // Act
        options.OnTestCompleted?.Invoke(TestResult.Pass("Test", "Method", TimeSpan.Zero));

        // Assert
        callCount.Should().Be(1);
    }
}

public class TestResultTests
{
    [Fact]
    public void Pass_CreatesPassedResult()
    {
        // Act
        var result = TestResult.Pass("MyTestClass", "TestMethod", TimeSpan.FromMilliseconds(100), 50000);

        // Assert
        result.ClassName.Should().Be("MyTestClass");
        result.TestName.Should().Be("TestMethod");
        result.Status.Should().Be(TestStatus.Passed);
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(100));
        result.GasConsumed.Should().Be(50000);
        result.Passed.Should().BeTrue();
        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        // Act
        var result = TestResult.Fail(
            "MyTestClass",
            "TestMethod",
            TimeSpan.FromMilliseconds(50),
            "Assertion failed",
            "at line 42",
            "expected",
            "actual");

        // Assert
        result.ClassName.Should().Be("MyTestClass");
        result.TestName.Should().Be("TestMethod");
        result.Status.Should().Be(TestStatus.Failed);
        result.FailureMessage.Should().Be("Assertion failed");
        result.StackTrace.Should().Be("at line 42");
        result.Expected.Should().Be("expected");
        result.Actual.Should().Be("actual");
        result.Passed.Should().BeFalse();
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Skip_CreatesSkippedResult()
    {
        // Act
        var result = TestResult.Skip("MyTestClass", "TestMethod", "Not implemented yet");

        // Assert
        result.ClassName.Should().Be("MyTestClass");
        result.TestName.Should().Be("TestMethod");
        result.Status.Should().Be(TestStatus.Skipped);
        result.FailureMessage.Should().Be("Not implemented yet");
        result.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void FullName_CombinesClassAndMethod()
    {
        // Arrange
        var result = TestResult.Pass("MyClass", "MyMethod", TimeSpan.Zero);

        // Assert
        result.FullName.Should().Be("MyClass::MyMethod");
    }

    [Fact]
    public void FuzzStats_CanBeAttached()
    {
        // Arrange
        var result = new TestResult
        {
            ClassName = "FuzzTest",
            TestName = "TestFuzz_Random",
            Status = TestStatus.Passed,
            Duration = TimeSpan.FromSeconds(5),
            FuzzStats = new FuzzStats
            {
                Runs = 256,
                Reverts = 10,
                AverageGas = 50000,
                MinGas = 40000,
                MaxGas = 60000
            }
        };

        // Assert
        result.FuzzStats.Should().NotBeNull();
        result.FuzzStats!.Runs.Should().Be(256);
        result.FuzzStats.Reverts.Should().Be(10);
        result.FuzzStats.AverageGas.Should().Be(50000);
    }
}

public class TestSummaryTests
{
    [Fact]
    public void Summary_CalculatesPassedCount()
    {
        // Arrange
        var results = new List<TestResult>
        {
            TestResult.Pass("Test", "Method1", TimeSpan.Zero),
            TestResult.Pass("Test", "Method2", TimeSpan.Zero),
            TestResult.Fail("Test", "Method3", TimeSpan.Zero, "Error"),
            TestResult.Skip("Test", "Method4", "Skipped")
        };

        var summary = new TestSummary
        {
            Results = results,
            TotalDuration = TimeSpan.FromSeconds(1)
        };

        // Assert
        summary.Passed.Should().Be(2);
        summary.Failed.Should().Be(1);
        summary.Skipped.Should().Be(1);
        summary.Total.Should().Be(4);
    }

    [Fact]
    public void Summary_CalculatesPassRate()
    {
        // Arrange
        var results = new List<TestResult>
        {
            TestResult.Pass("Test", "Method1", TimeSpan.Zero),
            TestResult.Pass("Test", "Method2", TimeSpan.Zero),
            TestResult.Pass("Test", "Method3", TimeSpan.Zero),
            TestResult.Fail("Test", "Method4", TimeSpan.Zero, "Error")
        };

        var summary = new TestSummary
        {
            Results = results,
            TotalDuration = TimeSpan.FromSeconds(1)
        };

        // Assert
        summary.PassRate.Should().Be(75); // 3/4 = 75%
    }

    [Fact]
    public void Summary_AllPassed_WhenNoFailures()
    {
        // Arrange
        var results = new List<TestResult>
        {
            TestResult.Pass("Test", "Method1", TimeSpan.Zero),
            TestResult.Pass("Test", "Method2", TimeSpan.Zero),
            TestResult.Skip("Test", "Method3", "Skipped")
        };

        var summary = new TestSummary
        {
            Results = results,
            TotalDuration = TimeSpan.FromSeconds(1)
        };

        // Assert
        summary.AllPassed.Should().BeTrue();
    }

    [Fact]
    public void Summary_NotAllPassed_WhenHasFailures()
    {
        // Arrange
        var results = new List<TestResult>
        {
            TestResult.Pass("Test", "Method1", TimeSpan.Zero),
            TestResult.Fail("Test", "Method2", TimeSpan.Zero, "Error")
        };

        var summary = new TestSummary
        {
            Results = results,
            TotalDuration = TimeSpan.FromSeconds(1)
        };

        // Assert
        summary.AllPassed.Should().BeFalse();
    }

    [Fact]
    public void Summary_CalculatesTotalGasConsumed()
    {
        // Arrange
        var results = new List<TestResult>
        {
            TestResult.Pass("Test", "Method1", TimeSpan.Zero, 100000),
            TestResult.Pass("Test", "Method2", TimeSpan.Zero, 200000),
            TestResult.Pass("Test", "Method3", TimeSpan.Zero, 150000)
        };

        var summary = new TestSummary
        {
            Results = results,
            TotalDuration = TimeSpan.FromSeconds(1)
        };

        // Assert
        summary.TotalGasConsumed.Should().Be(450000);
    }

    [Fact]
    public void Summary_EmptyResults_HandlesGracefully()
    {
        // Arrange
        var summary = new TestSummary
        {
            Results = new List<TestResult>(),
            TotalDuration = TimeSpan.Zero
        };

        // Assert
        summary.Passed.Should().Be(0);
        summary.Failed.Should().Be(0);
        summary.Total.Should().Be(0);
        summary.PassRate.Should().Be(0);
        summary.AllPassed.Should().BeTrue(); // No failures means all passed
    }
}

public class FuzzStatsTests
{
    [Fact]
    public void FuzzStats_StoresAllProperties()
    {
        // Arrange & Act
        var stats = new FuzzStats
        {
            Runs = 1000,
            Reverts = 50,
            AverageGas = 75000,
            MinGas = 50000,
            MaxGas = 100000,
            FailingInput = new object[] { 42, "test" }
        };

        // Assert
        stats.Runs.Should().Be(1000);
        stats.Reverts.Should().Be(50);
        stats.AverageGas.Should().Be(75000);
        stats.MinGas.Should().Be(50000);
        stats.MaxGas.Should().Be(100000);
        stats.FailingInput.Should().HaveCount(2);
    }
}

// Sample test class for testing the test discovery
public class SampleFairyTest : FairyTest
{
    public void TestSampleMethod()
    {
        // This is a sample test method
    }

    public void TestAnotherMethod()
    {
        // Another sample test
    }

    public void TestFuzz_RandomInput(int value)
    {
        // Fuzz test with parameter
    }

    private void PrivateMethod()
    {
        // Should not be discovered
    }

    public int NonVoidMethod()
    {
        // Should not be discovered (returns int, not void/Task)
        return 0;
    }

    public void HelperMethod()
    {
        // Should not be discovered (doesn't start with Test)
    }
}
