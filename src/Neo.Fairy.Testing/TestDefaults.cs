// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Testing;

/// <summary>
/// Default values and constants for Neo Fairy testing framework.
/// Centralizes magic numbers and configurable defaults.
/// </summary>
public static class TestDefaults
{
    #region Gas & Balances

    /// <summary>
    /// Default GAS balance for test accounts (100 GAS).
    /// </summary>
    public const long DefaultTestAccountGas = 100_00000000;

    /// <summary>
    /// Default NEO balance for test accounts.
    /// </summary>
    public const long DefaultTestAccountNeo = 100;

    /// <summary>
    /// Maximum GAS for contract invocation (200 GAS).
    /// </summary>
    public const long MaxInvokeGas = 200_00000000;

    /// <summary>
    /// One GAS in fractions (10^8).
    /// </summary>
    public const long OneGas = 100_000_000;

    /// <summary>
    /// Minimum GAS for transaction fee.
    /// </summary>
    public const long MinNetworkFee = 1_000_000;

    #endregion

    #region Timeouts & Limits

    /// <summary>
    /// Default session timeout in seconds (24 hours).
    /// </summary>
    public const int DefaultSessionTimeoutSeconds = 86400;

    /// <summary>
    /// Default number of fuzz test runs.
    /// </summary>
    public const int DefaultFuzzRuns = 256;

    /// <summary>
    /// Maximum fuzz test runs.
    /// </summary>
    public const int MaxFuzzRuns = 10000;

    /// <summary>
    /// Default RPC request timeout in milliseconds.
    /// </summary>
    public const int DefaultRpcTimeoutMs = 30000;

    #endregion

    #region Networking

    /// <summary>
    /// Default RPC URL for local Fairy node.
    /// </summary>
    public const string DefaultRpcUrl = "http://localhost:16868";

    /// <summary>
    /// Default WebSocket URL for local Fairy node.
    /// </summary>
    public const string DefaultWebSocketUrl = "ws://localhost:16869";

    /// <summary>
    /// Default host address for RPC binding.
    /// </summary>
    public const string DefaultHost = "127.0.0.1";

    /// <summary>
    /// Default port for RPC server.
    /// </summary>
    public const int DefaultPort = 16868;

    #endregion

    #region Networks

    /// <summary>
    /// Neo N3 MainNet network magic number.
    /// </summary>
    public const uint MainNetMagic = 860833102;

    /// <summary>
    /// Neo N3 TestNet network magic number.
    /// </summary>
    public const uint TestNetMagic = 894710606;

    /// <summary>
    /// Private/Local network default magic number.
    /// </summary>
    public const uint PrivateNetMagic = 1234567890;

    #endregion

    #region Coverage

    /// <summary>
    /// Minimum acceptable code coverage percentage.
    /// </summary>
    public const double MinimumCoveragePercent = 80.0;

    /// <summary>
    /// Target code coverage percentage.
    /// </summary>
    public const double TargetCoveragePercent = 90.0;

    #endregion

    #region Test Framework

    /// <summary>
    /// Default verbosity level for test output (0-3).
    /// </summary>
    public const int DefaultVerbosity = 1;

    /// <summary>
    /// Maximum test execution time in seconds.
    /// </summary>
    public const int MaxTestExecutionSeconds = 300;

    /// <summary>
    /// Default retry count for flaky tests.
    /// </summary>
    public const int DefaultRetryCount = 0;

    #endregion
}
