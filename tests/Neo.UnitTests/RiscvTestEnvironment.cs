#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

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
        var stagedAdapterDll = Path.Combine(baseDirectory, "Plugins", "Neo.Riscv.Adapter", "Neo.Riscv.Adapter.dll");
        var stagedHostLib = Path.Combine(baseDirectory, "Plugins", "Neo.Riscv.Adapter", GetPlatformFileName());
        if (File.Exists(stagedAdapterDll) && File.Exists(stagedHostLib))
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_ADAPTER_DLL", stagedAdapterDll);
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", stagedHostLib);
            return;
        }

        var adapterDll = ResolveSiblingFile(baseDirectory, "neo-riscv-vm", "dotnet", "Neo.Riscv.Adapter", "bin", "Debug", "net10.0", "Neo.Riscv.Adapter.dll")
            ?? ResolveSiblingFile(baseDirectory, "neo-riscv-vm", "dotnet", "Neo.Riscv.Adapter", "bin", "Release", "net10.0", "Neo.Riscv.Adapter.dll");
        if (File.Exists(adapterDll))
            Environment.SetEnvironmentVariable("NEO_RISCV_ADAPTER_DLL", adapterDll);

        var platformFileName = GetPlatformFileName();
        var release = ResolveSiblingFile(baseDirectory, "neo-riscv-vm", "target", "release", platformFileName);
        if (File.Exists(release))
        {
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", release);
            return;
        }

        var debug = ResolveSiblingFile(baseDirectory, "neo-riscv-vm", "target", "debug", platformFileName);
        if (File.Exists(debug))
            Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", debug);
    }

    [AssemblyCleanup]
    public static void Cleanup()
    {
        Environment.SetEnvironmentVariable("NEO_RISCV_ADAPTER_DLL", _previousAdapterAssemblyPath);
        Environment.SetEnvironmentVariable("NEO_RISCV_HOST_LIB", _previousHostLibraryPath);
    }

    private static string GetPlatformFileName()
    {
        if (OperatingSystem.IsWindows())
            return "neo_riscv_host.dll";
        if (OperatingSystem.IsMacOS())
            return "libneo_riscv_host.dylib";
        return "libneo_riscv_host.so";
    }

    private static string? ResolveSiblingFile(string start, params string[] segments)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return null;
    }
}
