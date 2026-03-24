// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ApplicationEngineProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Reflection;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public class UT_ApplicationEngineProvider
    {
        private DataCache _snapshotCache;
        private string _previousLibraryPath;

        [TestInitialize]
        public void TestSetup()
        {
            _previousLibraryPath = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
            RiscvAdapterTestSupport.ResetProviderForTesting();

            if (!string.IsNullOrWhiteSpace(_previousLibraryPath))
            {
                ApplicationEngine.Provider = RiscvAdapterTestSupport.ResolveProvider();
            }
            _snapshotCache = TestBlockchain.GetSystem().GetTestSnapshotCache();
            ApplicationEngine.Provider = null;
            RiscvAdapterTestSupport.ResetProviderForTesting();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ApplicationEngine.Provider = null;
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", _previousLibraryPath);
            RiscvAdapterTestSupport.ResetProviderForTesting();
        }

        [TestMethod]
        public void TestSetAppEngineProvider()
        {
            ApplicationEngine.Provider = new TestProvider();
            var snapshot = _snapshotCache.CloneCache();

            using var appEngine = ApplicationEngine.Create(TriggerType.Application,
                null, snapshot, gas: 0, settings: TestProtocolSettings.Default);
            Assert.IsTrue(appEngine is TestEngine);
        }

        [TestMethod]
        public void TestDefaultAppEngineProvider()
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", null);
            RiscvAdapterTestSupport.ResetProviderForTesting();
            var snapshot = _snapshotCache.CloneCache();
            Assert.ThrowsExactly<InvalidOperationException>(() => _ = ApplicationEngine.Create(TriggerType.Application,
                null, snapshot, gas: 0, settings: TestProtocolSettings.Default));
        }

        [TestMethod]
        public void TestConfiguredRiscvProviderBecomesDefault()
        {
            if (!RiscvAdapterTestSupport.CanUseAdapter())
            {
                Assert.Inconclusive(RiscvAdapterTestSupport.AdapterUnavailableReason());
            }

            ApplicationEngine.Provider = RiscvAdapterTestSupport.ResolveProvider();
            var snapshot = _snapshotCache.CloneCache();
            using var appEngine = ApplicationEngine.Create(TriggerType.Application,
                null, snapshot, gas: 0, settings: TestProtocolSettings.Default);
            Assert.IsTrue(RiscvAdapterTestSupport.IsRiscvEngine(appEngine));
        }

        [TestMethod]
        public void TestConfiguredRiscvProviderBootstrapsNativeState()
        {
            if (!RiscvAdapterTestSupport.CanUseAdapter())
            {
                Assert.Inconclusive(RiscvAdapterTestSupport.AdapterUnavailableReason());
            }

            ApplicationEngine.Provider = RiscvAdapterTestSupport.ResolveProvider();
            var system = TestBlockchain.GetSystem();
            var snapshot = system.GetSnapshotCache();

            Assert.IsTrue(NativeContract.Ledger.Initialized(snapshot));
            Assert.IsNotNull(NativeContract.ContractManagement.GetContract(snapshot, NativeContract.NEO.Hash));
            Assert.IsNotNull(NativeContract.ContractManagement.GetContract(snapshot, NativeContract.GAS.Hash));
            Assert.AreEqual(system.GenesisBlock.Hash, NativeContract.Ledger.CurrentHash(snapshot));
        }

        [TestMethod]
        public void TestExplicitRiscvProviderBootstrapsNativeState()
        {
            var libraryPath = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
            if (string.IsNullOrWhiteSpace(libraryPath) || !RiscvAdapterTestSupport.CanUseAdapter())
            {
                Assert.Inconclusive(RiscvAdapterTestSupport.AdapterUnavailableReason());
            }

            ApplicationEngine.Provider = RiscvAdapterTestSupport.CreateProvider(libraryPath);
            var system = new TestBlockchain.TestNeoSystem(TestProtocolSettings.Default);
            var snapshot = system.GetSnapshotCache();

            Assert.IsTrue(NativeContract.Ledger.Initialized(snapshot));
            Assert.IsNotNull(NativeContract.ContractManagement.GetContract(snapshot, NativeContract.NEO.Hash));
            Assert.IsNotNull(NativeContract.ContractManagement.GetContract(snapshot, NativeContract.GAS.Hash));
            Assert.AreEqual(system.GenesisBlock.Hash, NativeContract.Ledger.CurrentHash(snapshot));
        }

        [TestMethod]
        public void TestInitNonce()
        {
            var block = new Block
            {
                Header = new()
                {
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Nonce = 0x0102030405060708,
                    NextConsensus = UInt160.Zero,
                    Witness = null!
                },
                Transactions = []
            };
            using var app = new TestEngine(TriggerType.Application,
                null, null, block, TestProtocolSettings.Default, 0, null, null);

            var nonceData = typeof(ApplicationEngine)
                .GetField("nonceData", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(app) as byte[];
            Assert.IsNotNull(nonceData);
            Assert.AreEqual("08070605040302010000000000000000", nonceData.ToHexString());
        }

        class TestProvider : IApplicationEngineProvider
        {
            public ApplicationEngine Create(TriggerType trigger, IVerifiable container, DataCache snapshot,
                Block persistingBlock, ProtocolSettings settings, long gas, IDiagnostic diagnostic, JumpTable jumpTable)
            {
                return new TestEngine(trigger, container, snapshot, persistingBlock, settings, gas, diagnostic, jumpTable);
            }
        }

        class TestEngine : ApplicationEngine
        {
            public TestEngine(TriggerType trigger, IVerifiable container, DataCache snapshotCache,
                Block persistingBlock, ProtocolSettings settings, long gas, IDiagnostic diagnostic, JumpTable jumpTable)
                : base(trigger, container, snapshotCache, persistingBlock, settings, gas, diagnostic, jumpTable)
            {
            }
        }
    }
}
