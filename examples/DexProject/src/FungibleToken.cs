// SPDX-License-Identifier: MIT
// FungibleToken.cs - NEP-17 compliant fungible token for Neo N3

using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;
using System.ComponentModel;
using System.Numerics;

namespace NeoDex
{
    [DisplayName("FungibleToken")]
    [ManifestExtra("Author", "Neo Fairy Team")]
    [ManifestExtra("Description", "NEP-17 Fungible Token")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class FungibleToken : SmartContract
    {
        #region Storage Keys

        private const byte Prefix_TotalSupply = 0x00;
        private const byte Prefix_Balance = 0x01;
        private const byte Prefix_Owner = 0x02;
        private const byte Prefix_Minter = 0x03;

        #endregion

        #region Token Metadata

        [Safe]
        public static string Symbol() => "DEX";

        [Safe]
        public static byte Decimals() => 8;

        [Safe]
        public static BigInteger TotalSupply()
        {
            StorageContext context = Storage.CurrentContext;
            return (BigInteger)Storage.Get(context, new byte[] { Prefix_TotalSupply });
        }

        #endregion

        #region NEP-17 Methods

        [Safe]
        public static BigInteger BalanceOf(UInt160 account)
        {
            if (account is null || !account.IsValid)
                throw new Exception("Invalid account");

            StorageMap balanceMap = new(Storage.CurrentContext, Prefix_Balance);
            return (BigInteger)balanceMap.Get(account);
        }

        public static bool Transfer(UInt160 from, UInt160 to, BigInteger amount, object data)
        {
            if (from is null || !from.IsValid)
                throw new Exception("Invalid from address");
            if (to is null || !to.IsValid)
                throw new Exception("Invalid to address");
            if (amount < 0)
                throw new Exception("Amount must be non-negative");

            if (!Runtime.CheckWitness(from) && !from.Equals(Runtime.CallingScriptHash))
                throw new Exception("Not authorized");

            if (amount == 0)
                return true;

            StorageMap balanceMap = new(Storage.CurrentContext, Prefix_Balance);

            BigInteger fromBalance = (BigInteger)balanceMap.Get(from);
            if (fromBalance < amount)
                throw new Exception("Insufficient balance");

            if (from.Equals(to))
                return true;

            if (fromBalance == amount)
                balanceMap.Delete(from);
            else
                balanceMap.Put(from, fromBalance - amount);

            BigInteger toBalance = (BigInteger)balanceMap.Get(to);
            balanceMap.Put(to, toBalance + amount);

            OnTransfer(from, to, amount);

            if (ContractManagement.GetContract(to) != null)
                Contract.Call(to, "onNEP17Payment", CallFlags.All, from, amount, data);

            return true;
        }

        #endregion

        #region Minting

        public static bool Mint(UInt160 to, BigInteger amount)
        {
            if (to is null || !to.IsValid)
                throw new Exception("Invalid to address");
            if (amount <= 0)
                throw new Exception("Amount must be positive");

            // Check minter permission
            UInt160 minter = GetMinter();
            if (minter != null && !Runtime.CheckWitness(minter))
            {
                UInt160 owner = GetOwner();
                if (!Runtime.CheckWitness(owner))
                    throw new Exception("Not authorized to mint");
            }

            StorageContext context = Storage.CurrentContext;
            StorageMap balanceMap = new(context, Prefix_Balance);

            BigInteger totalSupply = (BigInteger)Storage.Get(context, new byte[] { Prefix_TotalSupply });
            BigInteger toBalance = (BigInteger)balanceMap.Get(to);

            Storage.Put(context, new byte[] { Prefix_TotalSupply }, totalSupply + amount);
            balanceMap.Put(to, toBalance + amount);

            OnTransfer(null, to, amount);
            return true;
        }

        public static bool Burn(UInt160 from, BigInteger amount)
        {
            if (from is null || !from.IsValid)
                throw new Exception("Invalid from address");
            if (amount <= 0)
                throw new Exception("Amount must be positive");

            if (!Runtime.CheckWitness(from))
                throw new Exception("Not authorized");

            StorageContext context = Storage.CurrentContext;
            StorageMap balanceMap = new(context, Prefix_Balance);

            BigInteger fromBalance = (BigInteger)balanceMap.Get(from);
            if (fromBalance < amount)
                throw new Exception("Insufficient balance");

            BigInteger totalSupply = (BigInteger)Storage.Get(context, new byte[] { Prefix_TotalSupply });

            Storage.Put(context, new byte[] { Prefix_TotalSupply }, totalSupply - amount);

            if (fromBalance == amount)
                balanceMap.Delete(from);
            else
                balanceMap.Put(from, fromBalance - amount);

            OnTransfer(from, null, amount);
            return true;
        }

        #endregion

        #region Admin

        [Safe]
        public static UInt160 GetOwner()
        {
            StorageContext context = Storage.CurrentContext;
            byte[] owner = Storage.Get(context, new byte[] { Prefix_Owner });
            return owner?.Length == 20 ? (UInt160)owner : null;
        }

        [Safe]
        public static UInt160 GetMinter()
        {
            StorageContext context = Storage.CurrentContext;
            byte[] minter = Storage.Get(context, new byte[] { Prefix_Minter });
            return minter?.Length == 20 ? (UInt160)minter : null;
        }

        public static bool SetMinter(UInt160 minter)
        {
            UInt160 owner = GetOwner();
            if (!Runtime.CheckWitness(owner))
                throw new Exception("Not authorized");

            StorageContext context = Storage.CurrentContext;
            if (minter is null || !minter.IsValid)
                Storage.Delete(context, new byte[] { Prefix_Minter });
            else
                Storage.Put(context, new byte[] { Prefix_Minter }, minter);

            return true;
        }

        public static void _deploy(object data, bool update)
        {
            if (update) return;

            StorageContext context = Storage.CurrentContext;
            Transaction tx = (Transaction)Runtime.ScriptContainer;
            Storage.Put(context, new byte[] { Prefix_Owner }, tx.Sender);
            Storage.Put(context, new byte[] { Prefix_TotalSupply }, 0);
        }

        #endregion

        #region Events

        [DisplayName("Transfer")]
        public static event Action<UInt160, UInt160, BigInteger> OnTransfer;

        #endregion
    }
}
