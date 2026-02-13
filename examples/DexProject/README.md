# DEX Example Project

This is a complete example showing how to build a decentralized exchange (DEX) with Neo Fairy Framework.

## Project Structure

```
DexProject/
├── fairy.toml           # Project configuration
├── src/
│   ├── FungibleToken.cs # NEP-17 token implementation
│   ├── LiquidityPool.cs # AMM liquidity pool
│   ├── Router.cs        # Trade router
│   └── Deploy.cs        # Deployment script
├── test/
│   ├── FungibleToken.Test.cs
│   └── LiquidityPool.Test.cs
└── script/
    └── Deploy.cs        # Deployment helper
```

## Contracts

### FungibleToken

A standard NEP-17 fungible token with:

- `symbol()`, `decimals()`, `totalSupply()`, `balanceOf()`, `transfer()`
- Minting and burning capabilities

### LiquidityPool

An automated market maker (AMM) pool:

- Add/remove liquidity
- Token swaps with constant product formula (x \* y = k)
- LP token minting for liquidity providers

### Router

Trade routing contract:

- Multi-hop swap routing
- Price quotes
- Slippage protection

## Usage

### Build Contracts

```bash
fairy build
```

### Run Tests

```bash
fairy test
```

### Test with Coverage

```bash
fairy test --coverage
```

### Deploy to Session

```bash
fairy deploy --session dev
```

### Interact with Contracts

```bash
# Get token symbol
fairy call token symbol --session dev

# Transfer tokens
fairy send token transfer hash160:NXH... int:1000 --session dev
```

## Test Examples

```csharp
public class FungibleTokenTest : FairyTest
{
    public void TestMint()
    {
        var token = DeployAndBind<FungibleToken>("token");

        // Check initial balance
        Assert.Equal(0, token.BalanceOf(Owner));

        // Mint tokens
        token.Mint(Owner, 1000);

        // Verify balance
        Assert.Equal(1000, token.BalanceOf(Owner));
    }

    public void TestTransferWithPrank()
    {
        var token = DeployAndBind<FungibleToken>("token");
        var alice = MakeAccount();
        var bob = MakeAccount();

        // Setup: Give alice some tokens
        Vm.Deal(alice, 100_00000000); // 100 GAS
        token.Mint(alice, 1000);

        // Prank as alice to transfer
        Vm.Prank(alice);
        token.Transfer(alice, bob, 500);

        // Verify
        Assert.Equal(500, token.BalanceOf(alice));
        Assert.Equal(500, token.BalanceOf(bob));
    }
}
```

## Configuration

See `fairy.toml` for all configuration options:

- `[project]` - Project metadata
- `[compiler]` - Compiler settings (debug, optimize)
- `[fairy]` - RPC URL, network selection
- `[test]` - Test runner options (coverage, fuzz_runs)
- `[[contracts]]` - Contract definitions with dependencies
