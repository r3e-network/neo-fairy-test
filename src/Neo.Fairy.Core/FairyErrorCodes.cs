// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Core;

/// <summary>
/// Standardized error codes for Neo Fairy Framework.
/// Compatible with JSON-RPC 2.0 error code conventions.
/// </summary>
public static class FairyErrorCodes
{
    #region JSON-RPC Standard Errors (-32700 to -32600)

    /// <summary>Parse error: Invalid JSON received.</summary>
    public const int ParseError = -32700;

    /// <summary>Invalid Request: Not a valid Request object.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>Method not found: The method does not exist.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Invalid params: Invalid method parameters.</summary>
    public const int InvalidParams = -32602;

    /// <summary>Internal error: Internal JSON-RPC error.</summary>
    public const int InternalError = -32603;

    #endregion

    #region Neo N3 VM Errors (-100 to -199)

    /// <summary>VM execution halted successfully (not an error).</summary>
    public const int VmHalt = 0;

    /// <summary>VM execution faulted.</summary>
    public const int VmFault = -100;

    /// <summary>Out of GAS during execution.</summary>
    public const int OutOfGas = -101;

    /// <summary>Stack overflow in VM.</summary>
    public const int StackOverflow = -102;

    /// <summary>Stack underflow in VM.</summary>
    public const int StackUnderflow = -103;

    /// <summary>Invalid opcode encountered.</summary>
    public const int InvalidOpcode = -104;

    /// <summary>Invalid script format.</summary>
    public const int InvalidScript = -105;

    /// <summary>Assertion failed (ASSERT opcode).</summary>
    public const int AssertionFailed = -106;

    /// <summary>Execution aborted (ABORT opcode).</summary>
    public const int Aborted = -107;

    /// <summary>Null reference exception in contract.</summary>
    public const int NullReference = -108;

    /// <summary>Array index out of bounds.</summary>
    public const int IndexOutOfRange = -109;

    /// <summary>Division by zero.</summary>
    public const int DivisionByZero = -110;

    /// <summary>Integer overflow.</summary>
    public const int IntegerOverflow = -111;

    #endregion

    #region Contract Errors (-200 to -299)

    /// <summary>Contract not found on chain.</summary>
    public const int ContractNotFound = -200;

    /// <summary>Method not found in contract manifest.</summary>
    public const int ContractMethodNotFound = -201;

    /// <summary>Invalid contract parameters.</summary>
    public const int ContractInvalidParams = -202;

    /// <summary>Contract verification failed.</summary>
    public const int ContractVerificationFailed = -203;

    /// <summary>Contract deployment failed.</summary>
    public const int ContractDeploymentFailed = -204;

    /// <summary>Contract already exists.</summary>
    public const int ContractAlreadyExists = -205;

    /// <summary>Contract manifest validation failed.</summary>
    public const int ManifestValidationFailed = -206;

    /// <summary>NEF file validation failed.</summary>
    public const int NefValidationFailed = -207;

    #endregion

    #region Session Errors (-300 to -399)

    /// <summary>Session not found.</summary>
    public const int SessionNotFound = -300;

    /// <summary>Session expired.</summary>
    public const int SessionExpired = -301;

    /// <summary>Session limit exceeded.</summary>
    public const int SessionLimitExceeded = -302;

    /// <summary>Invalid session ID format.</summary>
    public const int InvalidSessionId = -303;

    /// <summary>Snapshot not found.</summary>
    public const int SnapshotNotFound = -310;

    /// <summary>Failed to create snapshot.</summary>
    public const int SnapshotCreationFailed = -311;

    /// <summary>Failed to revert to snapshot.</summary>
    public const int SnapshotRevertFailed = -312;

    #endregion

    #region Workspace Errors (-400 to -499)

    /// <summary>Workspace not found.</summary>
    public const int WorkspaceNotFound = -400;

    /// <summary>Contract alias not found in workspace.</summary>
    public const int AliasNotFound = -401;

    /// <summary>Circular dependency detected in contracts.</summary>
    public const int CircularDependency = -402;

    /// <summary>Missing dependency in workspace.</summary>
    public const int MissingDependency = -403;

    /// <summary>Workspace configuration invalid.</summary>
    public const int WorkspaceConfigInvalid = -404;

    #endregion

    #region Debugging Errors (-500 to -599)

    /// <summary>Debug info not found for contract.</summary>
    public const int DebugInfoNotFound = -500;

    /// <summary>Breakpoint invalid or not set.</summary>
    public const int BreakpointInvalid = -501;

    /// <summary>Debug session not active.</summary>
    public const int DebugSessionNotActive = -502;

    /// <summary>Source file not found for debugging.</summary>
    public const int SourceFileNotFound = -503;

    /// <summary>Source line number out of range.</summary>
    public const int SourceLineInvalid = -504;

    #endregion

    #region Test Framework Errors (-600 to -699)

    /// <summary>Test assertion failed.</summary>
    public const int AssertionFailedTest = -600;

    /// <summary>Test expectation not met (ExpectRevert, ExpectEmit).</summary>
    public const int ExpectationNotMet = -601;

    /// <summary>Fuzz input rejected by Assume().</summary>
    public const int FuzzInputRejected = -602;

    /// <summary>Test timeout exceeded.</summary>
    public const int TestTimeout = -603;

    /// <summary>Test setup failed.</summary>
    public const int TestSetupFailed = -604;

    /// <summary>Test teardown failed.</summary>
    public const int TestTeardownFailed = -605;

    /// <summary>Coverage threshold not met.</summary>
    public const int CoverageThresholdNotMet = -610;

    #endregion

    #region Wallet Errors (-700 to -799)

    /// <summary>Wallet not found.</summary>
    public const int WalletNotFound = -700;

    /// <summary>Invalid wallet password.</summary>
    public const int WalletPasswordInvalid = -701;

    /// <summary>Account not found in wallet.</summary>
    public const int AccountNotFound = -702;

    /// <summary>Insufficient balance for transaction.</summary>
    public const int InsufficientBalance = -703;

    /// <summary>Transaction signing failed.</summary>
    public const int SigningFailed = -704;

    #endregion

    #region Network Errors (-800 to -899)

    /// <summary>Network connection failed.</summary>
    public const int NetworkConnectionFailed = -800;

    /// <summary>RPC endpoint unreachable.</summary>
    public const int RpcUnreachable = -801;

    /// <summary>Transaction broadcast failed.</summary>
    public const int BroadcastFailed = -802;

    /// <summary>Transaction not confirmed in time.</summary>
    public const int TransactionTimeout = -803;

    /// <summary>Invalid network configuration.</summary>
    public const int NetworkConfigInvalid = -804;

    #endregion

    /// <summary>
    /// Gets a human-readable message for an error code.
    /// </summary>
    public static string GetMessage(int code) => code switch
    {
        ParseError => "Parse error: Invalid JSON",
        InvalidRequest => "Invalid request object",
        MethodNotFound => "Method not found",
        InvalidParams => "Invalid parameters",
        InternalError => "Internal error",

        VmHalt => "Execution completed successfully",
        VmFault => "VM execution faulted",
        OutOfGas => "Out of GAS",
        StackOverflow => "Stack overflow",
        StackUnderflow => "Stack underflow",
        InvalidOpcode => "Invalid opcode",
        InvalidScript => "Invalid script",
        AssertionFailed => "Assertion failed (ASSERT)",
        Aborted => "Execution aborted (ABORT)",
        NullReference => "Null reference",
        IndexOutOfRange => "Index out of range",
        DivisionByZero => "Division by zero",
        IntegerOverflow => "Integer overflow",

        ContractNotFound => "Contract not found",
        ContractMethodNotFound => "Contract method not found",
        ContractInvalidParams => "Invalid contract parameters",
        ContractVerificationFailed => "Contract verification failed",
        ContractDeploymentFailed => "Contract deployment failed",
        ContractAlreadyExists => "Contract already exists",
        ManifestValidationFailed => "Manifest validation failed",
        NefValidationFailed => "NEF validation failed",

        SessionNotFound => "Session not found",
        SessionExpired => "Session expired",
        SessionLimitExceeded => "Session limit exceeded",
        InvalidSessionId => "Invalid session ID",
        SnapshotNotFound => "Snapshot not found",
        SnapshotCreationFailed => "Failed to create snapshot",
        SnapshotRevertFailed => "Failed to revert to snapshot",

        WorkspaceNotFound => "Workspace not found",
        AliasNotFound => "Contract alias not found",
        CircularDependency => "Circular dependency detected",
        MissingDependency => "Missing dependency",
        WorkspaceConfigInvalid => "Invalid workspace configuration",

        DebugInfoNotFound => "Debug info not found",
        BreakpointInvalid => "Invalid breakpoint",
        DebugSessionNotActive => "Debug session not active",
        SourceFileNotFound => "Source file not found",
        SourceLineInvalid => "Invalid source line",

        AssertionFailedTest => "Test assertion failed",
        ExpectationNotMet => "Test expectation not met",
        FuzzInputRejected => "Fuzz input rejected",
        TestTimeout => "Test timeout",
        TestSetupFailed => "Test setup failed",
        TestTeardownFailed => "Test teardown failed",
        CoverageThresholdNotMet => "Coverage threshold not met",

        WalletNotFound => "Wallet not found",
        WalletPasswordInvalid => "Invalid wallet password",
        AccountNotFound => "Account not found",
        InsufficientBalance => "Insufficient balance",
        SigningFailed => "Signing failed",

        NetworkConnectionFailed => "Network connection failed",
        RpcUnreachable => "RPC endpoint unreachable",
        BroadcastFailed => "Broadcast failed",
        TransactionTimeout => "Transaction timeout",
        NetworkConfigInvalid => "Invalid network configuration",

        _ => $"Unknown error (code: {code})"
    };
}

/// <summary>
/// Structured exception with error code for JSON-RPC compatibility.
/// </summary>
public class FairyException : Exception
{
    /// <summary>Gets the error code.</summary>
    public int Code { get; }

    /// <summary>Gets additional error data.</summary>
    public object? ErrorData { get; }

    public FairyException(int code, string? message = null, object? data = null)
        : base(message ?? FairyErrorCodes.GetMessage(code))
    {
        Code = code;
        ErrorData = data;
    }

    public FairyException(int code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}
