# Neo Fairy Error Codes

This document catalogs all error codes used by the Neo Fairy Framework. Error codes follow JSON-RPC 2.0 conventions for consistency with Neo N3 RPC interfaces.

## Error Code Ranges

| Range | Category | Description |
|-------|----------|-------------|
| -32700 to -32600 | JSON-RPC Standard | Standard JSON-RPC 2.0 errors |
| -100 to -199 | Neo N3 VM | Virtual machine execution errors |
| -200 to -299 | Contract | Smart contract deployment and invocation |
| -300 to -399 | Session | Fairy session and snapshot management |
| -400 to -499 | Workspace | Multi-contract workspace errors |
| -500 to -599 | Debugging | Debug session and breakpoint errors |
| -600 to -699 | Test Framework | Test assertions and expectations |
| -700 to -799 | Wallet | Wallet and signing operations |
| -800 to -899 | Network | Network and RPC connectivity |

---

## JSON-RPC Standard Errors (-32700 to -32600)

| Code | Name | Description |
|------|------|-------------|
| -32700 | `ParseError` | Invalid JSON received by the server |
| -32600 | `InvalidRequest` | The JSON sent is not a valid Request object |
| -32601 | `MethodNotFound` | The method does not exist or is not available |
| -32602 | `InvalidParams` | Invalid method parameters |
| -32603 | `InternalError` | Internal JSON-RPC error |

---

## Neo N3 VM Errors (-100 to -199)

| Code | Name | Description | Common Causes |
|------|------|-------------|---------------|
| 0 | `VmHalt` | Execution completed successfully | Not an error |
| -100 | `VmFault` | VM execution faulted | Unhandled exception in contract |
| -101 | `OutOfGas` | Insufficient GAS for execution | Increase gas limit or optimize contract |
| -102 | `StackOverflow` | VM stack overflow | Deep recursion or too many stack items |
| -103 | `StackUnderflow` | VM stack underflow | Malformed script or logic error |
| -104 | `InvalidOpcode` | Invalid VM opcode encountered | Corrupted NEF or unsupported operation |
| -105 | `InvalidScript` | Script format is invalid | Malformed NEF file |
| -106 | `AssertionFailed` | ASSERT opcode failed | Condition in contract returned false |
| -107 | `Aborted` | Execution aborted via ABORT opcode | Contract explicitly aborted |
| -108 | `NullReference` | Null reference in contract | Accessing uninitialized storage or null object |
| -109 | `IndexOutOfRange` | Array/buffer index out of bounds | Invalid array access |
| -110 | `DivisionByZero` | Division by zero | Math error in contract |
| -111 | `IntegerOverflow` | Integer overflow during arithmetic | Value exceeded BigInteger limits |

---

## Contract Errors (-200 to -299)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -200 | `ContractNotFound` | Contract not found on chain | Verify contract hash and deployment |
| -201 | `ContractMethodNotFound` | Method not in contract manifest | Check method name and manifest |
| -202 | `ContractInvalidParams` | Invalid parameters for method | Verify parameter types and count |
| -203 | `ContractVerificationFailed` | Contract verification failed | Check signers and witnesses |
| -204 | `ContractDeploymentFailed` | Contract deployment failed | Check NEF, manifest, and GAS balance |
| -205 | `ContractAlreadyExists` | Contract with same hash exists | Use update instead of deploy |
| -206 | `ManifestValidationFailed` | Manifest validation failed | Fix manifest JSON structure |
| -207 | `NefValidationFailed` | NEF file validation failed | Rebuild contract with valid compiler |

---

## Session Errors (-300 to -399)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -300 | `SessionNotFound` | Session does not exist | Create session or check session ID |
| -301 | `SessionExpired` | Session has expired | Create new session |
| -302 | `SessionLimitExceeded` | Too many active sessions | Clean up unused sessions |
| -303 | `InvalidSessionId` | Session ID format is invalid | Use valid session ID format |
| -310 | `SnapshotNotFound` | Snapshot does not exist | Create snapshot before reverting |
| -311 | `SnapshotCreationFailed` | Failed to create snapshot | Check session state |
| -312 | `SnapshotRevertFailed` | Failed to revert to snapshot | Verify snapshot ID |

---

## Workspace Errors (-400 to -499)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -400 | `WorkspaceNotFound` | Workspace does not exist | Create workspace first |
| -401 | `AliasNotFound` | Contract alias not in workspace | Register contract with alias |
| -402 | `CircularDependency` | Circular dependency detected | Fix contract dependency graph |
| -403 | `MissingDependency` | Required dependency not found | Deploy dependencies first |
| -404 | `WorkspaceConfigInvalid` | Workspace configuration invalid | Fix fairy.toml configuration |

---

## Debugging Errors (-500 to -599)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -500 | `DebugInfoNotFound` | Debug info not registered | Call setDebugInfo first |
| -501 | `BreakpointInvalid` | Breakpoint location invalid | Use valid line/instruction |
| -502 | `DebugSessionNotActive` | No active debug session | Start debug session first |
| -503 | `SourceFileNotFound` | Source file not in debug info | Rebuild with debug symbols |
| -504 | `SourceLineInvalid` | Line number out of range | Check source file |

---

## Test Framework Errors (-600 to -699)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -600 | `AssertionFailedTest` | Test assertion failed | Fix test or implementation |
| -601 | `ExpectationNotMet` | ExpectRevert/ExpectEmit failed | Verify expected behavior |
| -602 | `FuzzInputRejected` | Fuzz input failed Assume() | Normal for fuzzing, not an error |
| -603 | `TestTimeout` | Test exceeded time limit | Optimize test or increase timeout |
| -604 | `TestSetupFailed` | Test setup (SetUp) failed | Fix setup method |
| -605 | `TestTeardownFailed` | Test teardown failed | Fix teardown method |
| -610 | `CoverageThresholdNotMet` | Coverage below minimum | Add more test cases |

---

## Wallet Errors (-700 to -799)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -700 | `WalletNotFound` | Wallet file not found | Check wallet path |
| -701 | `WalletPasswordInvalid` | Wrong wallet password | Verify password |
| -702 | `AccountNotFound` | Account not in wallet | Check account address |
| -703 | `InsufficientBalance` | Not enough GAS/NEO | Fund the account |
| -704 | `SigningFailed` | Transaction signing failed | Check private key access |

---

## Network Errors (-800 to -899)

| Code | Name | Description | Resolution |
|------|------|-------------|------------|
| -800 | `NetworkConnectionFailed` | Network connection failed | Check network configuration |
| -801 | `RpcUnreachable` | RPC endpoint not responding | Verify RPC URL and node status |
| -802 | `BroadcastFailed` | Transaction broadcast failed | Check transaction validity |
| -803 | `TransactionTimeout` | Transaction not confirmed | Retry or check network |
| -804 | `NetworkConfigInvalid` | Invalid network configuration | Fix fairy.toml network settings |

---

## Using Error Codes in Code

### Throwing Errors

```csharp
using Neo.Fairy.Core;

// Throw with code only (message auto-generated)
throw new FairyException(FairyErrorCodes.ContractNotFound);

// Throw with custom message
throw new FairyException(FairyErrorCodes.ContractNotFound,
    "Contract 0x1234... not found in session 'test'");

// Throw with additional data
throw new FairyException(FairyErrorCodes.VmFault,
    "Execution failed",
    new { gasConsumed = 1500000, traceback = "..." });
```

### Handling Errors

```csharp
try
{
    var result = engine.InvokeMethod(session, contractHash, "transfer", args);
}
catch (FairyException ex) when (ex.Code == FairyErrorCodes.OutOfGas)
{
    Console.WriteLine($"Out of GAS: {ex.Message}");
}
catch (FairyException ex)
{
    Console.WriteLine($"Error {ex.Code}: {ex.Message}");
}
```

### Getting Error Messages

```csharp
// Get standard message for code
string message = FairyErrorCodes.GetMessage(FairyErrorCodes.VmFault);
// Returns: "VM execution faulted"
```

---

## JSON-RPC Error Response Format

Error responses follow the JSON-RPC 2.0 specification:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -100,
    "message": "VM execution faulted",
    "data": {
      "gasconsumed": "1500000",
      "traceback": "at Contract.Transfer()"
    }
  }
}
```
