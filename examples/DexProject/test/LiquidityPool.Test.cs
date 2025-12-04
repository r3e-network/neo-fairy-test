// LiquidityPool.Test.cs - Tests for the LiquidityPool contract
// Demonstrates advanced testing patterns for DeFi contracts

using Neo.Fairy.Testing;
using System.Numerics;

namespace NeoDex.Tests
{
    /// <summary>
    /// Test suite for LiquidityPool contract.
    /// Shows DeFi-specific testing patterns including:
    /// - Multi-contract interactions
    /// - Liquidity math verification
    /// - Slippage testing
    /// - Edge cases
    /// </summary>
    public class LiquidityPoolTest : FairyTest
    {
        private string tokenAHash = null!;
        private string tokenBHash = null!;
        private string poolHash = null!;

        private string owner = null!;
        private string alice = null!;
        private string bob = null!;

        private const long ONE_TOKEN = 100_000_000;
        private const long INITIAL_LIQUIDITY = 10000 * ONE_TOKEN;

        public override void SetUp()
        {
            // Deploy tokens first (pool depends on them)
            tokenAHash = Deploy("token"); // Reuse token contract as TokenA

            // For TokenB, we'd deploy another instance
            // In real scenario, deploy a second token contract
            tokenBHash = tokenAHash; // Simplified for example

            // Deploy the liquidity pool
            poolHash = Deploy("pool");

            // Create accounts
            owner = MakeAccount(10000 * ONE_TOKEN);
            alice = MakeAccount(1000 * ONE_TOKEN);
            bob = MakeAccount(1000 * ONE_TOKEN);

            // Mint tokens to users
            Vm.Prank(owner);
            Call(tokenAHash, "mint", alice, INITIAL_LIQUIDITY * 2);

            Vm.Prank(owner);
            Call(tokenAHash, "mint", bob, INITIAL_LIQUIDITY);
        }

        #region Add Liquidity Tests

        public void TestAddLiquidityInitial()
        {
            // First liquidity provider sets the initial ratio
            var amountA = 1000 * ONE_TOKEN;
            var amountB = 2000 * ONE_TOKEN;

            // Approve tokens to pool
            Vm.Prank(alice);
            Call(tokenAHash, "transfer", alice, poolHash, amountA, null);

            // Add liquidity
            Vm.Prank(alice);
            var result = Call(poolHash, "addLiquidity", amountA, amountB, 0, 0);

            Assert.Halted(result);
            Assert.EmittedEvent(result, "LiquidityAdded");

            // Check LP tokens received
            var lpBalance = Call<BigInteger>(poolHash, "balanceOf", alice);
            Assert.Greater(lpBalance, BigInteger.Zero);
        }

        public void TestAddLiquidityMaintainsRatio()
        {
            // Setup initial liquidity
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            // Bob adds liquidity - should maintain ratio
            var amountA = 500 * ONE_TOKEN;
            var expectedAmountB = 1000 * ONE_TOKEN; // 1:2 ratio

            Vm.Prank(bob);
            var result = Call(poolHash, "addLiquidity", amountA, expectedAmountB, 0, 0);

            Assert.Halted(result);
        }

        public void TestAddLiquiditySlippageProtection()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            // Bob tries to add with too much slippage
            var amountA = 500 * ONE_TOKEN;
            var amountB = 800 * ONE_TOKEN; // Wrong ratio
            var minLpTokens = 1000 * ONE_TOKEN; // High minimum

            Vm.ExpectRevert("Slippage exceeded");

            Vm.Prank(bob);
            Call(poolHash, "addLiquidity", amountA, amountB, minLpTokens, 0);
        }

        #endregion

        #region Remove Liquidity Tests

        public void TestRemoveLiquidity()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var lpBalance = Call<BigInteger>(poolHash, "balanceOf", alice);
            var removeAmount = lpBalance / 2;

            Vm.Prank(alice);
            var result = Call(poolHash, "removeLiquidity", removeAmount, 0, 0);

            Assert.Halted(result);
            Assert.EmittedEvent(result, "LiquidityRemoved");

            // LP balance should decrease
            var newLpBalance = Call<BigInteger>(poolHash, "balanceOf", alice);
            Assert.Equal(lpBalance - removeAmount, newLpBalance);
        }

        public void TestRemoveLiquidityMinimumAmounts()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var lpBalance = Call<BigInteger>(poolHash, "balanceOf", alice);
            var minAmountA = 900 * ONE_TOKEN; // Too high minimum
            var minAmountB = 1800 * ONE_TOKEN;

            Vm.ExpectRevert("Minimum amount not met");

            Vm.Prank(alice);
            Call(poolHash, "removeLiquidity", lpBalance, minAmountA, minAmountB);
        }

        public void TestRemoveAllLiquidity()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var lpBalance = Call<BigInteger>(poolHash, "balanceOf", alice);

            Vm.Prank(alice);
            var result = Call(poolHash, "removeLiquidity", lpBalance, 0, 0);

            Assert.Halted(result);

            // LP balance should be zero
            var newLpBalance = Call<BigInteger>(poolHash, "balanceOf", alice);
            Assert.Equal(BigInteger.Zero, newLpBalance);
        }

        #endregion

        #region Swap Tests

        public void TestSwapExactTokensForTokens()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var swapAmount = 100 * ONE_TOKEN;
            var minOutput = 150 * ONE_TOKEN; // Expect ~180 based on ratio minus fees

            Vm.Prank(bob);
            var result = Call(poolHash, "swapExactTokensForTokens",
                tokenAHash, swapAmount, minOutput);

            Assert.Halted(result);
            Assert.EmittedEvent(result, "Swap");
        }

        public void TestSwapSlippageProtection()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var swapAmount = 100 * ONE_TOKEN;
            var minOutput = 500 * ONE_TOKEN; // Unrealistic minimum

            Vm.ExpectRevert("Insufficient output amount");

            Vm.Prank(bob);
            Call(poolHash, "swapExactTokensForTokens",
                tokenAHash, swapAmount, minOutput);
        }

        public void TestSwapZeroAmount()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            Vm.ExpectRevert("Amount must be positive");

            Vm.Prank(bob);
            Call(poolHash, "swapExactTokensForTokens",
                tokenAHash, BigInteger.Zero, 0);
        }

        public void TestSwapPriceImpact()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            // Large swap should have significant price impact
            var largeSwap = 500 * ONE_TOKEN; // 50% of pool

            Vm.Prank(bob);
            var result = Call(poolHash, "swapExactTokensForTokens",
                tokenAHash, largeSwap, 0);

            Assert.Halted(result);

            // Price should have moved significantly
            var newPrice = Call<BigInteger>(poolHash, "getPrice", tokenAHash);
            // Verify price impact (implementation specific)
        }

        #endregion

        #region Fuzz Tests

        public void TestFuzz_SwapInvariant(uint swapAmount)
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var amount = Vm.Bound(swapAmount, 1u, 100u) * ONE_TOKEN;

            // Get reserves before
            var (reserveABefore, reserveBBefore) = GetReserves();
            var kBefore = reserveABefore * reserveBBefore;

            // Perform swap
            Vm.Prank(bob);
            Call(poolHash, "swapExactTokensForTokens", tokenAHash, amount, 0);

            // Get reserves after
            var (reserveAAfter, reserveBAfter) = GetReserves();
            var kAfter = reserveAAfter * reserveBAfter;

            // K should never decrease (constant product invariant)
            Assert.GreaterOrEqual(kAfter, kBefore);
        }

        public void TestFuzz_AddRemoveLiquidity(uint addAmount, uint removePercent)
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            var amount = Vm.Bound(addAmount, 10u, 500u) * ONE_TOKEN;
            var percent = Vm.Bound(removePercent, 1u, 100u);

            // Add liquidity
            Vm.Prank(bob);
            Call(poolHash, "addLiquidity", amount, amount * 2, 0, 0);

            var lpBalance = Call<BigInteger>(poolHash, "balanceOf", bob);
            var removeAmount = lpBalance * percent / 100;

            Vm.Assume(removeAmount > 0);

            // Remove liquidity
            Vm.Prank(bob);
            var result = Call(poolHash, "removeLiquidity", removeAmount, 0, 0);

            Assert.Halted(result);

            // LP balance should decrease correctly
            var newBalance = Call<BigInteger>(poolHash, "balanceOf", bob);
            Assert.Equal(lpBalance - removeAmount, newBalance);
        }

        #endregion

        #region Edge Cases

        public void TestEmptyPoolSwap()
        {
            // Try to swap on empty pool
            Vm.ExpectRevert("Insufficient liquidity");

            Vm.Prank(bob);
            Call(poolHash, "swapExactTokensForTokens",
                tokenAHash, 100 * ONE_TOKEN, 0);
        }

        public void TestRemoveLiquidityFromEmptyPosition()
        {
            SetupInitialLiquidity(alice, 1000 * ONE_TOKEN, 2000 * ONE_TOKEN);

            // Bob has no LP tokens
            Vm.ExpectRevert("Insufficient LP balance");

            Vm.Prank(bob);
            Call(poolHash, "removeLiquidity", 100 * ONE_TOKEN, 0, 0);
        }

        #endregion

        #region Helper Methods

        private void SetupInitialLiquidity(string provider, long amountA, long amountB)
        {
            Vm.Prank(provider);
            Call(tokenAHash, "transfer", provider, poolHash, amountA, null);

            Vm.Prank(provider);
            Call(poolHash, "addLiquidity", amountA, amountB, 0, 0);
        }

        private (BigInteger reserveA, BigInteger reserveB) GetReserves()
        {
            var reserveA = Call<BigInteger>(poolHash, "getReserveA");
            var reserveB = Call<BigInteger>(poolHash, "getReserveB");
            return (reserveA, reserveB);
        }

        #endregion
    }
}
