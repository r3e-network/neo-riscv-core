// Copyright (C) 2015-2026 The Neo Project.
//
// IRiscvVmBridge.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.

namespace Neo.SmartContract.RiscV
{
    /// <summary>
    /// Interface for the RISC-V VM bridge that executes contracts on PolkaVM.
    /// Supports two execution paths:
    /// - NeoVM contracts (ContractType.NeoVM): Script is NeoVM bytecode, interpreted
    ///   by the NeoVM interpreter running as a PolkaVM guest binary.
    /// - RISC-V contracts (ContractType.RiscV): Script is a PolkaVM binary (PVM\0 magic),
    ///   executed directly by PolkaVM without an interpreter layer.
    /// Both paths share the same host callback for SYSCALL/CALLT interop.
    /// </summary>
    public interface IRiscvVmBridge
    {
        /// <summary>
        /// Executes a contract through the PolkaVM runtime.
        /// The request contains the contract script(s) and execution context.
        /// PolkaVM auto-detects whether the script is a NeoVM bytecode blob
        /// (processed by the interpreter guest) or a native RISC-V binary
        /// (executed directly).
        /// </summary>
        RiscvExecutionResult Execute(RiscvExecutionRequest request);
    }
}
