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

        var hostLibSource = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
        if (string.IsNullOrWhiteSpace(hostLibSource) || !File.Exists(hostLibSource))
            Assert.Inconclusive("NEO_RISCV_HOST_LIB is not set to a valid host library for the current workspace.");

        var previousHostLib = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var stagedPluginRoot = Path.Combine(AppContext.BaseDirectory, "Plugins", "Neo.Riscv.Adapter");
        var stagedHostLibPath = Path.Combine(stagedPluginRoot, GetPlatformFileName());
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pluginRoot = Path.Combine(tempRoot, "Plugins", "Neo.Riscv.Adapter");
        var hostLibPath = Path.Combine(pluginRoot, GetPlatformFileName());
        var expectedPath = File.Exists(stagedHostLibPath) ? stagedHostLibPath : hostLibPath;

        try
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", null);
            if (!File.Exists(stagedHostLibPath))
            {
                Directory.CreateDirectory(pluginRoot);
                File.Copy(hostLibSource, hostLibPath, overwrite: true);
                Environment.CurrentDirectory = tempRoot;
            }

            Assert.IsTrue(RiscvAdapterTestSupport.CanUseAdapter());
            Assert.AreEqual(expectedPath, RiscvAdapterTestSupport.ResolveProviderLibraryPath());
        }
        finally
        {
            Environment.CurrentDirectory = previousCurrentDirectory;
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", previousHostLib);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [TestMethod]
    public void CanUseAdapter_UsesBundledPluginLibrary_WhenHostEnvVarPointsToAnotherExistingFile()
    {
        var adapterDll = Path.Combine(AppContext.BaseDirectory, "Plugins", "Neo.Riscv.Adapter", "Neo.Riscv.Adapter.dll");
        if (!File.Exists(adapterDll))
            Assert.Inconclusive("Neo.Riscv.Adapter.dll is not staged in the test output Plugins directory.");

        var hostLibSource = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
        if (string.IsNullOrWhiteSpace(hostLibSource) || !File.Exists(hostLibSource))
            Assert.Inconclusive("NEO_RISCV_HOST_LIB is not set to a valid host library for the current workspace.");

        var previousHostLib = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var stagedPluginRoot = Path.Combine(AppContext.BaseDirectory, "Plugins", "Neo.Riscv.Adapter");
        var stagedHostLibPath = Path.Combine(stagedPluginRoot, GetPlatformFileName());
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var pluginRoot = Path.Combine(tempRoot, "Plugins", "Neo.Riscv.Adapter");
        var hostLibPath = Path.Combine(pluginRoot, GetPlatformFileName());
        var externalRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var externalPath = Path.Combine(externalRoot, GetPlatformFileName());
        var expectedPath = File.Exists(stagedHostLibPath) ? stagedHostLibPath : hostLibPath;

        try
        {
            Directory.CreateDirectory(externalRoot);
            File.Copy(hostLibSource, externalPath, overwrite: true);
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", externalPath);
            if (!File.Exists(stagedHostLibPath))
            {
                Directory.CreateDirectory(pluginRoot);
                File.Copy(hostLibSource, hostLibPath, overwrite: true);
                Environment.CurrentDirectory = tempRoot;
            }

            Assert.IsTrue(RiscvAdapterTestSupport.CanUseAdapter());
            Assert.AreEqual(expectedPath, RiscvAdapterTestSupport.ResolveProviderLibraryPath());
        }
        finally
        {
            Environment.CurrentDirectory = previousCurrentDirectory;
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", previousHostLib);
            if (File.Exists(externalPath))
                File.Delete(externalPath);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
            if (Directory.Exists(externalRoot))
                Directory.Delete(externalRoot, recursive: true);
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
