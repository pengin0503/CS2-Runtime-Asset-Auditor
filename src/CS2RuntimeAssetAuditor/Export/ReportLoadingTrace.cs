using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CS2RuntimeAssetAuditor.Core.Loading;

namespace CS2RuntimeAssetAuditor.Export
{
    [DataContract]
    public sealed class ReportLoadingTrace
    {
        [DataMember(Name = "modStartedAtUtc", Order = 1)] public string ModStartedAtUtc { get; set; }
        [DataMember(Name = "loadStartedAtUtc", Order = 2)] public string LoadStartedAtUtc { get; set; }
        [DataMember(Name = "loadCompletedAtUtc", Order = 3, EmitDefaultValue = false)] public string LoadCompletedAtUtc { get; set; }
        [DataMember(Name = "purpose", Order = 4)] public string Purpose { get; set; }
        [DataMember(Name = "milestones", Order = 5)] public List<ReportLoadingMilestone> Milestones { get; set; }
        [DataMember(Name = "memorySamples", Order = 6)] public List<ReportLoadingMemory> MemorySamples { get; set; }
        [DataMember(Name = "memoryPeak", Order = 7)] public ReportLoadingMemory MemoryPeak { get; set; }
        [DataMember(Name = "memoryEnd", Order = 8, EmitDefaultValue = false)] public ReportLoadingMemory MemoryEnd { get; set; }
        [DataMember(Name = "registeredAssetCount", Order = 9, EmitDefaultValue = false)] public int? RegisteredAssetCount { get; set; }
        [DataMember(Name = "loadedBytes", Order = 10, EmitDefaultValue = false)] public long? LoadedBytes { get; set; }
        [DataMember(Name = "cacheReadyAtEnd", Order = 11, EmitDefaultValue = false)] public bool? CacheReadyAtEnd { get; set; }
        [DataMember(Name = "cacheBecameReady", Order = 12)] public bool CacheBecameReady { get; set; }
        [DataMember(Name = "cacheRebuilt", Order = 13, EmitDefaultValue = false)] public bool? CacheRebuilt { get; set; }
        [DataMember(Name = "cacheFailed", Order = 14, EmitDefaultValue = false)] public bool? CacheFailed { get; set; }
        [DataMember(Name = "limitations", Order = 15)] public List<string> Limitations { get; set; }

        public static ReportLoadingTrace FromSnapshot(LoadingTraceSnapshot snapshot)
        {
            if (snapshot == null) return null;
            return new ReportLoadingTrace
            {
                ModStartedAtUtc = Stamp(snapshot.ModStartedAtUtc),
                LoadStartedAtUtc = Stamp(snapshot.StartedAtUtc),
                LoadCompletedAtUtc = snapshot.CompletedAtUtc.HasValue ? Stamp(snapshot.CompletedAtUtc.Value) : null,
                Purpose = snapshot.Purpose,
                Milestones = snapshot.Milestones.Select(x => new ReportLoadingMilestone { Name = x.Name, AtUtc = Stamp(x.AtUtc) }).ToList(),
                MemorySamples = snapshot.Samples.Select(ToReportSample).ToList(),
                MemoryPeak = new ReportLoadingMemory
                {
                    UnityAllocatedBytes = snapshot.PeakUnityAllocatedBytes,
                    ProcessWorkingSetBytes = snapshot.PeakProcessWorkingSetBytes,
                    GraphicsDriverAllocatedBytes = snapshot.PeakGraphicsDriverAllocatedBytes
                },
                MemoryEnd = snapshot.End == null ? null : ToReportSample(snapshot.End),
                RegisteredAssetCount = snapshot.End?.RegisteredAssetCount,
                CacheReadyAtEnd = snapshot.End?.CacheReady,
                CacheBecameReady = snapshot.CacheBecameReady,
                Limitations = new List<string>
                {
                    "Unity allocated memory is not total physical RAM; process working set is a separate RAM indicator.",
                    "Graphics driver allocation is not physical VRAM usage; VRAM usage is unavailable.",
                    "Registered asset count is an AssetDatabase catalog count, not a count of files read during this load.",
                    "Asset database observed is the first successful sample, not the start or end of database processing.",
                    "Loaded byte count and cache rebuild/failure events are unavailable without I/O or cache instrumentation.",
                    "Blocked Unity frames may leave gaps between loading memory samples.",
                    "Game loading complete is the game callback, not proof that every simulation or UI job is finished."
                }
            };
        }

        private static ReportLoadingMemory ToReportSample(LoadingMemorySample sample) => new ReportLoadingMemory
        {
            AtUtc = Stamp(sample.AtUtc),
            UnityAllocatedBytes = sample.UnityAllocatedBytes,
            ProcessWorkingSetBytes = sample.ProcessWorkingSetBytes,
            GraphicsDriverAllocatedBytes = sample.GraphicsDriverAllocatedBytes,
            RegisteredAssetCount = sample.RegisteredAssetCount,
            CacheReady = sample.CacheReady
        };
        private static string Stamp(DateTimeOffset value) => value == default(DateTimeOffset) ? null : value.ToUniversalTime().ToString("O");
    }

    [DataContract]
    public sealed class ReportLoadingMilestone
    {
        [DataMember(Name = "name", Order = 1)] public string Name { get; set; }
        [DataMember(Name = "atUtc", Order = 2)] public string AtUtc { get; set; }
    }

    [DataContract]
    public sealed class ReportLoadingMemory
    {
        [DataMember(Name = "atUtc", Order = 1, EmitDefaultValue = false)] public string AtUtc { get; set; }
        [DataMember(Name = "unityAllocatedBytes", Order = 2, EmitDefaultValue = false)] public long? UnityAllocatedBytes { get; set; }
        [DataMember(Name = "processWorkingSetBytes", Order = 3, EmitDefaultValue = false)] public long? ProcessWorkingSetBytes { get; set; }
        [DataMember(Name = "graphicsDriverAllocatedBytes", Order = 4, EmitDefaultValue = false)] public long? GraphicsDriverAllocatedBytes { get; set; }
        [DataMember(Name = "vramUsedBytes", Order = 5, EmitDefaultValue = false)] public long? VramUsedBytes { get; set; }
        [DataMember(Name = "registeredAssetCount", Order = 6, EmitDefaultValue = false)] public int? RegisteredAssetCount { get; set; }
        [DataMember(Name = "cacheReady", Order = 7, EmitDefaultValue = false)] public bool? CacheReady { get; set; }
    }
}
