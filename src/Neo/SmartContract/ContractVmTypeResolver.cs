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
    /// <summary>
    /// Resolves the <see cref="ContractType"/> (NeoVM vs. RISC-V) for a deployed
    /// contract from its manifest marker and script payload.
    /// </summary>
    /// <remarks>
    /// <para><b>Role in the neovm→riscvvm replacement:</b> Before a contract
    /// can be executed, the runtime must decide which backend will run it.
    /// This resolver is that decision's authoritative source: it inspects the
    /// manifest's <c>extra.vm</c> field (set by the compiler/devpack at deploy
    /// time) and cross-checks it against the actual script bytes so a
    /// mislabeled contract fails loudly instead of silently diverging the state
    /// root under the wrong engine.</para>
    /// <para><b>Resolution order:</b></para>
    /// <list type="number">
    /// <item><description>If <c>extra.vm</c> is present, match it against
    /// <see cref="LegacyNeoVmMarker"/> (<c>legacy-neovm-v1</c>) or
    /// <see cref="RiscvPolkaVmMarker"/> (<c>riscv32-polkavm-v1</c>), then
    /// validate the script is consistent (see
    /// <see cref="ValidateScriptCompatibility"/>).</description></item>
    /// <item><description>If <c>extra.vm</c> is absent, default to
    /// <see cref="ContractType.NeoVM"/> (backward compatibility: pre-RISC-V
    /// contracts have no marker and must keep running on NeoVM).</description></item>
    /// </list>
    /// <para><b>PolkaVM magic:</b> A RISC-V contract's NEF script is a PolkaVM
    /// binary, identifiable by the <c>"PVM\0"</c> (0x50 0x56 0x4D 0x00) magic
    /// header (see <see cref="IsRiscVBinary"/>).</para>
    /// </remarks>
    internal static class ContractVmTypeResolver
    {
        /// <summary>
        /// Manifest <c>extra.vm</c> marker value identifying a legacy NeoVM
        /// contract (<c>"legacy-neovm-v1"</c>).
        /// </summary>
        internal const string LegacyNeoVmMarker = "legacy-neovm-v1";

        /// <summary>
        /// Manifest <c>extra.vm</c> marker value identifying a RISC-V (PolkaVM)
        /// contract (<c>"riscv32-polkavm-v1"</c>).
        /// </summary>
        internal const string RiscvPolkaVmMarker = "riscv32-polkavm-v1";

        /// <summary>
        /// Resolve the <see cref="ContractType"/> for a contract from its
        /// manifest and script payload.
        /// </summary>
        /// <param name="manifest">The contract manifest (its <c>Extra</c> field
        /// may carry a <c>vm</c> marker).</param>
        /// <param name="script">The contract's raw NEF script bytes (used to
        /// detect the PolkaVM magic header for consistency validation).</param>
        /// <returns>The resolved <see cref="ContractType"/> (defaults to
        /// <see cref="ContractType.NeoVM"/> when no marker is present).</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="manifest"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when the manifest marker and script payload are inconsistent.
        /// </exception>
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

        /// <summary>
        /// Try to read the <see cref="ContractType"/> from the manifest's
        /// <c>extra.vm</c> marker, if present.
        /// </summary>
        /// <param name="extra">The manifest <c>Extra</c> JSON object (may be null).</param>
        /// <param name="contractType">When this method returns <see langword="true"/>,
        /// holds the resolved type; otherwise holds <see cref="ContractType.NeoVM"/>.</param>
        /// <returns><see langword="true"/> if a recognized <c>vm</c> marker was found;
        /// <see langword="false"/> if the marker is absent or unrecognized.</returns>
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

            // A present-but-unrecognized vm marker is treated as opaque custom extra
            // data, NOT a backend marker: fall back to NeoVM for 100% backward
            // compatibility with mainnet, which has no such resolver and runs every
            // contract on NeoVM regardless of extra.vm. Throwing here would diverge the
            // state root for any pre-existing contract that carries a non-marker
            // extra.vm value. Deploy-time validation (ContractManagement.ValidateContractPayload)
            // still rejects a PolkaVM binary that lacks the RISC-V marker, so a genuinely
            // mislabeled contract cannot be deployed in the first place.
            return false;
        }

        /// <summary>
        /// Detect whether a script payload is a PolkaVM binary by checking for
        /// the <c>"PVM\0"</c> (0x50 0x56 0x4D 0x00) magic header.
        /// </summary>
        /// <param name="script">The script bytes to inspect.</param>
        /// <returns><see langword="true"/> if the payload starts with the PolkaVM magic.</returns>
        internal static bool IsRiscVBinary(ReadOnlyMemory<byte> script)
        {
            var span = script.Span;
            return span.Length >= 4
                && span[0] == 0x50
                && span[1] == 0x56
                && span[2] == 0x4D
                && span[3] == 0x00;
        }

        /// <summary>
        /// Validate that the manifest-declared <see cref="ContractType"/> is
        /// consistent with the script payload's actual format.
        /// </summary>
        /// <param name="contractType">The manifest-declared backend.</param>
        /// <param name="isRiscvBinary">Whether the script has the PolkaVM magic header.</param>
        /// <exception cref="FormatException">
        /// Thrown when the declared type and payload disagree (e.g. a RISC-V
        /// marker on a NeoVM script, or vice versa).
        /// </exception>
        private static void ValidateScriptCompatibility(ContractType contractType, bool isRiscvBinary)
        {
            if (contractType == ContractType.RiscV && !isRiscvBinary)
                throw new FormatException("Manifest extra.vm declares RISC-V but the NEF script is not a PolkaVM binary.");

            if (contractType == ContractType.NeoVM && isRiscvBinary)
                throw new FormatException("Manifest extra.vm declares legacy NeoVM but the NEF script is a PolkaVM binary.");
        }
    }
}
