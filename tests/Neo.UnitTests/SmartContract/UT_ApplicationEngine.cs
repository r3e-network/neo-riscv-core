// Copyright (C) 2015-2026 The Neo Project.
//
// UT_ApplicationEngine.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Extensions;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.Network.P2P.Payloads;
using Neo.Network.P2P;
using Neo.Cryptography;
using Neo.Wallets;
using Neo.UnitTests.Extensions;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Array = Neo.VM.Types.Array;
using Boolean = Neo.VM.Types.Boolean;
using ByteString = Neo.VM.Types.ByteString;
using Integer = Neo.VM.Types.Integer;
using Null = Neo.VM.Types.Null;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.UnitTests.SmartContract
{
    [TestClass]
    public partial class UT_ApplicationEngine
    {
        private static IApplicationEngineProvider s_provider;
        private static bool s_adapterAvailable;

        private string eventName = null;

        [ClassInitialize]
        public static void ClassInit(TestContext _)
        {
            var libraryPath = RiscvAdapterTestSupport.ResolveProviderLibraryPath();
            s_adapterAvailable = !string.IsNullOrWhiteSpace(libraryPath) && RiscvAdapterTestSupport.CanUseAdapter();
            if (s_adapterAvailable)
            {
                s_provider = RiscvAdapterTestSupport.CreateProvider(libraryPath);
                ApplicationEngine.Provider = s_provider;
            }
        }

        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static void ClassCleanup()
        {
            ApplicationEngine.Provider = null;
            if (s_provider is IDisposable disposable)
                disposable.Dispose();
            s_provider = null;
        }

        private void RequireNativeBridge()
        {
            if (!s_adapterAvailable)
                Assert.Inconclusive(RiscvAdapterTestSupport.AdapterUnavailableReason());
        }

        private static void AssertHalt(ApplicationEngine engine)
        {
            var state = engine.Execute();
            if (state != VMState.HALT)
                Assert.Fail(engine.FaultException?.ToString() ?? engine.GetEngineStackInfoOnFault(exceptionStackTrace: false));
        }

        [TestMethod]
        public void TestNotify()
        {
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            engine.LoadScript(System.Array.Empty<byte>());
            engine.Notify += Test_Notify1;
            const string notifyEvent = "TestEvent";

            engine.SendNotification(UInt160.Zero, notifyEvent, new Array());
            Assert.AreEqual(notifyEvent, eventName);

            engine.Notify += Test_Notify2;
            engine.SendNotification(UInt160.Zero, notifyEvent, new Array());
            Assert.IsNull(eventName);

            eventName = notifyEvent;
            engine.Notify -= Test_Notify1;
            engine.SendNotification(UInt160.Zero, notifyEvent, new Array());
            Assert.IsNull(eventName);

            engine.Notify -= Test_Notify2;
            engine.SendNotification(UInt160.Zero, notifyEvent, new Array());
            Assert.IsNull(eventName);
        }

        private void Test_Notify1(object sender, NotifyEventArgs e)
        {
            eventName = e.EventName;
        }

        private void Test_Notify2(object sender, NotifyEventArgs e)
        {
            eventName = null;
        }

        [TestMethod]
        public void TestCreateDummyBlock()
        {
            var system = TestBlockchain.GetSystem();
            var snapshotCache = system.GetTestSnapshotCache();
            byte[] SyscallSystemRuntimeCheckWitnessHash = [0x68, 0xf8, 0x27, 0xec, 0x8c];
            ApplicationEngine engine = ApplicationEngine.Run(SyscallSystemRuntimeCheckWitnessHash, snapshotCache, settings: TestProtocolSettings.Default);
            Assert.AreEqual(0u, engine.PersistingBlock.Version);
            Assert.AreEqual(system.GenesisBlock.Hash, engine.PersistingBlock.PrevHash);
            Assert.AreEqual(new UInt256(), engine.PersistingBlock.MerkleRoot);
        }

        [TestMethod]
        public void TestCheckingHardfork()
        {
            var allHardforks = Enum.GetValues(typeof(Hardfork)).Cast<Hardfork>().ToList();

            var builder = ImmutableDictionary.CreateBuilder<Hardfork, uint>();
            builder.Add(Hardfork.HF_Aspidochelone, 0);
            builder.Add(Hardfork.HF_Basilisk, 1);

            var setting = builder.ToImmutable();

            // Check for continuity in configured hardforks
            var sortedHardforks = setting.Keys
                .OrderBy(h => allHardforks.IndexOf(h))
                .ToList();

            for (int i = 0; i < sortedHardforks.Count - 1; i++)
            {
                int currentIndex = allHardforks.IndexOf(sortedHardforks[i]);
                int nextIndex = allHardforks.IndexOf(sortedHardforks[i + 1]);

                // If they aren't consecutive, return false.
                var inc = nextIndex - currentIndex;
                Assert.AreEqual(1, inc);
            }

            // Check that block numbers are not higher in earlier hardforks than in later ones
            for (int i = 0; i < sortedHardforks.Count - 1; i++)
            {
                Assert.IsLessThanOrEqualTo(setting[sortedHardforks[i + 1]], setting[sortedHardforks[i]]);
            }
        }

        [TestMethod]
        public void TestNativeRiscvBridgeExecutesTrivialScript()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);

            engine.LoadScript(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET });

            AssertHalt(engine);
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(1, engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeReturnsFullResultStack()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);

            engine.LoadScript(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.PUSH2, (byte)OpCode.RET });

            AssertHalt(engine);
            Assert.AreEqual(2, engine.ResultStack.Count);
            Assert.AreEqual(2, engine.ResultStack.Pop().GetInteger());
            Assert.AreEqual(1, engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeUnsupportedOpcodeFaultsEngine()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);

            engine.LoadScript(new byte[] { 0xff });

            Assert.AreEqual(VMState.FAULT, engine.Execute());
            Assert.IsNotNull(engine.FaultException);
            Assert.Contains("unsupported opcode", engine.FaultException!.Message);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimePlatformSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_Platform);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            AssertHalt(engine);
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual("NEO", engine.ResultStack.Pop<ByteString>().GetString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetTriggerSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Verification, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetTrigger);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual((int)TriggerType.Verification, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetNetworkSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetNetwork);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual((int)TestProtocolSettings.Default.Network, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetAddressVersionSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetAddressVersion);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(TestProtocolSettings.Default.AddressVersion, (byte)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetTimeSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var block = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = UInt256.Zero,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = 1_710_000_000,
                    Nonce = 0,
                    Index = 1,
                    PrimaryIndex = 0,
                    NextConsensus = UInt160.Zero,
                    Witness = Witness.Empty,
                },
                Transactions = [],
            };

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, block, TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetTime);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(1_710_000_000ul, (ulong)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeGetTimeFaultsWithoutPersistingBlock()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetTime);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.FAULT, engine.Execute());
            Assert.IsNotNull(engine.FaultException);
            Assert.Contains("GetTime", engine.FaultException!.Message);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeScriptHashSyscalls()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetExecutingScriptHash);
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetEntryScriptHash);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var expectedHash = scriptBytes.ToScriptHash();

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(2, engine.ResultStack.Count);
            Assert.AreEqual(expectedHash, new UInt160(engine.ResultStack.Pop().GetSpan()));
            Assert.AreEqual(expectedHash, new UInt160(engine.ResultStack.Pop().GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGasLeftSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default, gas: 987_654);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GasLeft);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(engine.GasLeft, (long)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetCallingScriptHashAsNull()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetCallingScriptHash);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.IsInstanceOfType(engine.ResultStack.Pop(), typeof(Null));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeLogSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            string logged = null;

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            engine.Log += static (_, args) => { };
            engine.Log += (_, args) => logged = args.Message;

            using var script = new ScriptBuilder();
            script.EmitPush("hello");
            script.EmitSysCall(ApplicationEngine.System_Runtime_Log);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual("hello", logged);
            Assert.AreEqual(0, engine.ResultStack.Count);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeCheckWitnessSyscall()
        {
            RequireNativeBridge();

            byte[] privateKey = { 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
                0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01 };
            var keyPair = new KeyPair(privateKey);
            var account = Contract.CreateSignatureRedeemScript(keyPair.PublicKey).ToScriptHash();
            var tx = TestUtils.GetTransaction(account);
            tx.Signers[0].Scopes = WitnessScope.Global;
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshotCache, settings: TestProtocolSettings.Default);
            Assert.IsInstanceOfType(engine.GetScriptContainer(), typeof(Neo.VM.Types.Array));
            using var script = new ScriptBuilder();
            script.EmitPush(account.ToArray());
            script.EmitSysCall(ApplicationEngine.System_Runtime_CheckWitness);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.IsTrue(engine.ResultStack.Pop<Boolean>().GetBoolean());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesCallingScriptHashForMultipleLoadedScripts()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            byte[] callerScript = { (byte)OpCode.PUSH1, (byte)OpCode.RET };

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var callee = new ScriptBuilder();
            callee.EmitSysCall(ApplicationEngine.System_Runtime_GetCallingScriptHash);
            callee.Emit(OpCode.RET);
            var calleeScript = callee.ToArray();

            engine.LoadScript(callerScript);
            engine.LoadScript(calleeScript);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(callerScript.ToScriptHash(), new UInt160(engine.ResultStack.Pop().GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetInvocationCounter()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetInvocationCounter);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            engine.LoadScript(scriptBytes);
            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(2, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeBurnGasSyscall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default, gas: 1_000);
            using var script = new ScriptBuilder();
            script.EmitPush(5);
            script.EmitSysCall(ApplicationEngine.System_Runtime_BurnGas);
            script.EmitSysCall(ApplicationEngine.System_Runtime_GasLeft);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(5, (long)engine.ResultStack.Pop().GetInteger());
            Assert.AreEqual(5, engine.GasLeft);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeCurrentSignersSyscall()
        {
            RequireNativeBridge();

            var tx = TestUtils.GetTransaction(UInt160.Zero);
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_CurrentSigners.Hash);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            var result = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(result, typeof(Neo.VM.Types.Array));
            Assert.HasCount(1, result as Neo.VM.Types.Array);
            result = (result as Neo.VM.Types.Array)[0];
            Assert.IsInstanceOfType(result, typeof(Neo.VM.Types.Array));
            Assert.HasCount(5, result as Neo.VM.Types.Array);
            result = (result as Neo.VM.Types.Array)[0];
            Assert.AreEqual(UInt160.Zero, new UInt160(result.GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetScriptContainerSyscall()
        {
            RequireNativeBridge();

            var tx = TestUtils.GetTransaction(UInt160.Zero);
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetScriptContainer);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            var result = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(result, typeof(Neo.VM.Types.Array));
            Assert.HasCount(8, result as Neo.VM.Types.Array);
            var container = (Neo.VM.Types.Array)result;
            Assert.AreEqual(tx.Hash, new UInt256(container[0].GetSpan()));
            Assert.AreEqual(0, (int)container[1].GetInteger());
            Assert.AreEqual(UInt160.Zero, new UInt160(container[3].GetSpan()));
            Assert.AreEqual((byte)OpCode.PUSH2, container[7].GetSpan()[0]);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetRandomSyscall()
        {
            RequireNativeBridge();

            var tx = TestUtils.GetTransaction(UInt160.Zero);
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var block = TestBlockchain.GetSystem().GenesisBlock;

            using var directEngine = ApplicationEngine.Create(TriggerType.Application, tx, null, block, settings: TestProtocolSettings.Default, gas: 1100_00000000);
            var expected = directEngine.GetRandom();

            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, null, block, settings: TestProtocolSettings.Default, gas: 1100_00000000);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetRandom);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(expected, engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeNotifyAndGetNotifications()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            string notifiedName = null;

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            engine.Notify += (_, args) => notifiedName = args.EventName;
            using var script = new ScriptBuilder();
            script.Emit(OpCode.NEWARRAY0);
            script.EmitPush("evt");
            script.EmitSysCall(ApplicationEngine.System_Runtime_Notify);
            script.Emit(OpCode.PUSHNULL);
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetNotifications);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            engine.LoadScript(scriptBytes);
            engine.CurrentContext.GetState<ExecutionContextState>().Contract = TestUtils.GetContract(
                scriptBytes,
                new ContractManifest
                {
                    Name = "notify",
                    Groups = [],
                    SupportedStandards = [],
                    Abi = new ContractAbi
                    {
                        Methods = [],
                        Events =
                        [
                            new ContractEventDescriptor
                            {
                                Name = "evt",
                                Parameters = [],
                            }
                        ]
                    },
                    Permissions = [],
                    Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                });

            AssertHalt(engine);
            Assert.AreEqual("evt", notifiedName);
            Assert.AreEqual(1, engine.Notifications.Count);

            var notifications = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(notifications, typeof(Neo.VM.Types.Array));
            Assert.HasCount(1, notifications as Neo.VM.Types.Array);

            var notification = (Neo.VM.Types.Array)(notifications as Neo.VM.Types.Array)[0];
            Assert.AreEqual(scriptBytes.ToScriptHash(), new UInt160(notification[0].GetSpan()));
            Assert.AreEqual("evt", notification[1].GetString());
            Assert.AreEqual(0, ((Neo.VM.Types.Array)notification[2]).Count);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetNotificationsWithHashFilter()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.Emit(OpCode.NEWARRAY0);
            script.EmitPush("evt");
            script.EmitSysCall(ApplicationEngine.System_Runtime_Notify);
            script.EmitPush(UInt160.Zero.GetSpan());
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetNotifications);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            engine.LoadScript(scriptBytes);
            engine.CurrentContext.GetState<ExecutionContextState>().Contract = TestUtils.GetContract(
                scriptBytes,
                new ContractManifest
                {
                    Name = "notify",
                    Groups = [],
                    SupportedStandards = [],
                    Abi = new ContractAbi
                    {
                        Methods = [],
                        Events =
                        [
                            new ContractEventDescriptor
                            {
                                Name = "evt",
                                Parameters = [],
                            }
                        ]
                    },
                    Permissions = [],
                    Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                });

            Assert.AreEqual(VMState.HALT, engine.Execute());
            var notifications = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(notifications, typeof(Neo.VM.Types.Array));
            Assert.HasCount(0, notifications as Neo.VM.Types.Array);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeGetNotificationsWithMatchingHash()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.Emit(OpCode.NEWARRAY0);
            script.EmitPush("evt");
            script.EmitSysCall(ApplicationEngine.System_Runtime_Notify);
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetExecutingScriptHash);
            script.EmitSysCall(ApplicationEngine.System_Runtime_GetNotifications);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            engine.LoadScript(scriptBytes);
            engine.CurrentContext.GetState<ExecutionContextState>().Contract = TestUtils.GetContract(
                scriptBytes,
                new ContractManifest
                {
                    Name = "notify",
                    Groups = [],
                    SupportedStandards = [],
                    Abi = new ContractAbi
                    {
                        Methods = [],
                        Events =
                        [
                            new ContractEventDescriptor
                            {
                                Name = "evt",
                                Parameters = [],
                            }
                        ]
                    },
                    Permissions = [],
                    Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                });

            Assert.AreEqual(VMState.HALT, engine.Execute());
            var notifications = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(notifications, typeof(Neo.VM.Types.Array));
            Assert.HasCount(1, notifications as Neo.VM.Types.Array);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageGetContextPutAndGet()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.Emit(OpCode.DUP);
            script.EmitPush("k");
            script.EmitPush("v");
            script.EmitSysCall(ApplicationEngine.System_Storage_Put);
            script.EmitPush("k");
            script.EmitSysCall(ApplicationEngine.System_Storage_Get);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            snapshotCache.AddContract(scriptBytes.ToScriptHash(), TestUtils.GetContract(scriptBytes));
            engine.LoadScript(scriptBytes);

            AssertHalt(engine);
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual("v", engine.ResultStack.Pop<ByteString>().GetString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageReadOnlyContextAndAsReadOnly()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetReadOnlyContext);
            script.EmitSysCall(ApplicationEngine.System_Storage_AsReadOnly);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            snapshotCache.AddContract(scriptBytes.ToScriptHash(), TestUtils.GetContract(scriptBytes));
            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            var context = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(context, typeof(Neo.VM.Types.Array));
            Assert.AreEqual(true, ((Neo.VM.Types.Array)context)[1].GetBoolean());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageDelete()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.Emit(OpCode.DUP);
            script.Emit(OpCode.DUP);
            script.EmitPush("k");
            script.EmitPush("v");
            script.EmitSysCall(ApplicationEngine.System_Storage_Put);
            script.EmitPush("k");
            script.EmitSysCall(ApplicationEngine.System_Storage_Delete);
            script.EmitPush("k");
            script.EmitSysCall(ApplicationEngine.System_Storage_Get);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            snapshotCache.AddContract(scriptBytes.ToScriptHash(), TestUtils.GetContract(scriptBytes));
            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.IsInstanceOfType(engine.ResultStack.Pop(), typeof(Null));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageLocalPutGetAndDelete()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitPush("k");
            script.EmitPush("v");
            script.EmitSysCall(ApplicationEngine.System_Storage_Local_Put);
            script.EmitPush("k");
            script.EmitSysCall(ApplicationEngine.System_Storage_Local_Get);
            script.EmitPush("k");
            script.EmitSysCall(ApplicationEngine.System_Storage_Local_Delete);
            script.EmitPush("k");
            script.EmitSysCall(ApplicationEngine.System_Storage_Local_Get);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            snapshotCache.AddContract(scriptBytes.ToScriptHash(), TestUtils.GetContract(scriptBytes));
            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(2, engine.ResultStack.Count);
            Assert.IsInstanceOfType(engine.ResultStack.Pop(), typeof(Null));
            Assert.AreEqual("v", engine.ResultStack.Pop<ByteString>().GetString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeStorageReadOnlyContextFaultsOnPut()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetReadOnlyContext);
            script.EmitPush("k");
            script.EmitPush("v");
            script.EmitSysCall(ApplicationEngine.System_Storage_Put);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();

            snapshotCache.AddContract(scriptBytes.ToScriptHash(), TestUtils.GetContract(scriptBytes));
            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.FAULT, engine.Execute());
            Assert.IsNotNull(engine.FaultException);
            Assert.Contains("read-only", engine.FaultException!.Message);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageFindIteratorValuesOnly()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var storageItem = new StorageItem { Value = new byte[] { 0x01, 0x02 } };

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)FindOptions.ValuesOnly);
            script.EmitSysCall(ApplicationEngine.System_Storage_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01 } }, storageItem);

            engine.LoadScript(scriptBytes);

            AssertHalt(engine);
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(storageItem.Value.Span.ToHexString(), engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageLocalFindIteratorValuesOnly()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var storageItem = new StorageItem { Value = new byte[] { 0x0A, 0x0B } };

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)FindOptions.ValuesOnly);
            script.EmitSysCall(ApplicationEngine.System_Storage_Local_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01 } }, storageItem);

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(storageItem.Value.Span.ToHexString(), engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeLoadScript()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.Emit(OpCode.NEWARRAY0);
            script.EmitPush((byte)CallFlags.All);
            script.EmitPush(new byte[] { (byte)OpCode.PUSH1, (byte)OpCode.RET });
            script.EmitSysCall(ApplicationEngine.System_Runtime_LoadScript);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(1, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeLoadScriptWithArgs()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitPush(1);
            script.EmitPush(2);
            script.EmitPush(2);
            script.Emit(OpCode.PACK);
            script.EmitPush((byte)CallFlags.All);
            script.EmitPush(new byte[] { (byte)OpCode.ADD, (byte)OpCode.RET });
            script.EmitSysCall(ApplicationEngine.System_Runtime_LoadScript);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(3, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeLoadScriptCallingHash()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.Emit(OpCode.NEWARRAY0);
            script.EmitPush((byte)CallFlags.All);
            script.EmitPush(new byte[]
            {
                (byte)OpCode.SYSCALL,
                (byte)ApplicationEngine.System_Runtime_GetCallingScriptHash,
                (byte)(ApplicationEngine.System_Runtime_GetCallingScriptHash >> 8),
                (byte)(ApplicationEngine.System_Runtime_GetCallingScriptHash >> 16),
                (byte)(ApplicationEngine.System_Runtime_GetCallingScriptHash >> 24),
                (byte)OpCode.RET
            });
            script.EmitSysCall(ApplicationEngine.System_Runtime_LoadScript);
            script.Emit(OpCode.RET);
            var outerScript = script.ToArray();

            engine.LoadScript(outerScript);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual(outerScript.ToScriptHash(), new UInt160(engine.ResultStack.Pop().GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesRuntimeLoadScriptCallFlags()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.Emit(OpCode.NEWARRAY0);
            script.EmitPush((byte)CallFlags.All);
            script.EmitPush(new byte[]
            {
                (byte)OpCode.SYSCALL,
                (byte)ApplicationEngine.System_Contract_GetCallFlags,
                (byte)(ApplicationEngine.System_Contract_GetCallFlags >> 8),
                (byte)(ApplicationEngine.System_Contract_GetCallFlags >> 16),
                (byte)(ApplicationEngine.System_Contract_GetCallFlags >> 24),
            });
            script.EmitSysCall(ApplicationEngine.System_Runtime_LoadScript);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual((int)CallFlags.ReadOnly, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractGetCallFlags()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_GetCallFlags);
            script.Emit(OpCode.RET);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual((int)CallFlags.All, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCreateStandardAccount()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var settings = TestProtocolSettings.Default;

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: settings, gas: 1100_00000000);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_CreateStandardAccount, settings.StandbyCommittee[0].EncodePoint(true));

            engine.LoadScript(script.ToArray());

            AssertHalt(engine);
            Assert.AreEqual(Contract.CreateSignatureRedeemScript(settings.StandbyCommittee[0]).ToScriptHash(), new UInt160(engine.ResultStack.Pop().GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCreateMultisigAccount()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var settings = TestProtocolSettings.Default;

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: settings, gas: 1100_00000000);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Contract_CreateMultisigAccount, new object[]
            {
                2,
                3,
                settings.StandbyCommittee[0].EncodePoint(true),
                settings.StandbyCommittee[1].EncodePoint(true),
                settings.StandbyCommittee[2].EncodePoint(true)
            });

            engine.LoadScript(script.ToArray());

            AssertHalt(engine);
            Assert.AreEqual(Contract.CreateMultiSigRedeemScript(2, settings.StandbyCommittee.Take(3).ToArray()).ToScriptHash(), new UInt160(engine.ResultStack.Pop().GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCall()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var callee = TestUtils.GetContract(new byte[] { (byte)OpCode.PUSH1 }, TestUtils.CreateManifest("test", ContractParameterType.Any));
            snapshotCache.AddContract(callee.Hash, callee);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(callee.Hash, "test");

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallWithArgs()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var callee = TestUtils.GetContract(new byte[] { (byte)OpCode.ADD }, TestUtils.CreateManifest("sum", ContractParameterType.Any, ContractParameterType.Integer, ContractParameterType.Integer));
            snapshotCache.AddContract(callee.Hash, callee);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(callee.Hash, "sum", 1, 2);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(3, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallExecutingHash()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var calleeScript = new byte[]
            {
                (byte)OpCode.SYSCALL,
                (byte)ApplicationEngine.System_Runtime_GetExecutingScriptHash,
                (byte)(ApplicationEngine.System_Runtime_GetExecutingScriptHash >> 8),
                (byte)(ApplicationEngine.System_Runtime_GetExecutingScriptHash >> 16),
                (byte)(ApplicationEngine.System_Runtime_GetExecutingScriptHash >> 24),
            };
            var callee = TestUtils.GetContract(calleeScript, TestUtils.CreateManifest("hash", ContractParameterType.Any));
            snapshotCache.AddContract(callee.Hash, callee);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(callee.Hash, "hash");

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(callee.Hash, new UInt160(engine.ResultStack.Pop().GetSpan()));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallWithNonZeroMethodOffset()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var calleeScript = new byte[] { (byte)OpCode.PUSH0, (byte)OpCode.PUSH1 };
            var manifest = TestUtils.CreateManifest("offset", ContractParameterType.Any);
            manifest.Abi.Methods[0].Offset = 1;
            var callee = TestUtils.GetContract(calleeScript, manifest);
            snapshotCache.AddContract(callee.Hash, callee);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(callee.Hash, "offset");

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesTopLevelInitialOffset()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            engine.LoadScript(new byte[] { (byte)OpCode.PUSH0, (byte)OpCode.PUSH1 }, initialPosition: 1);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallToNativeContract()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.NEO.Hash, "getGasPerBlock");

            engine.LoadScript(script.ToArray());

            AssertHalt(engine);
            Assert.AreNotEqual(0, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallToNativeArrayResult()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.NEO.Hash, "getCommittee");

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            var result = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(result, typeof(Neo.VM.Types.Array));
            Assert.IsTrue(((Neo.VM.Types.Array)result).Count > 0);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallToNativeStateChange()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var persistingBlock = TestBlockchain.GetSystem().GenesisBlock;
            var committeeAddress = NativeContract.NEO.GetCommitteeAddress(snapshotCache);
            var tx = TestUtils.GetTransaction(committeeAddress);
            tx.Signers[0].Scopes = WitnessScope.Global;
            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshotCache, persistingBlock, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(NativeContract.NEO.Hash, "setGasPerBlock", 123456789);
            script.EmitDynamicCall(NativeContract.NEO.Hash, "getGasPerBlock");

            engine.LoadScript(script.ToArray());
            engine.CurrentContext.GetState<ExecutionContextState>().Contract = new()
            {
                Hash = UInt160.Zero,
                Nef = null!,
                Manifest = new()
                {
                    Name = "",
                    Groups = [],
                    SupportedStandards = [],
                    Abi = new()
                    {
                        Methods = [],
                        Events = []
                    },
                    Permissions = [ContractPermission.DefaultPermission],
                    Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                }
            };

            AssertHalt(engine);
            Assert.AreEqual(123456789, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallPermissions()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var callee = TestUtils.GetContract(new byte[] { (byte)OpCode.PUSH1 }, TestUtils.CreateManifest("disallowed", ContractParameterType.Any));
            snapshotCache.AddContract(callee.Hash, callee);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, null, ProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(callee.Hash, "disallowed");

            engine.LoadScript(script.ToArray());
            engine.CurrentContext.GetState<ExecutionContextState>().Contract = new()
            {
                Hash = UInt160.Zero,
                Nef = null!,
                Manifest = new()
                {
                    Name = "",
                    Groups = [],
                    SupportedStandards = [],
                    Abi = new()
                    {
                        Methods = [],
                        Events = []
                    },
                    Permissions =
                    [
                        new ContractPermission
                        {
                            Contract = ContractPermissionDescriptor.Create(callee.Hash),
                            Methods = WildcardContainer<string>.Create(["allowed"])
                        }
                    ],
                    Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                }
            };

            Assert.AreEqual(VMState.FAULT, engine.Execute());
            Assert.IsNotNull(engine.FaultException);
            Assert.Contains("Cannot Call Method disallowed", engine.FaultException!.Message);
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallSafeMethodFlags()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var manifest = TestUtils.CreateManifest("flags", ContractParameterType.Any);
            manifest.Abi.Methods[0].Safe = true;
            var callee = TestUtils.GetContract(new byte[]
            {
                (byte)OpCode.SYSCALL,
                (byte)ApplicationEngine.System_Contract_GetCallFlags,
                (byte)(ApplicationEngine.System_Contract_GetCallFlags >> 8),
                (byte)(ApplicationEngine.System_Contract_GetCallFlags >> 16),
                (byte)(ApplicationEngine.System_Contract_GetCallFlags >> 24),
            }, manifest);
            snapshotCache.AddContract(callee.Hash, callee);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitDynamicCall(callee.Hash, "flags");

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual((int)CallFlags.ReadOnly, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesCryptoCheckSig()
        {
            RequireNativeBridge();

            byte[] privateKey = { 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
                0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01 };
            var keyPair = new KeyPair(privateKey);
            var tx = TestUtils.GetTransaction(UInt160.Zero);
            var message = tx.GetSignData(TestProtocolSettings.Default.Network);
            var signature = Crypto.Sign(message, privateKey);
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Crypto_CheckSig, keyPair.PublicKey.EncodePoint(false), signature);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.IsTrue(engine.ResultStack.Pop<Boolean>().GetBoolean());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesCryptoCheckMultisig()
        {
            RequireNativeBridge();

            var tx = TestUtils.GetTransaction(UInt160.Zero);
            var message = tx.GetSignData(TestProtocolSettings.Default.Network);
            var privateKey1 = Enumerable.Repeat((byte)0x01, 32).ToArray();
            var privateKey2 = Enumerable.Repeat((byte)0x02, 32).ToArray();
            var keyPair1 = new KeyPair(privateKey1);
            var keyPair2 = new KeyPair(privateKey2);
            var signatures = new[]
            {
                Crypto.Sign(message, privateKey1),
                Crypto.Sign(message, privateKey2),
            };
            var pubKeys = new[]
            {
                keyPair1.PublicKey.EncodePoint(false),
                keyPair2.PublicKey.EncodePoint(false),
            };
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            using var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.CreateArray(signatures);
            script.CreateArray(pubKeys);
            script.EmitSysCall(ApplicationEngine.System_Crypto_CheckMultisig);

            engine.LoadScript(script.ToArray());

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.IsTrue(engine.ResultStack.Pop<Boolean>().GetBoolean());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageFindKeysOnlyRemovePrefix()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var storageItem = new StorageItem { Value = new byte[] { 0x55 } };

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)(FindOptions.KeysOnly | FindOptions.RemovePrefix));
            script.EmitSysCall(ApplicationEngine.System_Storage_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01, 0xAA } }, storageItem);

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual(1, engine.ResultStack.Count);
            Assert.AreEqual("aa", engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageFindDefaultTupleShape()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            var storageItem = new StorageItem { Value = new byte[] { 0x0A, 0x0B } };

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)FindOptions.None);
            script.EmitSysCall(ApplicationEngine.System_Storage_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01 } }, storageItem);

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            var tuple = engine.ResultStack.Pop();
            Assert.IsInstanceOfType(tuple, typeof(Neo.VM.Types.Struct));
            Assert.AreEqual("01", ((Neo.VM.Types.Struct)tuple)[0].GetSpan().ToHexString());
            Assert.AreEqual(storageItem.Value.Span.ToHexString(), ((Neo.VM.Types.Struct)tuple)[1].GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageFindBackwards()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)(FindOptions.ValuesOnly | FindOptions.Backwards));
            script.EmitSysCall(ApplicationEngine.System_Storage_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01, 0x01 } }, new StorageItem { Value = new byte[] { 0x01 } });
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01, 0x02 } }, new StorageItem { Value = new byte[] { 0x02 } });

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual("02", engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageFindDeserializePickField0()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)(FindOptions.ValuesOnly | FindOptions.DeserializeValues | FindOptions.PickField0));
            script.EmitSysCall(ApplicationEngine.System_Storage_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(
                new StorageKey { Id = contract.Id, Key = new byte[] { 0x01 } },
                new StorageItem { Value = BinarySerializer.Serialize(new Neo.VM.Types.Array(new StackItem[] { new ByteString(new byte[] { 0x0A }), new ByteString(new byte[] { 0x0B }) }), ExecutionEngineLimits.Default) });

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual("0a", engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageFindDeserializePickField1()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitSysCall(ApplicationEngine.System_Storage_GetContext);
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)(FindOptions.ValuesOnly | FindOptions.DeserializeValues | FindOptions.PickField1));
            script.EmitSysCall(ApplicationEngine.System_Storage_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(
                new StorageKey { Id = contract.Id, Key = new byte[] { 0x01 } },
                new StorageItem { Value = BinarySerializer.Serialize(new Neo.VM.Types.Array(new StackItem[] { new ByteString(new byte[] { 0x0A }), new ByteString(new byte[] { 0x0B }) }), ExecutionEngineLimits.Default) });

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual("0b", engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesStorageLocalFindKeysOnlyRemovePrefix()
        {
            RequireNativeBridge();

            var snapshotCache = TestBlockchain.GetTestSnapshotCache();
            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, settings: TestProtocolSettings.Default);
            using var script = new ScriptBuilder();
            script.EmitPush(new byte[] { 0x01 });
            script.EmitPush((byte)(FindOptions.KeysOnly | FindOptions.RemovePrefix));
            script.EmitSysCall(ApplicationEngine.System_Storage_Local_Find);
            script.Emit(OpCode.DUP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Next);
            script.Emit(OpCode.DROP);
            script.EmitSysCall(ApplicationEngine.System_Iterator_Value);
            script.Emit(OpCode.RET);
            var scriptBytes = script.ToArray();
            var contract = TestUtils.GetContract(scriptBytes);
            snapshotCache.AddContract(contract.Hash, contract);
            snapshotCache.Add(new StorageKey { Id = contract.Id, Key = new byte[] { 0x01, 0xAA } }, new StorageItem { Value = new byte[] { 0x01 } });

            engine.LoadScript(scriptBytes);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreEqual("aa", engine.ResultStack.Pop().GetSpan().ToHexString());
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesNativeOnPersistAndPostPersist()
        {
            RequireNativeBridge();

            var system = TestBlockchain.GetSystem();
            var snapshot = system.GetSnapshotCache();
            var block = system.GenesisBlock;

            using (var onPersistScript = new ScriptBuilder())
            using (var onPersistEngine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings: TestProtocolSettings.Default))
            {
                onPersistScript.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                onPersistEngine.LoadScript(onPersistScript.ToArray());
                Assert.AreEqual(VMState.HALT, onPersistEngine.Execute());
                onPersistEngine.SnapshotCache.Commit();
            }

            Assert.IsNotNull(NativeContract.ContractManagement.GetContract(snapshot, NativeContract.NEO.Hash));
            Assert.IsNotNull(NativeContract.ContractManagement.GetContract(snapshot, NativeContract.GAS.Hash));

            using (var postPersistScript = new ScriptBuilder())
            using (var postPersistEngine = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshot, block, settings: TestProtocolSettings.Default))
            {
                postPersistScript.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                postPersistEngine.LoadScript(postPersistScript.ToArray());
                Assert.AreEqual(VMState.HALT, postPersistEngine.Execute());
                postPersistEngine.SnapshotCache.Commit();
            }

            Assert.AreEqual(block.Index, NativeContract.Ledger.CurrentIndex(snapshot));
            Assert.AreEqual(block.Hash, NativeContract.Ledger.CurrentHash(snapshot));
        }

        [TestMethod]
        public void TestNativeRiscvBridgeHandlesContractCallNativeScript()
        {
            RequireNativeBridge();

            var snapshot = TestBlockchain.GetTestSnapshotCache();
            var settings = TestProtocolSettings.Default;
            var nativeState = NativeContract.NEO.GetContractState(settings, 0);
            var method = nativeState.Manifest.Abi.GetMethod("getGasPerBlock", 0);

            using var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshot, settings: settings, gas: 1100_00000000);
            engine.LoadContract(nativeState, method, CallFlags.All);

            Assert.AreEqual(VMState.HALT, engine.Execute());
            Assert.AreNotEqual(0, (int)engine.ResultStack.Pop().GetInteger());
        }

        [TestMethod]
        public void TestSystem_Contract_Call_Permissions()
        {
            UInt160 scriptHash;
            var snapshotCache = TestBlockchain.GetTestSnapshotCache();

            // Setup: put a simple contract to the storage.
            using (var script = new ScriptBuilder())
            {
                // Push True on stack and return.
                script.EmitPush(true);
                script.Emit(OpCode.RET);

                // Mock contract and put it to the Managemant's storage.
                scriptHash = script.ToArray().ToScriptHash();

                snapshotCache.DeleteContract(scriptHash);
                var contract = TestUtils.GetContract(script.ToArray(), TestUtils.CreateManifest("test", ContractParameterType.Any));
                contract.Manifest.Abi.Methods = [
                    new ContractMethodDescriptor { Name = "disallowed", Parameters = [] },
                    new ContractMethodDescriptor { Name = "test", Parameters = [] }
                ];
                snapshotCache.AddContract(scriptHash, contract);
            }

            // Disallowed method call.
            using (var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, null, ProtocolSettings.Default))
            using (var script = new ScriptBuilder())
            {
                // Build call script calling disallowed method.
                script.EmitDynamicCall(scriptHash, "disallowed");

                // Mock executing state to be a contract-based.
                engine.LoadScript(script.ToArray());
                engine.CurrentContext.GetState<ExecutionContextState>().Contract = new()
                {
                    Hash = UInt160.Zero,
                    Nef = null!,
                    Manifest = new()
                    {
                        Name = "",
                        Groups = [],
                        SupportedStandards = [],
                        Abi = new()
                        {
                            Methods = [],
                            Events = []
                        },
                        Permissions = [
                            new ContractPermission
                            {
                                Contract = ContractPermissionDescriptor.Create(scriptHash),
                                Methods = WildcardContainer<string>.Create(["test"]) // allowed to call only "test" method of the target contract.
                            }
                        ],
                        Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                    }
                };
                var currentScriptHash = engine.EntryScriptHash;

                Assert.AreEqual("", engine.GetEngineStackInfoOnFault());
                Assert.AreEqual(VMState.FAULT, engine.Execute());
                Assert.Contains($"Cannot Call Method disallowed Of Contract {scriptHash.ToString()}", engine.FaultException.ToString());
                string traceback = engine.GetEngineStackInfoOnFault();
                Assert.Contains($"Cannot Call Method disallowed Of Contract {scriptHash.ToString()}", traceback);
                Assert.Contains("CurrentScriptHash", traceback);
                Assert.Contains("EntryScriptHash", traceback);
                Assert.Contains("InstructionPointer", traceback);
                Assert.Contains("Script Length=", traceback);
            }

            // Allowed method call.
            using (var engine = ApplicationEngine.Create(TriggerType.Application, null, snapshotCache, null, ProtocolSettings.Default))
            using (var script = new ScriptBuilder())
            {
                // Build call script.
                script.EmitDynamicCall(scriptHash, "test");

                // Mock executing state to be a contract-based.
                engine.LoadScript(script.ToArray());
                engine.CurrentContext.GetState<ExecutionContextState>().Contract = new()
                {
                    Hash = UInt160.Zero,
                    Nef = null!,
                    Manifest = new()
                    {
                        Name = "",
                        Groups = [],
                        SupportedStandards = [],
                        Abi = new()
                        {
                            Methods = [],
                            Events = []
                        },
                        Permissions = [
                            new ContractPermission
                            {
                                Contract = ContractPermissionDescriptor.Create(scriptHash),
                                Methods = WildcardContainer<string>.Create(["test"]) // allowed to call only "test" method of the target contract.
                            }
                        ],
                        Trusts = WildcardContainer<ContractPermissionDescriptor>.CreateWildcard()
                    }
                };
                var currentScriptHash = engine.EntryScriptHash;

                Assert.AreEqual(VMState.HALT, engine.Execute());
                Assert.HasCount(1, engine.ResultStack);
                Assert.IsInstanceOfType(engine.ResultStack.Peek(), typeof(Boolean));
                var res = (Boolean)engine.ResultStack.Pop();
                Assert.IsTrue(res.GetBoolean());
            }
        }
    }
}
