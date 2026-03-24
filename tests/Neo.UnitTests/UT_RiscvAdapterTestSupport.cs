// Copyright (C) 2015-2026 The Neo Project.
//
// UT_RiscvAdapterTestSupport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Neo.UnitTests;

[TestClass]
public class UT_RiscvAdapterTestSupport
{
    [TestMethod]
    public void CanUseAdapter_UsesResolvedPluginLibraryPath_WhenHostEnvVarUnset()
    {
        var adapterDll = Path.Combine(AppContext.BaseDirectory, "Plugins", "Neo.Riscv.Adapter", "Neo.Riscv.Adapter.dll");
        if (!File.Exists(adapterDll))
            Assert.Inconclusive("Neo.Riscv.Adapter.dll is not staged in the test output Plugins directory.");

        var previousHostLib = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
        var pluginRoot = Path.Combine(AppContext.BaseDirectory, "Plugins", "Neo.Riscv.Adapter");
        Directory.CreateDirectory(pluginRoot);
        var hostLibPath = Path.Combine(pluginRoot, GetPlatformFileName());

        try
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", null);
            File.WriteAllBytes(hostLibPath, []);

            Assert.IsTrue(RiscvAdapterTestSupport.CanUseAdapter());
            Assert.AreEqual(hostLibPath, RiscvAdapterTestSupport.ResolveProviderLibraryPath());
        }
        finally
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", previousHostLib);
            if (File.Exists(hostLibPath))
                File.Delete(hostLibPath);
        }
    }

    private static string GetPlatformFileName()
    {
        if (OperatingSystem.IsWindows())
            return "neo_riscv_host.dll";
        if (OperatingSystem.IsMacOS())
            return "libneo_riscv_host.dylib";
        return "libneo_riscv_host.so";
    }
}
