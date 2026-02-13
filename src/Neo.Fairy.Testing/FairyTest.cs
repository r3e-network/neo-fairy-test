// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.CodeGen;
using Neo.Fairy.Core.Debugging;
using Neo.Fairy.Core.Interfaces;
using Neo.Fairy.Core.Models;
using Neo.Fairy.Engine;
using Neo.Fairy.Testing.Cheatcodes;

namespace Neo.Fairy.Testing;

/// <summary>
/// Base class for Fairy tests.
/// Provides Foundry-style test infrastructure for Neo N3 smart contracts.
/// Implements IContractInvoker to support generated contract interfaces.
/// </summary>
/// <remarks>
/// Test methods must:
/// - Be public
/// - Return void or Task
/// - Start with "Test" prefix (e.g., TestMint, TestTransfer)
/// - Fuzz tests start with "TestFuzz_" and have parameters
/// </remarks>
public abstract class FairyTest : IDisposable, IContractInvoker
{
    private IFairySession? _session;
    private FairyRpcClient? _rpcClient;
    private readonly Dictionary<string, string> _deployedContracts = new(StringComparer.OrdinalIgnoreCase);
    private long _gasConsumed;
    private bool _collectCoverage;
    private bool _disposed;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> DebugInfoRegistered =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the current test session.
    /// </summary>
    protected IFairySession Session => _session ?? throw new InvalidOperationException("Session not initialized. Call SetUp first.");

    /// <summary>
    /// Gets the cheatcodes interface for test manipulation.
    /// </summary>
    protected ICheatcodes Vm { get; private set; } = null!;

    /// <summary>
    /// Gets the assertion helper.
    /// </summary>
    protected static Assertions.Assert Assert => Assertions.Assert.Instance;

    /// <summary>
    /// Called before each test method. Override to set up test fixtures.
    /// </summary>
    public virtual void SetUp()
    {
    }

    /// <summary>
    /// Called after each test method. Override to clean up test fixtures.
    /// </summary>
    public virtual void TearDown()
    {
    }

    /// <summary>
    /// Called once before all tests in the class.
    /// </summary>
    public virtual void SetUpClass()
    {
    }

    /// <summary>
    /// Called once after all tests in the class.
    /// </summary>
    public virtual void TearDownClass()
    {
    }

    /// <summary>
    /// Deploys a contract by alias and returns its hash.
    /// </summary>
    /// <param name="alias">The contract alias from fairy.toml.</param>
    /// <returns>The deployed contract hash.</returns>
    protected string Deploy(string alias)
    {
        if (_deployedContracts.TryGetValue(alias, out var existingHash))
        {
            return existingHash;
        }

        // Deploy via Fairy RPC into the current test session.
        var hash = DeployInternal(alias);
        _deployedContracts[alias] = hash;
        return hash;
    }

    /// <summary>
    /// Deploys a contract from file paths.
    /// </summary>
    /// <param name="nefPath">Path to the .nef file.</param>
    /// <param name="manifestPath">Path to the manifest file.</param>
    /// <param name="alias">Optional alias for the contract.</param>
    /// <returns>The deployed contract hash.</returns>
    protected string DeployFromFiles(string nefPath, string manifestPath, string? alias = null)
    {
        alias ??= Path.GetFileNameWithoutExtension(nefPath);
        // Deploy explicit NEF+manifest into the current test session.
        var hash = DeployFromFilesInternal(nefPath, manifestPath);
        _deployedContracts[alias] = hash;
        return hash;
    }

    /// <summary>
    /// Invokes a contract method and returns the execution result.
    /// </summary>
    /// <param name="contractHash">The contract hash.</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The method arguments.</param>
    /// <returns>The execution result.</returns>
    protected ExecutionResult Call(string contractHash, string method, params object[] args)
    {
        return CallInternal(contractHash, method, args);
    }

    /// <summary>
    /// Invokes a contract method and returns the typed result.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="contractHash">The contract hash.</param>
    /// <param name="method">The method name.</param>
    /// <param name="args">The method arguments.</param>
    /// <returns>The typed result.</returns>
    protected T? Call<T>(string contractHash, string method, params object[] args)
    {
        var result = Call(contractHash, method, args);
        return result.GetResult<T>();
    }

    /// <summary>
    /// Creates a new test account with the specified balance.
    /// </summary>
    /// <param name="gasBalance">Initial GAS balance.</param>
    /// <returns>The account hash.</returns>
    protected string MakeAccount(long gasBalance = 100_00000000)
    {
        var account = GenerateAccountInternal();
        if (gasBalance > 0)
        {
            Vm.Deal(account, gasBalance);
        }
        return account;
    }

    /// <summary>
    /// Gets a deployed contract hash by alias.
    /// </summary>
    /// <param name="alias">The contract alias.</param>
    /// <returns>The contract hash.</returns>
    protected string GetContract(string alias)
    {
        if (_deployedContracts.TryGetValue(alias, out var hash))
        {
            return hash;
        }
        throw new InvalidOperationException($"Contract '{alias}' not deployed. Call Deploy(\"{alias}\") first.");
    }

    /// <summary>
    /// Logs a message during test execution.
    /// </summary>
    /// <param name="message">The message to log.</param>
    protected void Log(string message)
    {
        Console.WriteLine($"[{GetType().Name}] {message}");
    }

    /// <summary>
    /// Skips the current test with a reason.
    /// </summary>
    /// <param name="reason">The skip reason.</param>
    protected void Skip(string reason)
    {
        throw new TestSkippedException(reason);
    }

    /// <summary>
    /// Fails the current test with a message.
    /// </summary>
    /// <param name="message">The failure message.</param>
    protected void Fail(string message)
    {
        throw new TestFailedException(message);
    }

    #region IContractInvoker Implementation

    /// <summary>
    /// Invokes a contract method. Implementation of IContractInvoker.
    /// </summary>
    ExecutionResult IContractInvoker.Call(string contractHash, string method, params object[] args)
    {
        return Call(contractHash, method, args);
    }

    /// <summary>
    /// Invokes a contract method and returns a typed result. Implementation of IContractInvoker.
    /// </summary>
    T? IContractInvoker.Call<T>(string contractHash, string method, params object[] args) where T : default
    {
        return Call<T>(contractHash, method, args);
    }

    #endregion

    #region Contract Wrapper Support

    /// <summary>
    /// Creates a typed contract wrapper for the deployed contract.
    /// Use with generated contract interfaces for type-safe invocation.
    /// </summary>
    /// <typeparam name="T">The generated contract wrapper type.</typeparam>
    /// <param name="alias">The contract alias.</param>
    /// <returns>A typed contract wrapper instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the contract is not deployed or the type doesn't have the required constructor.
    /// </exception>
    /// <example>
    /// <code>
    /// var counter = Bind&lt;Counter&gt;("counter");
    /// var count = counter.GetCount();
    /// counter.Increment();
    /// </code>
    /// </example>
    protected T Bind<T>(string alias) where T : class
    {
        var contractHash = GetContract(alias);

        // Validate that T has the required constructor (IContractInvoker, string)
        var constructor = typeof(T).GetConstructor(new[] { typeof(IContractInvoker), typeof(string) });
        if (constructor == null)
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' must have a public constructor with signature " +
                $"(IContractInvoker invoker, string contractHash). " +
                $"Ensure you are using a generated contract wrapper class.");
        }

        try
        {
            return (T)constructor.Invoke(new object[] { this, contractHash });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of '{typeof(T).Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deploys a contract and returns a typed wrapper.
    /// Combines Deploy() and Bind() in one call.
    /// </summary>
    /// <typeparam name="T">The generated contract wrapper type.</typeparam>
    /// <param name="alias">The contract alias.</param>
    /// <returns>A typed contract wrapper instance.</returns>
    /// <example>
    /// <code>
    /// var counter = DeployAndBind&lt;Counter&gt;("counter");
    /// Assert.Equal(0, counter.GetCount());
    /// </code>
    /// </example>
    protected T DeployAndBind<T>(string alias) where T : class
    {
        Deploy(alias);
        return Bind<T>(alias);
    }

    #endregion

    #region Internal Implementation (connected to Fairy engine)

    /// <summary>
    /// Initializes the test session with Fairy engine components.
    /// Called by TestRunner before each test.
    /// </summary>
    internal void InitializeSession(
        IFairySession session,
        ICheatcodes cheatcodes,
        FairyRpcClient? rpcClient = null,
        bool collectCoverage = false)
    {
        _session = session;
        Vm = cheatcodes;
        _rpcClient = rpcClient;
        _gasConsumed = 0;
        _collectCoverage = collectCoverage;
        _deployedContracts.Clear();
    }

    /// <summary>
    /// Clears the session reference without disposing it.
    /// Used by TestRunner to prevent double-dispose when the runner owns the session lifetime.
    /// </summary>
    internal void ClearSession()
    {
        _session = null;
    }

    /// <summary>
    /// Clears the static debug-info registration cache.
    /// Called by TestRunner between assembly runs so contracts are re-registered.
    /// </summary>
    internal static void ClearDebugInfoCache()
    {
        DebugInfoRegistered.Clear();
    }

    internal void ResetGasCounter()
    {
        _gasConsumed = 0;
    }

    internal long GetGasConsumed()
    {
        return _gasConsumed;
    }

    private string DeployInternal(string alias)
    {
        if (_rpcClient == null || _session == null)
            throw new InvalidOperationException("Session not initialized. Ensure test is run via TestRunner.");

        // Try to load contract from project configuration
        FairyProject? project = null;
        Exception? loadException = null;

        try
        {
            project = FairyProject.Load();
        }
        catch (FileNotFoundException)
        {
            // fairy.toml not found - this is expected in some scenarios
        }
        catch (DirectoryNotFoundException)
        {
            // Project directory not found - this is expected in some scenarios
        }
        catch (Exception ex)
        {
            // Log other errors for debugging but continue
            loadException = ex;
            Log($"Warning: Failed to load fairy.toml: {ex.Message}");
        }

        if (project != null)
        {
            var contractConfig = project.GetContractByAlias(alias);
            if (contractConfig != null)
            {
                if (!contractConfig.IsCompiled)
                {
                    throw new InvalidOperationException(
                        $"Contract '{alias}' is not compiled. Run 'fairy build' first. " +
                        $"Expected NEF at: {contractConfig.NefPath}");
                }
                return DeployFromFilesInternal(contractConfig.NefPath, contractConfig.ManifestPath);
            }
        }

        var errorMessage = $"Contract '{alias}' not found. Either configure it in fairy.toml or use DeployFromFiles() with explicit paths.";
        if (loadException != null)
        {
            errorMessage += $" (fairy.toml load error: {loadException.Message})";
        }

        throw new InvalidOperationException(errorMessage);
    }

    private string DeployFromFilesInternal(string nefPath, string manifestPath)
    {
        if (_rpcClient == null || _session == null)
            throw new InvalidOperationException("Session not initialized. Ensure test is run via TestRunner.");

        // Load NEF and manifest files
        if (!File.Exists(nefPath))
            throw new FileNotFoundException($"NEF file not found: {nefPath}");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

        var nefBytes = File.ReadAllBytes(nefPath);
        var manifestJson = File.ReadAllText(manifestPath);

        // Deploy via RPC
        var result = _rpcClient.VirtualDeployAsync(
            _session.Id,
            nefBytes,
            manifestJson,
            null,
            null).GetAwaiter().GetResult();

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Deployment failed: {result.Exception ?? "Unknown error"}");
        }

        _gasConsumed += result.GasConsumed;

        // Register the contract in session
        var alias = Path.GetFileNameWithoutExtension(nefPath);
        _session.RegisterContract(alias, result.ContractHash);

        if (_collectCoverage)
        {
            Coverage.CoverageRegistry.Register(result.ContractHash, alias);
            TryRegisterDebugInfoForCoverage(result.ContractHash, nefPath);
        }

        return result.ContractHash;
    }

    private ExecutionResult CallInternal(string contractHash, string method, object[] args)
    {
        if (_rpcClient == null || _session == null)
            throw new InvalidOperationException("Session not initialized. Ensure test is run via TestRunner.");

        // Check for mocked calls before hitting the RPC
        if (Vm is FairyCheatcodes mockCheck)
        {
            var (isMocked, returnData, shouldRevert, revertMessage) = mockCheck.GetMock(contractHash, method);
            if (isMocked)
            {
                if (shouldRevert)
                {
                    var faultResult = new ExecutionResult
                    {
                        State = ExecutionState.Fault,
                        GasConsumed = 0,
                        Exception = revertMessage ?? "Mocked revert"
                    };
                    mockCheck.ValidateExpectations(faultResult, contractHash, method);
                    return faultResult;
                }

                var mockResult = new ExecutionResult
                {
                    State = ExecutionState.Halt,
                    GasConsumed = 0,
                    Stack = new[] { new StackItem { Type = "Any", Value = returnData } }
                };
                mockCheck.ValidateExpectations(mockResult, contractHash, method);
                return mockResult;
            }
        }

        // Get prank account if set (for Vm.Prank support)
        IReadOnlyList<SignerInfo>? signers = null;
        if (Vm is FairyCheatcodes cheatcodes)
        {
            var prankAccount = cheatcodes.GetPrankAccount();
            if (prankAccount != null)
            {
                signers = new[] { new SignerInfo { Account = prankAccount, Scopes = "CalledByEntry" } };
            }
        }

        // Execute via RPC
        var result = _rpcClient.InvokeFunctionWithSessionAsync(
            _session.Id,
            contractHash,
            method,
            args,
            true, // writeSnapshot - persist state changes
            signers).GetAwaiter().GetResult();

        // Validate expectations (ExpectRevert, ExpectEmit, ExpectCall)
        if (Vm is FairyCheatcodes fc)
        {
            fc.ValidateExpectations(result, contractHash, method);
        }

        _gasConsumed += result.GasConsumed;

        return result;
    }

    private void TryRegisterDebugInfoForCoverage(string contractHash, string nefPath)
    {
        if (_rpcClient == null)
            return;

        if (!DebugInfoRegistered.TryAdd(contractHash, true))
            return;

        var debugInfoPath = Path.ChangeExtension(nefPath, ".nefdbgnfo");
        if (!File.Exists(debugInfoPath))
        {
            Log($"Coverage: debug info not found for {nefPath}. Build with --debug.");
            return;
        }

        try
        {
            var dbgBytes = File.ReadAllBytes(debugInfoPath);

            var dumpCandidates = new[]
            {
                Path.ChangeExtension(nefPath, ".nef.txt"),
                Path.ChangeExtension(nefPath, ".nef.asm"),
                Path.ChangeExtension(nefPath, ".asm")
            };

            var dumpPath = dumpCandidates.FirstOrDefault(File.Exists);
            string dumpText;

            if (dumpPath != null)
            {
                dumpText = File.ReadAllText(dumpPath);
            }
            else
            {
                var nefBytes = File.ReadAllBytes(nefPath);
                if (!NefDumpGenerator.TryGenerateDumpNef(
                        nefBytes,
                        dbgBytes,
                        Path.GetDirectoryName(nefPath),
                        out dumpText,
                        out var genError))
                {
                    Log($"Coverage: dumpnef text not found for {nefPath}. {genError ?? "Provide a .nef.txt/.nef.asm file."}");
                    return;
                }

                // Cache the generated dump for tooling reuse.
                try
                {
                    dumpPath = Path.ChangeExtension(nefPath, ".nef.txt");
                    File.WriteAllText(dumpPath, dumpText);
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(dumpText))
            {
                Log($"Coverage: dumpnef text at {dumpPath ?? nefPath} is empty.");
                return;
            }

            _rpcClient.SetDebugInfoAsync(contractHash, dbgBytes, dumpText).GetAwaiter().GetResult();
            _rpcClient.ClearContractOpCodeCoverageAsync(contractHash).GetAwaiter().GetResult();
            Log($"Coverage: registered debug info for {contractHash}.");
        }
        catch (Exception ex)
        {
            Log($"Coverage: failed to register debug info: {ex.Message}");
        }
    }

    private string GenerateAccountInternal()
    {
        // Generate a random account hash for testing
        var bytes = new byte[20];
        Random.Shared.NextBytes(bytes);
        return "0x" + Convert.ToHexString(bytes).ToLower();
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _session?.Dispose();
                _session = null;
                _deployedContracts.Clear();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Exception thrown when a test is skipped.
/// </summary>
public sealed class TestSkippedException : Exception
{
    public TestSkippedException(string reason) : base(reason) { }
}

/// <summary>
/// Exception thrown when a test fails explicitly.
/// </summary>
public sealed class TestFailedException : Exception
{
    public TestFailedException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when an assertion fails.
/// </summary>
public sealed class AssertionFailedException : Exception
{
    public string? Expected { get; }
    public string? Actual { get; }

    public AssertionFailedException(string message, string? expected = null, string? actual = null)
        : base(message)
    {
        Expected = expected;
        Actual = actual;
    }
}

/// <summary>
/// Backward-compatible alias for <see cref="AssertionFailedException"/>.
/// Use <see cref="AssertionFailedException"/> in new code.
/// </summary>
[Obsolete("Use AssertionFailedException instead. This alias corrects the original typo and will be removed in v2.0.")]
public sealed class AssertionFailedExcepton : Exception
{
    public string? Expected { get; }
    public string? Actual { get; }

    public AssertionFailedExcepton(string message, string? expected = null, string? actual = null)
        : base(message)
    {
        Expected = expected;
        Actual = actual;
    }
}
