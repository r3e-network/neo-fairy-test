// Deploy.cs - Deployment script for the DEX project
// Demonstrates scripted deployments similar to Foundry's forge script

using Neo.Fairy.Deployment;
using System.Numerics;

namespace NeoDex.Scripts
{
    /// <summary>
    /// Main deployment script for the DEX.
    /// Deploys all contracts in the correct order and initializes them.
    /// </summary>
    public class Deploy : FairyScript
    {
        private const long ONE_TOKEN = 100_000_000;

        public override async Task RunAsync()
        {
            Log("=== NEO DEX Deployment Script ===");
            Log($"Network: {Config.Network}");
            Log($"Deployer: {Deployer}");
            Log("");

            // Step 1: Deploy Token A
            Log("Step 1: Deploying Token A...");
            var tokenA = await DeployAsync("token");
            Log($"  Token A deployed at: {tokenA.ContractHash}");
            Log($"  Gas used: {tokenA.GasConsumed / ONE_TOKEN:F2} GAS");

            // Step 2: Deploy Token B (in real scenario, this would be a different contract)
            Log("Step 2: Deploying Token B...");
            // var tokenB = await DeployAsync("tokenB");
            // For demo, we'll use the same token contract
            var tokenB = tokenA;
            Log($"  Token B: {tokenB.ContractHash}");

            // Step 3: Deploy Liquidity Pool
            Log("Step 3: Deploying Liquidity Pool...");
            var pool = await DeployAsync("pool");
            Log($"  Pool deployed at: {pool.ContractHash}");
            Log($"  Gas used: {pool.GasConsumed / ONE_TOKEN:F2} GAS");

            // Step 4: Deploy Router
            Log("Step 4: Deploying Router...");
            var router = await DeployAsync("router");
            Log($"  Router deployed at: {router.ContractHash}");
            Log($"  Gas used: {router.GasConsumed / ONE_TOKEN:F2} GAS");

            // Step 5: Initialize contracts
            Log("");
            Log("Step 5: Initializing contracts...");

            // Set pool in router
            await CallAsync(router.ContractHash, "setPool", pool.ContractHash);
            Log("  Router configured with pool address");

            // Set tokens in pool
            await CallAsync(pool.ContractHash, "initialize",
                tokenA.ContractHash, tokenB.ContractHash);
            Log("  Pool initialized with token addresses");

            // Step 6: Setup initial liquidity (testnet/devnet only)
            if (Config.Network != "mainnet")
            {
                Log("");
                Log("Step 6: Setting up initial liquidity (testnet only)...");

                // Mint tokens to deployer
                var mintAmount = 10000 * ONE_TOKEN;
                await CallAsync(tokenA.ContractHash, "mint", Deployer, mintAmount);
                Log($"  Minted {mintAmount / ONE_TOKEN} Token A to deployer");

                // Add initial liquidity
                var liquidityA = 1000 * ONE_TOKEN;
                var liquidityB = 2000 * ONE_TOKEN;

                await CallAsync(tokenA.ContractHash, "transfer",
                    Deployer, pool.ContractHash, liquidityA, null);
                await CallAsync(pool.ContractHash, "addLiquidity",
                    liquidityA, liquidityB, 0, 0);
                Log($"  Added initial liquidity: {liquidityA / ONE_TOKEN} A / {liquidityB / ONE_TOKEN} B");
            }

            // Summary
            Log("");
            Log("=== Deployment Complete ===");
            Log("");
            Log("Contract Addresses:");
            Log($"  Token A: {tokenA.ContractHash}");
            Log($"  Token B: {tokenB.ContractHash}");
            Log($"  Pool:    {pool.ContractHash}");
            Log($"  Router:  {router.ContractHash}");
            Log("");

            var totalGas = tokenA.GasConsumed + pool.GasConsumed + router.GasConsumed;
            Log($"Total Gas Used: {totalGas / ONE_TOKEN:F2} GAS");
        }
    }

    /// <summary>
    /// Script to add more liquidity to an existing pool.
    /// </summary>
    public class AddLiquidity : FairyScript
    {
        private const long ONE_TOKEN = 100_000_000;

        public override async Task RunAsync()
        {
            var poolAddress = GetEnvOrFail("POOL_ADDRESS");
            var tokenAAddress = GetEnvOrFail("TOKEN_A_ADDRESS");
            var amountA = long.Parse(GetEnvOrDefault("AMOUNT_A", "1000")) * ONE_TOKEN;
            var amountB = long.Parse(GetEnvOrDefault("AMOUNT_B", "2000")) * ONE_TOKEN;

            Log($"Adding liquidity to pool: {poolAddress}");
            Log($"  Amount A: {amountA / ONE_TOKEN}");
            Log($"  Amount B: {amountB / ONE_TOKEN}");

            // Transfer tokens to pool
            await CallAsync(tokenAAddress, "transfer",
                Deployer, poolAddress, amountA, null);

            // Add liquidity
            var result = await CallAsync(poolAddress, "addLiquidity",
                amountA, amountB, 0, 0);

            Log($"Liquidity added successfully!");
            Log($"  Gas used: {result.GasConsumed / ONE_TOKEN:F2} GAS");
        }
    }

    /// <summary>
    /// Script to perform a token swap.
    /// </summary>
    public class Swap : FairyScript
    {
        private const long ONE_TOKEN = 100_000_000;

        public override async Task RunAsync()
        {
            var routerAddress = GetEnvOrFail("ROUTER_ADDRESS");
            var tokenInAddress = GetEnvOrFail("TOKEN_IN_ADDRESS");
            var amountIn = long.Parse(GetEnvOrDefault("AMOUNT_IN", "100")) * ONE_TOKEN;
            var minAmountOut = long.Parse(GetEnvOrDefault("MIN_AMOUNT_OUT", "0")) * ONE_TOKEN;

            Log($"Swapping tokens via router: {routerAddress}");
            Log($"  Token In: {tokenInAddress}");
            Log($"  Amount In: {amountIn / ONE_TOKEN}");
            Log($"  Min Amount Out: {minAmountOut / ONE_TOKEN}");

            var result = await CallAsync(routerAddress, "swapExactTokensForTokens",
                tokenInAddress, amountIn, minAmountOut);

            if (result.IsSuccess)
            {
                Log($"Swap successful!");
                Log($"  Gas used: {result.GasConsumed / ONE_TOKEN:F2} GAS");
            }
            else
            {
                Log($"Swap failed: {result.Exception}");
            }
        }
    }
}
