using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Json;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using System;

namespace Neo.UnitTests.SmartContract.Manifest
{
    [TestClass]
    public class UT_ContractVmTypeResolver
    {
        [TestMethod]
        public void DefaultsToNeoVmWhenManifestHasNoVmMarker()
        {
            var manifest = TestUtils.CreateDefaultManifest();

            Assert.AreEqual(
                ContractType.NeoVM,
                ContractVmTypeResolver.Resolve(manifest, new byte[] { (byte)VM.OpCode.PUSH1, (byte)VM.OpCode.RET }));
        }

        [TestMethod]
        public void UsesManifestVmMarkerForRiscV()
        {
            var manifest = TestUtils.CreateDefaultManifest();
            manifest.Extra = new JObject
            {
                ["vm"] = "riscv32-polkavm-v1"
            };

            Assert.AreEqual(
                ContractType.RiscV,
                ContractVmTypeResolver.Resolve(manifest, new byte[] { 0x50, 0x56, 0x4D, 0x00, 0x01 }));
        }

        [TestMethod]
        public void UsesManifestVmMarkerForLegacyNeoVm()
        {
            var manifest = TestUtils.CreateDefaultManifest();
            manifest.Extra = new JObject
            {
                ["vm"] = "legacy-neovm-v1"
            };

            Assert.AreEqual(
                ContractType.NeoVM,
                ContractVmTypeResolver.Resolve(manifest, new byte[] { 0x50, 0x56, 0x4D, 0x00 }));
        }

        [TestMethod]
        public void FallsBackToPvmMagicForExistingRiscvContracts()
        {
            var manifest = TestUtils.CreateDefaultManifest();

            Assert.AreEqual(
                ContractType.RiscV,
                ContractVmTypeResolver.Resolve(manifest, new byte[] { 0x50, 0x56, 0x4D, 0x00, 0x01 }));
        }

        [TestMethod]
        public void RejectsUnknownManifestVmMarker()
        {
            var manifest = TestUtils.CreateDefaultManifest();
            manifest.Extra = new JObject
            {
                ["vm"] = "unknown-vm"
            };

            Assert.ThrowsExactly<FormatException>(() =>
                ContractVmTypeResolver.Resolve(manifest, new byte[] { (byte)VM.OpCode.PUSH1, (byte)VM.OpCode.RET }));
        }

        [TestMethod]
        public void RejectsLegacyMarkerOnRiscvBinary()
        {
            var manifest = TestUtils.CreateDefaultManifest();
            manifest.Extra = new JObject
            {
                ["vm"] = ContractVmTypeResolver.LegacyNeoVmMarker
            };

            Assert.ThrowsExactly<FormatException>(() =>
                ContractVmTypeResolver.Resolve(manifest, new byte[] { 0x50, 0x56, 0x4D, 0x00, 0x01 }));
        }

        [TestMethod]
        public void RejectsRiscvMarkerOnLegacyScript()
        {
            var manifest = TestUtils.CreateDefaultManifest();
            manifest.Extra = new JObject
            {
                ["vm"] = ContractVmTypeResolver.RiscvPolkaVmMarker
            };

            Assert.ThrowsExactly<FormatException>(() =>
                ContractVmTypeResolver.Resolve(manifest, new byte[] { (byte)VM.OpCode.PUSH1, (byte)VM.OpCode.RET }));
        }
    }
}
