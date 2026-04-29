// Copyright (C) 2015-2026 The Neo Project.
//
// ApplicationEngine.LoadContractProfiling.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Neo.SmartContract
{
    public partial class ApplicationEngine
    {
        private static readonly ConcurrentDictionary<string, LoadContractProfileStat> loadContractProfileStats = new();
        private static int s_loadContractProfileHookRegistered;
        private const string LoadContractProfileEnvironmentVariable = "NEO_RISCV_PROFILE_ENGINE";
        private static readonly bool s_loadContractProfileEnabled =
            string.Equals(Environment.GetEnvironmentVariable(LoadContractProfileEnvironmentVariable), "1", StringComparison.Ordinal);

        private sealed class LoadContractProfileStat
        {
            public long Count;
            public long Ticks;
        }

        private static long BeginLoadContractProfilePhase()
        {
            if (!s_loadContractProfileEnabled)
                return 0;

            EnsureLoadContractProfileHook();
            return Stopwatch.GetTimestamp();
        }

        private static void RecordLoadContractProfilePhase(string phase, long startTicks)
        {
            if (!s_loadContractProfileEnabled || startTicks == 0)
                return;

            var stat = loadContractProfileStats.GetOrAdd(phase, _ => new LoadContractProfileStat());
            Interlocked.Increment(ref stat.Count);
            Interlocked.Add(ref stat.Ticks, Stopwatch.GetTimestamp() - startTicks);
        }

        private static void EnsureLoadContractProfileHook()
        {
            if (Interlocked.Exchange(ref s_loadContractProfileHookRegistered, 1) != 0)
                return;

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (!s_loadContractProfileEnabled || loadContractProfileStats.IsEmpty)
                    return;

                Console.Error.WriteLine("[neo-riscv-engine][profile] load_contract phases:");
                foreach (var entry in loadContractProfileStats.OrderByDescending(pair => pair.Value.Ticks))
                {
                    var count = Math.Max(1, entry.Value.Count);
                    var totalUs = entry.Value.Ticks * 1_000_000d / Stopwatch.Frequency;
                    Console.Error.WriteLine(
                        $"[neo-riscv-engine][profile] load_contract phase={entry.Key} count={entry.Value.Count} avg_us={totalUs / count:F3} total_us={totalUs:F3}");
                }
            };
        }
    }
}
