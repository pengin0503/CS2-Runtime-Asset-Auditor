using System;
using System.Diagnostics;
using Colossal.IO.AssetDatabase;
using Game;
using UnityEngine.Profiling;

namespace CS2RuntimeAssetAuditor.Lifecycle
{
    /// <summary>Fixed-cadence loading observations; no recorder discovery, asset scan or per-file hooks.</summary>
    public sealed partial class LoadingMetricsSystem : GameSystemBase
    {
        private readonly Stopwatch _clock = new Stopwatch();
        private Process _process;

        protected override void OnCreate()
        {
            base.OnCreate();
            try { _process = Process.GetCurrentProcess(); }
            catch (Exception) { _process = null; }
        }

        public void Begin()
        {
            _clock.Restart();
            Mod.LoadingTrace.ShouldSample(0d);
            CaptureNow();
        }

        protected override void OnUpdate()
        {
            if (Mod.LoadingTrace.ShouldSample(_clock.Elapsed.TotalSeconds))
                CaptureNow();
        }

        public void CaptureNow()
        {
            if (!Mod.LoadingTrace.IsLoading) return;
            long? unity = null, ram = null, graphicsDriver = null;
            int? assetCount = null;
            bool? cacheReady = null;
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
                    cacheReady = database.isCached;
                }
            }
            catch (Exception) { }
            Mod.LoadingTrace.Observe(DateTimeOffset.UtcNow, unity, ram, graphicsDriver, assetCount, cacheReady);
        }

        protected override void OnDestroy()
        {
            _clock.Stop();
            _process?.Dispose();
            _process = null;
            base.OnDestroy();
        }
    }
}
