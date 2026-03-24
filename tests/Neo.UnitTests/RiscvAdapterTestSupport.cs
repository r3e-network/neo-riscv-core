// Copyright (C) 2015-2026 The Neo Project.
//
// RiscvAdapterTestSupport.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

#nullable enable

namespace Neo.UnitTests;

internal static class RiscvAdapterTestSupport
{
    private const string AdapterAssemblyName = "Neo.Riscv.Adapter";
    private const string AdapterAssemblyFileName = AdapterAssemblyName + ".dll";
    private const string AdapterEnvVar = "NEO_RISCV_ADAPTER_DLL";
    private const string HostLibEnvVar = "NEO_RISCV_HOST_LIB";
    private const string ProviderTypeName = "Neo.SmartContract.RiscV.RiscvApplicationEngineProvider";
    private const string BridgeTypeName = "Neo.SmartContract.RiscV.NativeRiscvVmBridge";
    private const string EngineTypeName = "Neo.SmartContract.RiscV.RiscvApplicationEngine";
    private const string ResolverTypeName = "Neo.SmartContract.RiscV.RiscvApplicationEngineProviderResolver";

    internal static bool CanUseAdapter()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HostLibEnvVar)))
            return false;

        return TryLoadAdapterAssembly() is not null;
    }

    internal static string AdapterUnavailableReason()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HostLibEnvVar)))
            return $"{HostLibEnvVar} is not set.";

        return "Neo.Riscv.Adapter assembly is not available from the configured plugin locations.";
    }

    internal static bool IsRiscvEngine(ApplicationEngine engine)
    {
        return string.Equals(engine.GetType().FullName, EngineTypeName, StringComparison.Ordinal);
    }

    internal static IApplicationEngineProvider CreateProvider(string libraryPath)
    {
        var assembly = TryLoadAdapterAssembly()
            ?? throw new InvalidOperationException("Neo.Riscv.Adapter assembly could not be loaded.");

        var bridgeType = assembly.GetType(BridgeTypeName, throwOnError: true)!;
        var providerType = assembly.GetType(ProviderTypeName, throwOnError: true)!;
        var bridge = Activator.CreateInstance(bridgeType, libraryPath)
            ?? throw new InvalidOperationException($"Failed to create {BridgeTypeName}.");
        return (IApplicationEngineProvider)(Activator.CreateInstance(providerType, bridge)
            ?? throw new InvalidOperationException($"Failed to create {ProviderTypeName}."));
    }

    internal static IApplicationEngineProvider ResolveProvider()
    {
        var assembly = TryLoadAdapterAssembly()
            ?? throw new InvalidOperationException("Neo.Riscv.Adapter assembly could not be loaded.");
        var resolverType = assembly.GetType(ResolverTypeName, throwOnError: true)!;
        var resolveMethod = resolverType.GetMethod(
            "ResolveRequiredProvider",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredProvider was not found.");

        return (IApplicationEngineProvider)(resolveMethod.Invoke(null, null)
            ?? throw new InvalidOperationException("ResolveRequiredProvider returned null."));
    }

    internal static void ResetProviderForTesting()
    {
        var assembly = TryLoadAdapterAssembly();
        if (assembly is null)
            return;

        var resolverType = assembly.GetType(ResolverTypeName, throwOnError: false);
        var resetMethod = resolverType?.GetMethod(
            "ResetForTesting",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        resetMethod?.Invoke(null, null);
    }

    private static Assembly? TryLoadAdapterAssembly()
    {
        var alreadyLoaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, AdapterAssemblyName, StringComparison.Ordinal));
        if (alreadyLoaded is not null)
            return alreadyLoaded;

        var candidate = ResolveAdapterAssemblyPath();
        if (candidate is null)
            return null;

        return Assembly.LoadFrom(candidate);
    }

    private static string? ResolveAdapterAssemblyPath()
    {
        var configured = Environment.GetEnvironmentVariable(AdapterEnvVar);
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Plugins", AdapterAssemblyName, AdapterAssemblyFileName),
            Path.Combine(Environment.CurrentDirectory, "Plugins", AdapterAssemblyName, AdapterAssemblyFileName),
            Path.Combine(AppContext.BaseDirectory, AdapterAssemblyFileName),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
