// FungibleToken.Test.cs - Comprehensive tests for the FungibleToken contract
// Demonstrates Fairy testing patterns inspired by Foundry

using Neo.Fairy.Testing;
using System.Numerics;

namespace NeoDex.Tests
{
    /// <summary>
    /// Test suite for FungibleToken contract.
    /// Shows various testing patterns including:
    /// - Basic assertions
    /// - Event verification
    /// - Revert testing
    /// - Fuzz testing
    /// - Cheatcodes usage
    /// </summary>
    public class FungibleTokenTest : FairyTest
    {
        private string tokenHash = null!;
        private string owner = null!;
        private string alice = null!;
        private string bob = null!;
        private string charlie = null!;

        private const long ONE_TOKEN = 100_000_000; // 8 decimals

        /// <summary>
        /// Setup runs before each test.
        /// Deploys the contract and creates test accounts.
        /// </summary>
        public override void SetUp()
        {
            // Deploy the token contract
            tokenHash = Deploy("token");

            // Create test accounts with GAS for fees
            owner = MakeAccount(1000 * ONE_TOKEN);
            alice = MakeAccount(100 * ONE_TOKEN);
            bob = MakeAccount(100 * ONE_TOKEN);
            charlie = MakeAccount(100 * ONE_TOKEN);

            // Owner mints initial supply to alice
            Vm.Prank(owner);
            Call(tokenHash, "mint", alice, 1000 * ONE_TOKEN);
        }

        #region Basic Token Tests

        public void TestSymbol()
        {
            var symbol = Call<string>(tokenHash, "symbol");
            Assert.Equal("DEX", symbol);
        }

        public void TestDecimals()
        {
            var decimals = Call<int>(tokenHash, "decimals");
            Assert.Equal(8, decimals);
        }

        public void TestInitialBalance()
        {
            var balance = Call<BigInteger>(tokenHash, "balanceOf", alice);
            Assert.Equal(1000 * ONE_TOKEN, balance);
        }

        public void TestTotalSupply()
        {
            var totalSupply = Call<BigInteger>(tokenHash, "totalSupply");
            Assert.Equal(1000 * ONE_TOKEN, totalSupply);
        }

        #endregion

        #region Transfer Tests

        public void TestTransfer()
        {
            // Arrange
            var amount = 100 * ONE_TOKEN;

            // Act
            Vm.Prank(alice);
            var result = Call(tokenHash, "transfer", alice, bob, amount, null);

            // Assert
            Assert.Halted(result);
            Assert.Equal(900 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", alice));
            Assert.Equal(100 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", bob));

            // Verify Transfer event
            Assert.EmittedEvent(result, "Transfer");
        }

        public void TestTransferToSelf()
        {
            // Transfer to self should succeed without changing balance
            var balanceBefore = Call<BigInteger>(tokenHash, "balanceOf", alice);

            Vm.Prank(alice);
            var result = Call(tokenHash, "transfer", alice, alice, 100 * ONE_TOKEN, null);

            Assert.Halted(result);
            Assert.Equal(balanceBefore, Call<BigInteger>(tokenHash, "balanceOf", alice));
        }

        public void TestTransferZeroAmount()
        {
            // Zero amount transfer should succeed
            Vm.Prank(alice);
            var result = Call(tokenHash, "transfer", alice, bob, BigInteger.Zero, null);

            Assert.Halted(result);
        }

        public void TestTransferInsufficientBalance()
        {
            // Expect revert when transferring more than balance
            Vm.ExpectRevert("Insufficient balance");

            Vm.Prank(alice);
            Call(tokenHash, "transfer", alice, bob, 2000 * ONE_TOKEN, null);
        }

        public void TestTransferWithoutAuthorization()
        {
            // Bob tries to transfer Alice's tokens without permission
            Vm.ExpectRevert("Not authorized");

            Vm.Prank(bob);
            Call(tokenHash, "transfer", alice, bob, 100 * ONE_TOKEN, null);
        }

        public void TestTransferNegativeAmount()
        {
            Vm.ExpectRevert("Amount must be non-negative");

            Vm.Prank(alice);
            Call(tokenHash, "transfer", alice, bob, new BigInteger(-100), null);
        }

        #endregion

        #region Mint Tests

        public void TestMint()
        {
            var supplyBefore = Call<BigInteger>(tokenHash, "totalSupply");

            Vm.Prank(owner);
            var result = Call(tokenHash, "mint", bob, 500 * ONE_TOKEN);

            Assert.Halted(result);
            Assert.Equal(500 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", bob));
            Assert.Equal(supplyBefore + 500 * ONE_TOKEN, Call<BigInteger>(tokenHash, "totalSupply"));

            // Verify mint event (Transfer from null)
            Assert.EmittedEvent(result, "Transfer");
        }

        public void TestMintUnauthorized()
        {
            // Non-owner/non-minter cannot mint
            Vm.ExpectRevert("Not authorized to mint");

            Vm.Prank(alice);
            Call(tokenHash, "mint", alice, 100 * ONE_TOKEN);
        }

        public void TestMintZeroAmount()
        {
            Vm.ExpectRevert("Amount must be positive");

            Vm.Prank(owner);
            Call(tokenHash, "mint", bob, BigInteger.Zero);
        }

        #endregion

        #region Burn Tests

        public void TestBurn()
        {
            var supplyBefore = Call<BigInteger>(tokenHash, "totalSupply");
            var balanceBefore = Call<BigInteger>(tokenHash, "balanceOf", alice);

            Vm.Prank(alice);
            var result = Call(tokenHash, "burn", alice, 100 * ONE_TOKEN);

            Assert.Halted(result);
            Assert.Equal(balanceBefore - 100 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", alice));
            Assert.Equal(supplyBefore - 100 * ONE_TOKEN, Call<BigInteger>(tokenHash, "totalSupply"));
        }

        public void TestBurnInsufficientBalance()
        {
            Vm.ExpectRevert("Insufficient balance");

            Vm.Prank(alice);
            Call(tokenHash, "burn", alice, 2000 * ONE_TOKEN);
        }

        public void TestBurnUnauthorized()
        {
            Vm.ExpectRevert("Not authorized");

            Vm.Prank(bob);
            Call(tokenHash, "burn", alice, 100 * ONE_TOKEN);
        }

        #endregion

        #region Admin Tests

        public void TestSetMinter()
        {
            Vm.Prank(owner);
            var result = Call(tokenHash, "setMinter", alice);

            Assert.Halted(result);
            Assert.Equal(alice, Call<string>(tokenHash, "getMinter"));

            // Now alice can mint
            Vm.Prank(alice);
            result = Call(tokenHash, "mint", charlie, 100 * ONE_TOKEN);
            Assert.Halted(result);
        }

        public void TestSetMinterUnauthorized()
        {
            Vm.ExpectRevert("Not authorized");

            Vm.Prank(alice);
            Call(tokenHash, "setMinter", alice);
        }

        #endregion

        #region Fuzz Tests

        /// <summary>
        /// Fuzz test for transfers.
        /// Tests random amounts to ensure transfer invariants hold.
        /// </summary>
        public void TestFuzz_Transfer(uint amount)
        {
            // Bound the amount to valid range
            var boundedAmount = Vm.Bound(amount, 1u, 1000u) * ONE_TOKEN;

            // Skip if amount exceeds alice's balance
            var aliceBalance = Call<BigInteger>(tokenHash, "balanceOf", alice);
            Vm.Assume(boundedAmount <= aliceBalance);

            var bobBalanceBefore = Call<BigInteger>(tokenHash, "balanceOf", bob);
            var totalSupplyBefore = Call<BigInteger>(tokenHash, "totalSupply");

            // Execute transfer
            Vm.Prank(alice);
            Call(tokenHash, "transfer", alice, bob, boundedAmount, null);

            // Verify invariants
            var aliceBalanceAfter = Call<BigInteger>(tokenHash, "balanceOf", alice);
            var bobBalanceAfter = Call<BigInteger>(tokenHash, "balanceOf", bob);
            var totalSupplyAfter = Call<BigInteger>(tokenHash, "totalSupply");

            // Balance changes should be exact
            Assert.Equal(aliceBalance - boundedAmount, aliceBalanceAfter);
            Assert.Equal(bobBalanceBefore + boundedAmount, bobBalanceAfter);

            // Total supply should not change
            Assert.Equal(totalSupplyBefore, totalSupplyAfter);
        }

        /// <summary>
        /// Fuzz test for mint and burn.
        /// Ensures supply invariants are maintained.
        /// </summary>
        public void TestFuzz_MintBurn(uint mintAmount, uint burnAmount)
        {
            var mint = Vm.Bound(mintAmount, 1u, 1000u) * ONE_TOKEN;
            var burn = Vm.Bound(burnAmount, 1u, 500u) * ONE_TOKEN;

            var supplyBefore = Call<BigInteger>(tokenHash, "totalSupply");

            // Mint to bob
            Vm.Prank(owner);
            Call(tokenHash, "mint", bob, mint);

            // Burn from bob (only what was minted)
            Vm.Assume(burn <= mint);
            Vm.Prank(bob);
            Call(tokenHash, "burn", bob, burn);

            var supplyAfter = Call<BigInteger>(tokenHash, "totalSupply");

            // Supply should increase by (mint - burn)
            Assert.Equal(supplyBefore + mint - burn, supplyAfter);
        }

        #endregion

        #region Time-based Tests

        public void TestTimestampManipulation()
        {
            // Set a specific timestamp
            var futureTime = (ulong)DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeMilliseconds();
            Vm.Warp(futureTime);

            // Contract operations should use the warped time
            // (This would be useful for time-locked features)
            Vm.Prank(alice);
            var result = Call(tokenHash, "transfer", alice, bob, 10 * ONE_TOKEN, null);
            Assert.Halted(result);
        }

        #endregion

        #region Snapshot Tests

        public void TestSnapshotAndRevert()
        {
            // Take a snapshot
            var snapshotId = Vm.Snapshot();

            // Make some changes
            Vm.Prank(alice);
            Call(tokenHash, "transfer", alice, bob, 500 * ONE_TOKEN, null);

            Assert.Equal(500 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", alice));
            Assert.Equal(500 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", bob));

            // Revert to snapshot
            Vm.RevertTo(snapshotId);

            // Balances should be restored
            Assert.Equal(1000 * ONE_TOKEN, Call<BigInteger>(tokenHash, "balanceOf", alice));
            Assert.Equal(BigInteger.Zero, Call<BigInteger>(tokenHash, "balanceOf", bob));
        }

        #endregion

        #region Gas Tests

        public void TestTransferGasUsage()
        {
            Vm.Prank(alice);
            var result = Call(tokenHash, "transfer", alice, bob, 100 * ONE_TOKEN, null);

            // Ensure gas usage is reasonable (less than 1 GAS)
            Assert.GasLessThan(result, 1 * ONE_TOKEN);
        }

        #endregion
    }
}
