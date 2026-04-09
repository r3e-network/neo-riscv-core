#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Neo.UnitTests;

[TestClass]
public static class RiscvTestEnvironment
{
    private static string? _previousAdapterAssemblyPath;
    private static string? _previousHostLibraryPath;

    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        _previousAdapterAssemblyPath = Environment.GetEnvironmentVariable("NEO_RISCV_ADAPTER_DLL");
        _previousHostLibraryPath = Environment.GetEnvironmentVariable("NEO_RISCV_HOST_LIB");

        var baseDirectory = AppContext.BaseDirectory;
        var adapterDll = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "neo-riscv-vm", "compat", "Neo.Riscv.Adapter", "bin", "Debug", "net10.0", "Neo.Riscv.Adapter.dll"));
        if (File.Exists(adapterDll))
            Environment.SetEnvironmentVariable("NEO_RISCV_ADAPTER_DLL", adapterDll);

        var release = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "neo-riscv-vm", "target", "release", "libneo_riscv_host.so"));
        if (File.Exists(release))
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", release);
            return;
        }

        var debug = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "neo-riscv-vm", "target", "debug", "libneo_riscv_host.so"));
        if (File.Exists(debug))
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", debug);
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        Environment.SetEnvironmentVariable("NEO_RISCV_ADAPTER_DLL", _previousAdapterAssemblyPath);
        Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", _previousHostLibraryPath);
    }
}
