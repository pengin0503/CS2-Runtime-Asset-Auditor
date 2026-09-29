using System;
using System.Diagnostics;
using Colossal.IO.AssetDatabase;
using UnityEngine;
using UnityEngine.Profiling;

namespace CS2RuntimeAssetAuditor.Lifecycle
{
    /// <summary>
    /// Uses Unity's frame loop while the game World is suspended for loading.
    /// Inactive loads cost one boolean check per frame; observations run at most every two seconds.
    /// </summary>
    public sealed class LoadingMetricsBehaviour : MonoBehaviour
    {
        private readonly Stopwatch _clock = new Stopwatch();
        private Process _process;

        public static LoadingMetricsBehaviour Install()
        {
            var host = new GameObject("CS2RuntimeAssetAuditor.LoadingMetrics");
            host.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(host);
            return host.AddComponent<LoadingMetricsBehaviour>();
        }

        private void Awake()
        {
            try { _process = Process.GetCurrentProcess(); }
            catch (Exception) { _process = null; }
        }

        public void Begin()
        {
            _clock.Restart();
            Mod.LoadingTrace.ShouldSample(0d);
            CaptureNow();
        }

        private void Update()
        {
            if (!Mod.LoadingTrace.IsLoading) return;
            if (Mod.LoadingTrace.ShouldSample(_clock.Elapsed.TotalSeconds))
                CaptureNow();
        }

        public void CaptureNow()
        {
            if (!Mod.LoadingTrace.IsLoading) return;
            long? unity = null, ram = null, graphicsDriver = null;
            int? assetCount = null;
            bool? anyDatabaseCached = null;
            try { unity = Profiler.GetTotalAllocatedMemoryLong(); } catch (Exception) { }
            try
            {
                _process?.Refresh();
                ram = _process?.WorkingSet64;
            }
            catch (Exception) { }
            try { graphicsDriver = Profiler.GetAllocatedMemoryForGraphicsDriver(); } catch (Exception) { }
            try
            {
                var database = AssetDatabase.global;
                if (database != null)
                {
                    assetCount = database.count;
                    anyDatabaseCached = database.isCached;
                }
            }
            catch (Exception) { }
            Mod.LoadingTrace.Observe(DateTimeOffset.UtcNow, unity, ram, graphicsDriver, assetCount, anyDatabaseCached);
        }

        private void OnDestroy()
        {
            _clock.Stop();
            _process?.Dispose();
            _process = null;
        }
    }
}
