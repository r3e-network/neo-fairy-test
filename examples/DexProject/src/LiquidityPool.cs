// SPDX-License-Identifier: MIT
// LiquidityPool.cs - Minimal constant-product AMM example for Fairy workspace

using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.Numerics;

namespace NeoDex
{
    [DisplayName("LiquidityPool")]
    [ManifestExtra("Author", "Neo Fairy Team")]
    [ManifestExtra("Description", "Example constant-product liquidity pool")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class LiquidityPool : SmartContract
    {
        private static readonly byte[] PrefixTotalSupply = { 0x10 };
        private static readonly byte[] PrefixReserveA = { 0x11 };
        private static readonly byte[] PrefixReserveB = { 0x12 };
        private static readonly byte[] PrefixTokenA = { 0x13 };
        private static readonly byte[] PrefixTokenB = { 0x14 };
        private const byte PrefixLpBalance = 0x20;

        private static StorageMap LpBalances() => new(Storage.CurrentContext, PrefixLpBalance);

        // Events
        [DisplayName("LiquidityAdded")]
        public static event Action<UInt160, BigInteger, BigInteger, BigInteger> OnLiquidityAdded;

        [DisplayName("LiquidityRemoved")]
        public static event Action<UInt160, BigInteger, BigInteger, BigInteger> OnLiquidityRemoved;

        [DisplayName("Swap")]
        public static event Action<UInt160, BigInteger, BigInteger> OnSwap;

        [Safe]
        public static BigInteger GetReserveA() => (BigInteger)Storage.Get(Storage.CurrentContext, PrefixReserveA);

        [Safe]
        public static BigInteger GetReserveB() => (BigInteger)Storage.Get(Storage.CurrentContext, PrefixReserveB);

        [Safe]
        public static BigInteger TotalSupply() => (BigInteger)Storage.Get(Storage.CurrentContext, PrefixTotalSupply);

        [Safe]
        public static BigInteger BalanceOf(UInt160 account)
        {
            if (account is null || !account.IsValid) return 0;
            return (BigInteger)LpBalances().Get(account);
        }

        [Safe]
        public static UInt160 GetTokenA()
        {
            var data = Storage.Get(Storage.CurrentContext, PrefixTokenA);
            return data.Length == 20 ? (UInt160)data : null;
        }

        [Safe]
        public static UInt160 GetTokenB()
        {
            var data = Storage.Get(Storage.CurrentContext, PrefixTokenB);
            return data.Length == 20 ? (UInt160)data : null;
        }

        public static void SetTokens(UInt160 tokenA, UInt160 tokenB)
        {
            if (tokenA is null || tokenB is null) throw new Exception("Tokens required");
            Storage.Put(Storage.CurrentContext, PrefixTokenA, tokenA);
            Storage.Put(Storage.CurrentContext, PrefixTokenB, tokenB);
        }

        public static bool AddLiquidity(BigInteger amountA, BigInteger amountB, BigInteger minLpTokens, BigInteger _deadline)
        {
            if (amountA <= 0 || amountB <= 0) throw new Exception("Amounts must be positive");

            var reserveA = GetReserveA();
            var reserveB = GetReserveB();
            var totalSupply = TotalSupply();

            // Set tokens lazily on first add
            var tokenA = GetTokenA();
            var tokenB = GetTokenB();
            if (tokenA is null) Storage.Put(Storage.CurrentContext, PrefixTokenA, Runtime.CallingScriptHash);
            if (tokenB is null) Storage.Put(Storage.CurrentContext, PrefixTokenB, Runtime.CallingScriptHash);

            BigInteger minted;
            if (reserveA == 0 && reserveB == 0)
            {
                minted = Sqrt(amountA * amountB);
            }
            else
            {
                var expectedB = reserveB * amountA / reserveA;
                if (amountB < expectedB) throw new Exception("Slippage exceeded");

                var mintedA = amountA * totalSupply / reserveA;
                var mintedB = amountB * totalSupply / reserveB;
                minted = BigInteger.Min(mintedA, mintedB);
            }

            if (minted <= 0) throw new Exception("Minted LP would be zero");
            if (minted < minLpTokens) throw new Exception("Minimum LP not met");

            var sender = GetSender();
            var balances = LpBalances();
            var current = (BigInteger)balances.Get(sender);
            balances.Put(sender, current + minted);

            Storage.Put(Storage.CurrentContext, PrefixReserveA, reserveA + amountA);
            Storage.Put(Storage.CurrentContext, PrefixReserveB, reserveB + amountB);
            Storage.Put(Storage.CurrentContext, PrefixTotalSupply, totalSupply + minted);

            OnLiquidityAdded(sender, amountA, amountB, minted);
            return true;
        }

        public static bool RemoveLiquidity(BigInteger lpAmount, BigInteger minAmountA, BigInteger minAmountB)
        {
            if (lpAmount <= 0) throw new Exception("Amount must be positive");

            var totalSupply = TotalSupply();
            if (totalSupply == 0) throw new Exception("No liquidity");

            var reserveA = GetReserveA();
            var reserveB = GetReserveB();

            var sender = GetSender();
            var balances = LpBalances();
            var balance = (BigInteger)balances.Get(sender);
            if (balance < lpAmount) throw new Exception("Insufficient LP balance");

            var amountA = reserveA * lpAmount / totalSupply;
            var amountB = reserveB * lpAmount / totalSupply;

            if (amountA < minAmountA || amountB < minAmountB) throw new Exception("Minimum amount not met");

            var newBalance = balance - lpAmount;
            if (newBalance == 0) balances.Delete(sender);
            else balances.Put(sender, newBalance);

            Storage.Put(Storage.CurrentContext, PrefixReserveA, reserveA - amountA);
            Storage.Put(Storage.CurrentContext, PrefixReserveB, reserveB - amountB);
            Storage.Put(Storage.CurrentContext, PrefixTotalSupply, totalSupply - lpAmount);

            OnLiquidityRemoved(sender, amountA, amountB, lpAmount);
            return true;
        }

        public static bool SwapExactTokensForTokens(UInt160 tokenIn, BigInteger amountIn, BigInteger minOut)
        {
            if (amountIn <= 0) throw new Exception("Amount must be positive");

            var tokenA = GetTokenA();
            var tokenB = GetTokenB();

            var reserveA = GetReserveA();
            var reserveB = GetReserveB();
            if (reserveA == 0 || reserveB == 0) throw new Exception("Insufficient liquidity");

            bool inputIsA = tokenA is null || tokenIn == tokenA;

            BigInteger reserveIn = inputIsA ? reserveA : reserveB;
            BigInteger reserveOut = inputIsA ? reserveB : reserveA;

            var amountOut = (amountIn * reserveOut) / (reserveIn + amountIn);
            if (amountOut <= 0) throw new Exception("Insufficient output amount");
            if (amountOut < minOut) throw new Exception("Insufficient output amount");

            reserveIn += amountIn;
            reserveOut -= amountOut;

            if (inputIsA)
            {
                Storage.Put(Storage.CurrentContext, PrefixReserveA, reserveIn);
                Storage.Put(Storage.CurrentContext, PrefixReserveB, reserveOut);
            }
            else
            {
                Storage.Put(Storage.CurrentContext, PrefixReserveA, reserveOut);
                Storage.Put(Storage.CurrentContext, PrefixReserveB, reserveIn);
            }

            OnSwap(tokenIn, amountIn, amountOut);
            return true;
        }

        [Safe]
        public static BigInteger GetPrice(UInt160 tokenIn)
        {
            var reserveA = GetReserveA();
            var reserveB = GetReserveB();
            if (reserveA == 0 || reserveB == 0) return 0;

            var tokenA = GetTokenA();
            if (tokenA is null || tokenIn == tokenA)
            {
                return reserveB * 100_000_000 / reserveA;
            }
            return reserveA * 100_000_000 / reserveB;
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;
            Storage.Put(Storage.CurrentContext, PrefixTotalSupply, 0);
            Storage.Put(Storage.CurrentContext, PrefixReserveA, 0);
            Storage.Put(Storage.CurrentContext, PrefixReserveB, 0);
        }

        private static UInt160 GetSender()
        {
            // In many examples we just return the transaction sender; Fairy's Vm.Prank sets witnesses accordingly.
            Transaction tx = (Transaction)Runtime.ScriptContainer;
            return tx.Sender;
        }

        private static BigInteger Sqrt(BigInteger x)
        {
            if (x <= 0) return 0;
            BigInteger z = (x + 1) / 2;
            BigInteger y = x;
            while (z < y)
            {
                y = z;
                z = (x / z + z) / 2;
            }
            return y;
        }
    }
}
