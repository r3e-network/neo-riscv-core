// Copyright (C) 2015-2026 The Neo Project.
//
// AssetDescriptor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Numerics;

namespace Neo.Wallets
{
    /// <summary>
    /// Represents the descriptor of an asset.
    /// </summary>
    public class AssetDescriptor
    {
        /// <summary>
        /// The id of the asset.
        /// </summary>
        public UInt160 AssetId { get; }

        /// <summary>
        /// The name of the asset.
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// The symbol of the asset.
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// The number of decimal places of the token.
        /// </summary>
        public byte Decimals { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssetDescriptor"/> class.
        /// </summary>
        /// <param name="snapshot">The snapshot used to read data.</param>
        /// <param name="settings">The <see cref="ProtocolSettings"/> used by the <see cref="ApplicationEngine"/>.</param>
        /// <param name="assetId">The id of the asset.</param>
        public AssetDescriptor(DataCache snapshot, ProtocolSettings settings, UInt160 assetId)
        {
            var contract = NativeContract.ContractManagement.GetContract(snapshot, assetId);
            if (contract is null) throw new ArgumentException($"No asset contract found for assetId {assetId}. Please ensure the assetId is correct and the asset is deployed on the blockchain.", nameof(assetId));

            AssetId = assetId;
            AssetName = contract.Manifest.Name;
            Decimals = (byte)CallReadOnlyInteger(snapshot, settings, assetId, "decimals");
            Symbol = CallReadOnlyString(snapshot, settings, assetId, "symbol");
        }

        public override string ToString()
        {
            return AssetName;
        }

        private static BigInteger CallReadOnlyInteger(DataCache snapshot, ProtocolSettings settings, UInt160 assetId, string method)
        {
            using var engine = CreateReadOnlyEngine(snapshot, settings, assetId, method);
            var result = engine.ResultStack.Pop();
            return result.GetInteger();
        }

        private static string CallReadOnlyString(DataCache snapshot, ProtocolSettings settings, UInt160 assetId, string method)
        {
            using var engine = CreateReadOnlyEngine(snapshot, settings, assetId, method);
            var result = engine.ResultStack.Pop();
            return result.GetString()!;
        }

        private static ApplicationEngine CreateReadOnlyEngine(DataCache snapshot, ProtocolSettings settings, UInt160 assetId, string method)
        {
            using ScriptBuilder sb = new();
            sb.EmitDynamicCall(assetId, method, CallFlags.ReadOnly);

            var engine = ApplicationEngine.Run(sb.ToArray(), snapshot, settings: settings, gas: 0_30000000L);
            if (engine.State != VMState.HALT)
                throw new ArgumentException($"Failed to execute '{method}' for asset {assetId}. The contract execution did not complete successfully (VM state: {engine.State}).", nameof(assetId));
            return engine;
        }
    }
}
