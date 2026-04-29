// Copyright (C) 2015-2026 The Neo Project.
//
// NeoVMHostApplicationEngineProvider.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;

namespace Neo.SmartContract
{
    /// <summary>
    /// A pure-managed <see cref="IApplicationEngineProvider"/> that constructs the
    /// host-side NeoVM <see cref="ApplicationEngine"/> directly - no native
    /// library, no RISC-V guest, no plugin dependency.
    ///
    /// Intended only for reference tooling and tests that compare RiscvVM
    /// compatibility behavior against the managed NeoVM implementation.
    /// Production execution must use the RISC-V adapter provider.
    /// </summary>
    public sealed class NeoVMHostApplicationEngineProvider : IApplicationEngineProvider
    {
        public ApplicationEngine Create(
            TriggerType trigger,
            IVerifiable? container,
            DataCache snapshot,
            Block? persistingBlock,
            ProtocolSettings settings,
            long gas,
            IDiagnostic? diagnostic,
            JumpTable jumpTable)
        {
            return new NeoVMHostApplicationEngine(trigger, container, snapshot, persistingBlock, settings, gas, diagnostic, jumpTable);
        }

        private sealed class NeoVMHostApplicationEngine : ApplicationEngine
        {
            internal NeoVMHostApplicationEngine(
                TriggerType trigger,
                IVerifiable? container,
                DataCache snapshotCache,
                Block? persistingBlock,
                ProtocolSettings settings,
                long gas,
                IDiagnostic? diagnostic,
                JumpTable? jumpTable)
                : base(trigger, container, snapshotCache, persistingBlock, settings, gas, diagnostic, jumpTable)
            {
            }
        }
    }
}
