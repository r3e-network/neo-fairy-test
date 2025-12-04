// SPDX-License-Identifier: MIT
// Router.cs - Minimal router stub for Fairy workspace example

using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;
using System;
using System.Numerics;

namespace NeoDex
{
    [DisplayName("Router")]
    [ManifestExtra("Author", "Neo Fairy Team")]
    [ManifestExtra("Description", "Example router wrapper for liquidity pool")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class Router : SmartContract
    {
        [DisplayName("RouterEvent")]
        public static event Action<string> OnRouterEvent;

        public static bool AddLiquidity(UInt160 pool, BigInteger amountA, BigInteger amountB, BigInteger minA, BigInteger minB)
        {
            // In a real router, this would transfer tokens and call pool.addLiquidity.
            // Here we emit an event and return true for the example.
            OnRouterEvent("AddLiquidity");
            return true;
        }

        public static bool SwapExactTokensForTokens(UInt160 pool, UInt160 tokenIn, BigInteger amountIn, BigInteger minOut)
        {
            // Real implementation would proxy to the pool. This is a stub for the sample workspace.
            OnRouterEvent("SwapExactTokensForTokens");
            return true;
        }

        [Safe]
        public static string Version() => "0.0.1";

        public static void _deploy(object data, bool update)
        {
            // No-op
        }
    }
}
