// Copyright (C) 2015-2026 The Neo Project.
//
// ContractVmTypeResolver.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.SmartContract.Manifest;
using System;

namespace Neo.SmartContract
{
    internal static class ContractVmTypeResolver
    {
        internal const string LegacyNeoVmMarker = "legacy-neovm-v1";
        internal const string RiscvPolkaVmMarker = "riscv32-polkavm-v1";

        internal static ContractType Resolve(ContractManifest manifest, ReadOnlyMemory<byte> script)
        {
            if (manifest is null) throw new ArgumentNullException(nameof(manifest));

            var isRiscvBinary = IsRiscVBinary(script);
            if (TryResolveFromManifest(manifest.Extra, out var contractType))
            {
                ValidateScriptCompatibility(contractType, isRiscvBinary);
                return contractType;
            }

            return ContractType.NeoVM;
        }

        private static bool TryResolveFromManifest(JObject? extra, out ContractType contractType)
        {
            contractType = ContractType.NeoVM;

            var vmToken = extra?["vm"];
            if (vmToken is null || vmToken is JToken.Null)
                return false;

            if (vmToken is not JString vmString)
                return false;

            var marker = vmString.Value;
            if (string.IsNullOrWhiteSpace(marker))
                return false;

            if (string.Equals(marker, LegacyNeoVmMarker, StringComparison.Ordinal))
            {
                contractType = ContractType.NeoVM;
                return true;
            }

            if (string.Equals(marker, RiscvPolkaVmMarker, StringComparison.Ordinal))
            {
                contractType = ContractType.RiscV;
                return true;
            }

            return false;
        }

        internal static bool IsRiscVBinary(ReadOnlyMemory<byte> script)
        {
            var span = script.Span;
            return span.Length >= 4
                && span[0] == 0x50
                && span[1] == 0x56
                && span[2] == 0x4D
                && span[3] == 0x00;
        }

        private static void ValidateScriptCompatibility(ContractType contractType, bool isRiscvBinary)
        {
            if (contractType == ContractType.RiscV && !isRiscvBinary)
                throw new FormatException("Manifest extra.vm declares RISC-V but the NEF script is not a PolkaVM binary.");

            if (contractType == ContractType.NeoVM && isRiscvBinary)
                throw new FormatException("Manifest extra.vm declares legacy NeoVM but the NEF script is a PolkaVM binary.");
        }
    }
}
