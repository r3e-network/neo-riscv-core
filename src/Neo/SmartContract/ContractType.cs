// Copyright (C) 2015-2026 The Neo Project.
//
// ContractType.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.

namespace Neo.SmartContract
{
    /// <summary>
    /// Represents the execution backend for a deployed contract.
    /// </summary>
    public enum ContractType : byte
    {
        /// <summary>
        /// NeoVM bytecode executed through the NeoVM interpreter on PolkaVM.
        /// This is the default for all existing contracts.
        /// </summary>
        NeoVM = 0,

        /// <summary>
        /// PolkaVM RISC-V binary executed directly on PolkaVM.
        /// The NEF script starts with the PVM\0 magic bytes.
        /// </summary>
        RiscV = 1,
    }
}
