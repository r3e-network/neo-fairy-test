// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using Neo.Fairy.Core.CodeGen;
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
    private bool _disposed;

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

        // This would call the actual deployment logic
        // For now, return placeholder - actual implementation needs Fairy engine
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
        // Implementation would load and deploy the contract
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
        return (T)Activator.CreateInstance(typeof(T), this, contractHash)!;
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
    internal void InitializeSession(IFairySession session, ICheatcodes cheatcodes, FairyRpcClient? rpcClient = null)
    {
        _session = session;
        Vm = cheatcodes;
        _rpcClient = rpcClient;
    }

    private string DeployInternal(string alias)
    {
        if (_rpcClient == null || _session == null)
            throw new InvalidOperationException("Session not initialized. Ensure test is run via TestRunner.");

        // Try to load contract from project configuration
        FairyProject? project = null;
        try
        {
            project = FairyProject.Load();
        }
        catch
        {
            // Project config not available, alias must be a file path
        }

        if (project != null)
        {
            var contractConfig = project.GetContractByAlias(alias);
            if (contractConfig != null)
            {
                return DeployFromFilesInternal(contractConfig.NefPath, contractConfig.ManifestPath);
            }
        }

        throw new InvalidOperationException(
            $"Contract '{alias}' not found. Either configure it in fairy.toml or use DeployFromFiles() with explicit paths.");
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

        // Register the contract in session
        _session.RegisterContract(Path.GetFileNameWithoutExtension(nefPath), result.ContractHash);

        return result.ContractHash;
    }

    private ExecutionResult CallInternal(string contractHash, string method, object[] args)
    {
        if (_rpcClient == null || _session == null)
            throw new InvalidOperationException("Session not initialized. Ensure test is run via TestRunner.");

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

        // Validate expectations (ExpectRevert, ExpectEmit)
        if (Vm is FairyCheatcodes fc)
        {
            fc.ValidateExpectations(result);
        }

        return result;
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
