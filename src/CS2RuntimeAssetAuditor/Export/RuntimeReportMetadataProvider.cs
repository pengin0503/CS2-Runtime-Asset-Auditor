using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using UnityEngine;

namespace CS2RuntimeAssetAuditor.Export
{
    internal static class RuntimeReportMetadataProvider
    {
        public static RuntimeReportMetadata Capture()
        {
            ReadMods(out var enabled, out var failed);
            return new RuntimeReportMetadata
            {
                BuildId = BuildIdentityProvider.Current,
                HardwareSummary = BuildHardwareSummary(),
                GpuMemoryCapacityMegabytes = ReadGpuMemoryCapacity(),
                EnabledMods = enabled,
                ModLoadFailures = failed,
                GameBenchmark = ReadGameBenchmark()
            };
        }

        // Game.Settings.BenchmarkSettings (1.6.2f1): BenchmarkUISystem stores the last completed run with the UTC
        // ticks of its save. A cancelled run is not stored. Only the summary is copied, not the per-frame series.
        private static ReportGameBenchmark ReadGameBenchmark()
        {
            try
            {
                var settings = SharedSettings.instance?.benchmark;
                var data = settings?.latestResult;
                if (data == null)
                    return null;
                var report = ReportGameBenchmark.Create(data.hasData, data.framesRendered, settings.lastUpdatedTicks);
                if (report == null)
                    return null;
                report.GameVersion = ReportGameBenchmark.Text(data.gameVersion);
                report.GraphicsQuality = ReportGameBenchmark.Text(data.graphicsQuality);
                report.ScreenResolution = ReportGameBenchmark.Text(data.screenResolution);
                report.CpuModel = ReportGameBenchmark.Text(data.cpuModel);
                report.GpuModel = ReportGameBenchmark.Text(data.gpuModel);
                report.GraphicsDeviceVersion = ReportGameBenchmark.Text(data.videoDriverVersion);
                report.SystemMemoryMb = ReportGameBenchmark.Positive(data.systemMemoryMb);
                report.VideoMemoryMb = ReportGameBenchmark.Positive(data.videoMemoryMb);
                report.SimulationTicks = data.simulationTicks;
                report.AverageFps = data.averageFps;
                report.MapLoadingSeconds = data.loadingTimeSecs > 0 ? data.loadingTimeSecs : (double?)null;
                report.CpuFrameMs = Stats(data.cpuMs);
                report.CpuGameMs = Stats(data.cpuGameMs);
                report.CpuRenderMs = Stats(data.cpuRenderMs);
                report.GpuMs = Stats(data.gpuMs);
                // Without GPU timings every GPU sample is 0 and no frame can count as GPU bound.
                report.GpuBoundPercent = report.GpuMs != null ? data.gpuBoundPercent : (double?)null;
                return report;
            }
            catch
            {
                return null;
            }
        }

        private static ReportBenchmarkStats Stats(BenchmarkMetricStats stats)
            => stats == null ? null : ReportBenchmarkStats.From(stats.min, stats.average, stats.p95, stats.max);

        private static int? ReadGpuMemoryCapacity()
        {
            try { return SystemInfo.graphicsMemorySize > 0 ? SystemInfo.graphicsMemorySize : (int?)null; }
            catch { return null; }
        }

        private static string BuildHardwareSummary()
        {
            try
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(SystemInfo.processorType))
                    parts.Add($"CPU={SystemInfo.processorType} ({SystemInfo.processorCount} logical)");
                if (SystemInfo.systemMemorySize > 0)
                    parts.Add($"RAM={SystemInfo.systemMemorySize} MB");
                if (!string.IsNullOrWhiteSpace(SystemInfo.graphicsDeviceName))
                    parts.Add($"GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB, {SystemInfo.graphicsDeviceType})");
                if (!string.IsNullOrWhiteSpace(SystemInfo.operatingSystem))
                    parts.Add($"OS={SystemInfo.operatingSystem}");
                return string.Join("; ", parts);
            }
            catch
            {
                return null;
            }
        }

        // The game's public ModManager API (1.6.2f1): ListModsEnabled() gives the loaded code mods and the UI
        // modules, and each ModInfo gives a code mod's load state. The loaded-IMod scan below is kept only for a
        // game build where ModManager cannot be read.
        private static void ReadMods(out IReadOnlyList<string> enabled, out IReadOnlyList<string> failed)
        {
            enabled = Array.Empty<string>();
            failed = Array.Empty<string>();
            try
            {
                var modManager = GameManager.instance?.modManager;
                if (modManager != null)
                {
                    var mods = new List<ModLoadInfo>();
                    foreach (var info in modManager)
                    {
                        if (info == null) continue;
                        try { mods.Add(new ModLoadInfo(info.name, info.isLoaded, info.state.ToString())); }
                        catch { /* An entry whose asset cannot be read is left out, not guessed. */ }
                    }
                    enabled = EnabledModNames.Enabled(modManager.ListModsEnabled(), mods);
                    failed = EnabledModNames.Failed(mods);
                    return;
                }
            }
            catch
            {
                // Continue into the loaded-code-mod fallback below.
            }

            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectLoadedCodeMods(names);
            enabled = names.ToArray();
        }

        private static void CollectLoadedCodeMods(ISet<string> names)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == null || assembly.IsDynamic)
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                if (!types.Any(type => type != null
                    && type != typeof(IMod)
                    && !type.IsAbstract
                    && typeof(IMod).IsAssignableFrom(type)))
                {
                    continue;
                }

                var assemblyName = assembly.GetName().Name;
                if (EnabledModNames.IsSafeLabel(assemblyName))
                    names.Add(assemblyName);
            }
        }
    }
}
