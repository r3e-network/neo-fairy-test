// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Neo.Fairy.Testing.Cheatcodes;
using System.Diagnostics;
using System.Reflection;

namespace Neo.Fairy.Testing.Runner;

/// <summary>
/// Discovers and executes Fairy tests.
/// </summary>
public sealed class TestRunner
{
    private readonly FairySessionFactory _sessionFactory;
    private readonly TestRunnerOptions _options;

    public TestRunner(string rpcUrl = "http://localhost:16868", TestRunnerOptions? options = null)
    {
        _sessionFactory = new FairySessionFactory(rpcUrl);
        _options = options ?? new TestRunnerOptions();
    }

    /// <summary>
    /// Runs all tests in the specified assembly.
    /// </summary>
    public async Task<TestSummary> RunAssemblyAsync(Assembly assembly)
    {
        var testClasses = DiscoverTestClasses(assembly);
        return await RunTestClassesAsync(testClasses);
    }

    /// <summary>
    /// Runs all tests in the specified test class type.
    /// </summary>
    public async Task<TestSummary> RunTestClassAsync<T>() where T : FairyTest, new()
    {
        return await RunTestClassesAsync(new[] { typeof(T) });
    }

    /// <summary>
    /// Runs tests matching the specified filter.
    /// </summary>
    public async Task<TestSummary> RunWithFilterAsync(Assembly assembly, string? classFilter, string? methodFilter)
    {
        var testClasses = DiscoverTestClasses(assembly);

        if (!string.IsNullOrEmpty(classFilter))
        {
            testClasses = testClasses
                .Where(t => t.Name.Contains(classFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return await RunTestClassesAsync(testClasses, methodFilter);
    }

    private async Task<TestSummary> RunTestClassesAsync(IReadOnlyList<Type> testClasses, string? methodFilter = null)
    {
        var allResults = new List<TestResult>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var testClass in testClasses)
        {
            var classResults = await RunSingleTestClassAsync(testClass, methodFilter);
            allResults.AddRange(classResults);

            if (_options.FailFast && classResults.Any(r => r.Failed))
            {
                break;
            }
        }

        stopwatch.Stop();

        return new TestSummary
        {
            Results = allResults,
            TotalDuration = stopwatch.Elapsed
        };
    }

    private async Task<List<TestResult>> RunSingleTestClassAsync(Type testClass, string? methodFilter)
    {
        var results = new List<TestResult>();
        var testMethods = DiscoverTestMethods(testClass);

        if (!string.IsNullOrEmpty(methodFilter))
        {
            testMethods = testMethods
                .Where(m => m.Name.Contains(methodFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Create test instance
        var instance = (FairyTest)Activator.CreateInstance(testClass)!;

        try
        {
            // Run SetUpClass once
            instance.SetUpClass();

            foreach (var method in testMethods)
            {
                var result = await RunSingleTestAsync(instance, method);
                results.Add(result);

                _options.OnTestCompleted?.Invoke(result);

                if (_options.FailFast && result.Failed)
                {
                    break;
                }
            }

            // Run TearDownClass once
            instance.TearDownClass();
        }
        finally
        {
            instance.Dispose();
        }

        return results;
    }

    private async Task<TestResult> RunSingleTestAsync(FairyTest instance, MethodInfo method)
    {
        var className = instance.GetType().Name;
        var testName = method.Name;
        var stopwatch = Stopwatch.StartNew();

        // Check if this is a fuzz test
        var isFuzzTest = testName.StartsWith("TestFuzz_", StringComparison.OrdinalIgnoreCase);
        var parameters = method.GetParameters();

        if (isFuzzTest && parameters.Length > 0)
        {
            return await RunFuzzTestAsync(instance, method, className, testName);
        }

        // Create a fresh session for this test
        var session = _sessionFactory.CreateSession($"{className}_{testName}_{Guid.NewGuid():N}");
        var cheatcodes = new FairyCheatcodes(session, _sessionFactory.RpcClient);

        try
        {
            // Initialize the test instance with session, cheatcodes, and RPC client
            instance.InitializeSession(session, cheatcodes, _sessionFactory.RpcClient);

            // Run SetUp
            instance.SetUp();

            // Run the test method
            if (method.ReturnType == typeof(Task))
            {
                await (Task)method.Invoke(instance, null)!;
            }
            else
            {
                method.Invoke(instance, null);
            }

            // Run TearDown
            instance.TearDown();

            stopwatch.Stop();

            return TestResult.Pass(className, testName, stopwatch.Elapsed);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            stopwatch.Stop();
            return HandleTestException(className, testName, stopwatch.Elapsed, ex.InnerException);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleTestException(className, testName, stopwatch.Elapsed, ex);
        }
        finally
        {
            session.Dispose();
        }
    }

    private async Task<TestResult> RunFuzzTestAsync(FairyTest instance, MethodInfo method, string className, string testName)
    {
        var parameters = method.GetParameters();
        var runs = _options.FuzzRuns;
        var random = new Random(_options.FuzzSeed ?? Environment.TickCount);

        var gasValues = new List<long>();
        var revertCount = 0;
        object[]? failingInput = null;
        Exception? failingException = null;

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < runs; i++)
        {
            var session = _sessionFactory.CreateSession($"{className}_{testName}_fuzz_{i}");
            var cheatcodes = new FairyCheatcodes(session, _sessionFactory.RpcClient);

            try
            {
                instance.InitializeSession(session, cheatcodes, _sessionFactory.RpcClient);
                instance.SetUp();

                // Generate random arguments
                var args = GenerateFuzzArguments(parameters, random);

                try
                {
                    if (method.ReturnType == typeof(Task))
                    {
                        await (Task)method.Invoke(instance, args)!;
                    }
                    else
                    {
                        method.Invoke(instance, args);
                    }

                    // Track gas if available
                    // gasValues.Add(session.LastGasConsumed);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is AssumeViolationException)
                {
                    // Skip this input - assume condition failed
                    continue;
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    failingInput = args;
                    failingException = ex.InnerException;
                    break;
                }

                instance.TearDown();
            }
            finally
            {
                session.Dispose();
            }
        }

        stopwatch.Stop();

        if (failingException != null)
        {
            var result = HandleTestException(className, testName, stopwatch.Elapsed, failingException);
            return result with
            {
                FuzzStats = new FuzzStats
                {
                    Runs = runs,
                    Reverts = revertCount,
                    FailingInput = failingInput
                }
            };
        }

        return new TestResult
        {
            ClassName = className,
            TestName = testName,
            Status = TestStatus.Passed,
            Duration = stopwatch.Elapsed,
            FuzzStats = new FuzzStats
            {
                Runs = runs,
                Reverts = revertCount,
                AverageGas = gasValues.Count > 0 ? gasValues.Average() : 0,
                MinGas = gasValues.Count > 0 ? gasValues.Min() : 0,
                MaxGas = gasValues.Count > 0 ? gasValues.Max() : 0
            }
        };
    }

    private static object[] GenerateFuzzArguments(ParameterInfo[] parameters, Random random)
    {
        var args = new object[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            args[i] = GenerateFuzzValue(parameters[i].ParameterType, random);
        }

        return args;
    }

    private static object GenerateFuzzValue(Type type, Random random)
    {
        if (type == typeof(int)) return random.Next();
        if (type == typeof(uint)) return (uint)random.Next();
        if (type == typeof(long)) return (long)random.Next() << 32 | (uint)random.Next();
        if (type == typeof(ulong)) return (ulong)random.Next() << 32 | (uint)random.Next();
        if (type == typeof(short)) return (short)random.Next(short.MinValue, short.MaxValue);
        if (type == typeof(ushort)) return (ushort)random.Next(0, ushort.MaxValue);
        if (type == typeof(byte)) return (byte)random.Next(0, 256);
        if (type == typeof(sbyte)) return (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue);
        if (type == typeof(bool)) return random.Next(2) == 1;
        if (type == typeof(uint96)) return (uint)random.Next(); // Simplified
        if (type == typeof(string))
        {
            var length = random.Next(0, 100);
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = (char)random.Next(32, 127);
            }
            return new string(chars);
        }
        if (type == typeof(byte[]))
        {
            var length = random.Next(0, 100);
            var bytes = new byte[length];
            random.NextBytes(bytes);
            return bytes;
        }

        throw new NotSupportedException($"Fuzz generation not supported for type {type.Name}");
    }

    private static TestResult HandleTestException(string className, string testName, TimeSpan duration, Exception ex)
    {
        if (ex is TestSkippedException skip)
        {
            return TestResult.Skip(className, testName, skip.Message);
        }

        if (ex is AssertionFailedException assertion)
        {
            return TestResult.Fail(
                className,
                testName,
                duration,
                assertion.Message,
                assertion.StackTrace,
                assertion.Expected,
                assertion.Actual);
        }

        return TestResult.Fail(
            className,
            testName,
            duration,
            ex.Message,
            ex.StackTrace);
    }

    private static List<Type> DiscoverTestClasses(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(FairyTest).IsAssignableFrom(t))
            .ToList();
    }

    private static List<MethodInfo> DiscoverTestMethods(Type testClass)
    {
        return testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.ReturnType == typeof(void) || m.ReturnType == typeof(Task))
            .OrderBy(m => m.Name)
            .ToList();
    }
}

/// <summary>
/// Options for the test runner.
/// </summary>
public sealed class TestRunnerOptions
{
    /// <summary>
    /// Stop on first test failure.
    /// </summary>
    public bool FailFast { get; set; }

    /// <summary>
    /// Number of fuzz test runs.
    /// </summary>
    public int FuzzRuns { get; set; } = 256;

    /// <summary>
    /// Seed for fuzz test random generation.
    /// </summary>
    public int? FuzzSeed { get; set; }

    /// <summary>
    /// Callback invoked when a test completes.
    /// </summary>
    public Action<TestResult>? OnTestCompleted { get; set; }

    /// <summary>
    /// Verbosity level (0-4).
    /// </summary>
    public int Verbosity { get; set; } = 2;

    /// <summary>
    /// Whether to collect code coverage.
    /// </summary>
    public bool CollectCoverage { get; set; }
}

// Placeholder for uint96 type used in fuzz tests
internal struct uint96
{
    public uint Value;
    public static implicit operator uint(uint96 v) => v.Value;
    public static implicit operator uint96(uint v) => new() { Value = v };
}
