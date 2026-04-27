// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ContractManagementRiscV.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.Json;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.UnitTests.Extensions;
using System;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_ContractManagementRiscV
    {
        [TestMethod]
        public void TestContract_Create_RiscvManifestMarkerSetsContractType()
        {
            WithNeoVmProvider(snapshotCache =>
            {
                var nef = CreatePolkaVmNef(new byte[] { 0x50, 0x56, 0x4D, 0x00, 0x01, 0x00, 0x00, 0x00 });
                var manifest = CreateRiscVManifest();

                var contract = snapshotCache.DeployContract(UInt160.Zero, nef.ToArray(), manifest.ToJson().ToByteArray(false));

                Assert.AreEqual(ContractType.RiscV, contract.Type);
            });
        }

        [TestMethod]
        public void TestContract_Update_RiscvRejectsShortPolkaVmPayload()
        {
            WithNeoVmProvider(snapshotCache =>
            {
                var manifest = CreateRiscVManifest();
                var deployedNef = CreatePolkaVmNef(new byte[] { 0x50, 0x56, 0x4D, 0x00, 0x01, 0x00, 0x00, 0x00 });
                var state = snapshotCache.DeployContract(UInt160.Zero, deployedNef.ToArray(), manifest.ToJson().ToByteArray(false));
                var shortNef = CreatePolkaVmNef(new byte[] { 0x50, 0x56, 0x4D, 0x00, 0x01 });

                Assert.ThrowsExactly<FormatException>(() =>
                    snapshotCache.UpdateContract(state.Hash, shortNef.ToArray(), manifest.ToJson().ToByteArray(false)));
            });
        }

        private static ContractManifest CreateRiscVManifest()
        {
            var manifest = TestUtils.CreateDefaultManifest();
            manifest.Extra = new JObject
            {
                ["vm"] = ContractVmTypeResolver.RiscvPolkaVmMarker
            };
            return manifest;
        }

        private static NefFile CreatePolkaVmNef(byte[] script)
        {
            var nef = new NefFile
            {
                Script = script,
                Source = string.Empty,
                Compiler = string.Empty,
                Tokens = []
            };
            nef.CheckSum = NefFile.ComputeChecksum(nef);
            return nef;
        }

        private static void WithNeoVmProvider(Action<StoreCache> action)
        {
            var previousProvider = ApplicationEngine.Provider;
            ApplicationEngine.Provider = new NeoVMHostApplicationEngineProvider();
            try
            {
                using var system = new TestBlockchain.TestNeoSystem(TestProtocolSettings.Default);
                action(system.GetSnapshotCache());
            }
            finally
            {
                ApplicationEngine.Provider = previousProvider;
                RiscvAdapterTestSupport.ResetProviderForTesting();
            }
        }
    }
}
