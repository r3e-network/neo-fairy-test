// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Neo.Fairy.Testing.Cheatcodes;
using Neo.Fairy.Testing.Coverage;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
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
        CoverageRegistry.Clear();
        FairyTest.ClearDebugInfoCache();
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

        if (_options.Parallel && !_options.FailFast && testClasses.Count > 1)
        {
            // Run test classes in parallel
            var maxParallelism = Math.Max(1, _options.MaxParallelism);
            using var semaphore = new SemaphoreSlim(maxParallelism);
            var tasks = testClasses.Select(async testClass =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await RunSingleTestClassAsync(testClass, methodFilter);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            foreach (var classResults in results)
            {
                allResults.AddRange(classResults);
            }
        }
        else
        {
            // Run test classes sequentially
            foreach (var testClass in testClasses)
            {
                var classResults = await RunSingleTestClassAsync(testClass, methodFilter);
                allResults.AddRange(classResults);

                if (_options.FailFast && classResults.Any(r => r.Failed))
                {
                    break;
                }
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
        FairyTest instance;
        try
        {
            instance = (FairyTest)Activator.CreateInstance(testClass)!;
        }
        catch (Exception ex)
        {
            var innerEx = ex is System.Reflection.TargetInvocationException tie ? tie.InnerException ?? ex : ex;
            results.Add(TestResult.Fail(
                testClass.Name,
                "(constructor)",
                TimeSpan.Zero,
                $"Test class constructor threw: {innerEx.Message}",
                innerEx.StackTrace));
            return results;
        }

        try
        {
            // Run SetUpClass once
            try
            {
                instance.SetUpClass();
            }
            catch (Exception ex)
            {
                results.Add(TestResult.Fail(
                    testClass.Name,
                    "(SetUpClass)",
                    TimeSpan.Zero,
                    $"SetUpClass threw: {ex.Message}",
                    ex.StackTrace));
                instance.Dispose();
                return results;
            }

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
            try { instance.TearDownClass(); } catch { /* TearDownClass failure must not mask test results */ }
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

        if (parameters.Length > 0)
        {
            if (isFuzzTest)
            {
                return await RunFuzzTestAsync(instance, method, className, testName);
            }
            // Non-fuzz methods with parameters cannot be invoked directly; skip them.
            return TestResult.Skip(className, testName, $"Parameterized method requires TestFuzz_ prefix");
        }

        // Create a fresh session for this test
        var session = _sessionFactory.CreateSession($"{className}_{testName}_{Guid.NewGuid():N}");
        var cheatcodes = new FairyCheatcodes(session, _sessionFactory.RpcClient);

        var setUpCalled = false;
        try
        {
            // Initialize the test instance with session, cheatcodes, and RPC client
            instance.InitializeSession(session, cheatcodes, _sessionFactory.RpcClient, _options.CollectCoverage);
            instance.ResetGasCounter();

            // Run SetUp
            instance.SetUp();
            setUpCalled = true;

            // Run the test method
            if (method.ReturnType == typeof(Task))
            {
                var task = method.Invoke(instance, null) as Task;
                if (task != null) await task;
            }
            else
            {
                method.Invoke(instance, null);
            }

            // Validate end-of-test expectations (ExpectCallCount, unconsumed ExpectCall)
            if (cheatcodes is FairyCheatcodes fc)
            {
                fc.ValidateFinalExpectations();
            }

            stopwatch.Stop();

            return TestResult.Pass(className, testName, stopwatch.Elapsed, instance.GetGasConsumed());
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            stopwatch.Stop();
            var gasConsumed = instance.GetGasConsumed();
            return HandleTestException(className, testName, stopwatch.Elapsed, gasConsumed, ex.InnerException);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var gasConsumed = instance.GetGasConsumed();
            return HandleTestException(className, testName, stopwatch.Elapsed, gasConsumed, ex);
        }
        finally
        {
            if (setUpCalled)
            {
                try { instance.TearDown(); } catch { /* TearDown failure must not mask the original exception */ }
            }
            instance.ClearSession(); // prevent double-dispose when instance.Dispose() is called later
            session.Dispose();
        }
    }

    private async Task<TestResult> RunFuzzTestAsync(FairyTest instance, MethodInfo method, string className, string testName)
    {
        var parameters = method.GetParameters();
        var runs = _options.FuzzRuns;
        if (runs <= 0) runs = TestDefaults.DefaultFuzzRuns;
        var seed = _options.FuzzSeed ?? Environment.TickCount;
        var random = new Random(seed);

        var gasValues = new List<long>();
        var revertCount = 0;
        var successCount = 0;
        object[]? failingInput = null;
        Exception? failingException = null;

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < runs; i++)
        {
            var session = _sessionFactory.CreateSession($"{className}_{testName}_fuzz_{i}");
            var cheatcodes = new FairyCheatcodes(session, _sessionFactory.RpcClient);

            try
            {
                instance.InitializeSession(session, cheatcodes, _sessionFactory.RpcClient, _options.CollectCoverage);
                instance.ResetGasCounter();
                instance.SetUp();

                // Generate random arguments
                var args = GenerateFuzzArguments(parameters, random);

                try
                {
                    if (method.ReturnType == typeof(Task))
                    {
                        var task = method.Invoke(instance, args) as Task;
                        if (task != null) await task;
                    }
                    else
                    {
                        method.Invoke(instance, args);
                    }

                    // Track gas if available
                    gasValues.Add(instance.GetGasConsumed());
                    successCount++;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is AssumeViolationException)
                {
                    // Skip this input - assume condition failed (counts as revert)
                    revertCount++;
                    continue;
                }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    failingInput = args;
                    failingException = ex.InnerException;
                    gasValues.Add(instance.GetGasConsumed());
                    break;
                }
            }
            finally
            {
                try { instance.TearDown(); } catch { /* TearDown failure must not mask the original exception */ }
                instance.ClearSession(); // prevent double-dispose when instance.Dispose() is called later
                session.Dispose();
            }
        }

        stopwatch.Stop();

        if (failingException != null)
        {
            var gas = gasValues.Count > 0 ? gasValues.Last() : 0;
            var result = HandleTestException(className, testName, stopwatch.Elapsed, gas, failingException);
            return result with
            {
                FuzzStats = new FuzzStats
                {
                    Runs = successCount + revertCount + 1,
                    Reverts = revertCount,
                    Seed = seed,
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
            GasConsumed = gasValues.Count > 0 ? (long)gasValues.Average() : 0,
            FuzzStats = new FuzzStats
            {
                Runs = runs,
                Reverts = revertCount,
                Seed = seed,
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
        // Nullable support: sometimes return null.
        var underlyingNullable = Nullable.GetUnderlyingType(type);
        if (underlyingNullable != null)
        {
            if (random.NextDouble() < 0.1)
            {
                return null!;
            }

            return GenerateFuzzValue(underlyingNullable, random);
        }

        // Enum support: pick any defined value.
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(random.Next(values.Length))!;
        }

        if (type == typeof(int))
        {
            var bytes = new byte[4];
            random.NextBytes(bytes);
            return BitConverter.ToInt32(bytes);
        }
        if (type == typeof(uint))
        {
            var bytes = new byte[4];
            random.NextBytes(bytes);
            return BitConverter.ToUInt32(bytes);
        }
        if (type == typeof(long))
        {
            var bytes = new byte[8];
            random.NextBytes(bytes);
            return BitConverter.ToInt64(bytes);
        }
        if (type == typeof(ulong))
        {
            var bytes = new byte[8];
            random.NextBytes(bytes);
            return BitConverter.ToUInt64(bytes);
        }
        if (type == typeof(short)) return (short)random.Next(short.MinValue, short.MaxValue + 1);
        if (type == typeof(ushort)) return (ushort)random.Next(0, ushort.MaxValue + 1);
        if (type == typeof(byte)) return (byte)random.Next(0, 256);
        if (type == typeof(sbyte)) return (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue + 1);
        if (type == typeof(bool)) return random.Next(2) == 1;
        if (type == typeof(uint96)) return uint96.FromRandom(random);
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

        if (type == typeof(BigInteger))
        {
            var length = random.Next(1, 33);
            var bytes = new byte[length];
            random.NextBytes(bytes);
            var bi = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
            return random.Next(2) == 0 ? bi : BigInteger.Negate(bi);
        }

        // Neo hash types
        if (type == typeof(Neo.UInt160))
        {
            var bytes = new byte[Neo.UInt160.Length];
            random.NextBytes(bytes);
            return new Neo.UInt160(bytes);
        }

        if (type == typeof(Neo.UInt256))
        {
            var bytes = new byte[Neo.UInt256.Length];
            random.NextBytes(bytes);
            return new Neo.UInt256(bytes);
        }

        if (type.Name == "UInt160")
        {
            var bytes = new byte[20];
            random.NextBytes(bytes);
            var hex = "0x" + Convert.ToHexString(bytes).ToLowerInvariant();

            var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
            if (parse != null)
            {
                return parse.Invoke(null, new object[] { hex })!;
            }

            var implicitFromString = type.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
            if (implicitFromString != null)
            {
                return implicitFromString.Invoke(null, new object[] { hex })!;
            }
        }

        if (type.Name == "UInt256")
        {
            var bytes = new byte[32];
            random.NextBytes(bytes);
            var hex = "0x" + Convert.ToHexString(bytes).ToLowerInvariant();

            var parse = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
            if (parse != null)
            {
                return parse.Invoke(null, new object[] { hex })!;
            }

            var implicitFromString = type.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, new[] { typeof(string) });
            if (implicitFromString != null)
            {
                return implicitFromString.Invoke(null, new object[] { hex })!;
            }
        }

        // Arrays of supported element types.
        if (type.IsArray && type != typeof(byte[]))
        {
            var elementType = type.GetElementType();
            if (elementType == null)
                throw new NotSupportedException($"Fuzz generation not supported for array type {type.Name}");

            var length = random.Next(0, 10);
            var array = Array.CreateInstance(elementType, length);
            for (int i = 0; i < length; i++)
            {
                array.SetValue(GenerateFuzzValue(elementType, random), i);
            }
            return array;
        }

        // Lists of supported element types.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var length = random.Next(0, 10);
            var list = (IList)Activator.CreateInstance(type)!;

            for (int i = 0; i < length; i++)
            {
                list.Add(GenerateFuzzValue(elementType, random));
            }

            return list;
        }

        throw new NotSupportedException($"Fuzz generation not supported for type {type.Name}");
    }

    private static TestResult HandleTestException(string className, string testName, TimeSpan duration, long gasConsumed, Exception ex)
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
                assertion.Actual,
                gasConsumed);
        }

        return TestResult.Fail(
            className,
            testName,
            duration,
            ex.Message,
            ex.StackTrace,
            gasConsumed: gasConsumed);
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
    public int FuzzRuns { get; set; } = TestDefaults.DefaultFuzzRuns;

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
    public int Verbosity { get; set; } = TestDefaults.DefaultVerbosity;

    /// <summary>
    /// Whether to collect code coverage.
    /// </summary>
    public bool CollectCoverage { get; set; }

    /// <summary>
    /// Whether to run test classes in parallel.
    /// Note: Tests within a class are always run sequentially.
    /// </summary>
    public bool Parallel { get; set; }

    /// <summary>
    /// Maximum degree of parallelism when Parallel is enabled.
    /// Defaults to the number of processors.
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;
}

/// <summary>
/// Represents a 96-bit unsigned integer for fuzz testing.
/// Backed by BigInteger, clamped to [0, 2^96 - 1].
/// </summary>
internal readonly struct uint96
{
    private static readonly System.Numerics.BigInteger MaxValue96 = (System.Numerics.BigInteger.One << 96) - 1;

    public System.Numerics.BigInteger Value { get; }

    public uint96(System.Numerics.BigInteger value)
    {
        if (value < 0 || value > MaxValue96)
            throw new ArgumentOutOfRangeException(nameof(value), "Value must be in [0, 2^96 - 1].");
        Value = value;
    }

    public static implicit operator System.Numerics.BigInteger(uint96 v) => v.Value;

    public static uint96 FromRandom(Random random)
    {
        // Generate 12 random bytes (96 bits), force unsigned by ensuring high bit is 0
        var bytes = new byte[13]; // 12 data bytes + 1 zero byte for unsigned
        random.NextBytes(bytes.AsSpan(0, 12));
        bytes[12] = 0;
        return new uint96(new System.Numerics.BigInteger(bytes, isUnsigned: true));
    }

    public override string ToString() => Value.ToString();
}
