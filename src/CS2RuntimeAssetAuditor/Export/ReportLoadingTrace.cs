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
        [DataMember(Name = "loadInterruptedAtUtc", Order = 4, EmitDefaultValue = false)] public string LoadInterruptedAtUtc { get; set; }
        /// <summary>inProgress, completed or interrupted.</summary>
        [DataMember(Name = "outcome", Order = 5)] public string Outcome { get; set; }
        [DataMember(Name = "interruptionReason", Order = 6, EmitDefaultValue = false)] public string InterruptionReason { get; set; }
        [DataMember(Name = "purpose", Order = 7)] public string Purpose { get; set; }
        [DataMember(Name = "milestones", Order = 8)] public List<ReportLoadingMilestone> Milestones { get; set; }
        [DataMember(Name = "observationCount", Order = 9)] public long ObservationCount { get; set; }
        [DataMember(Name = "memorySamples", Order = 10)] public List<ReportLoadingMemory> MemorySamples { get; set; }
        [DataMember(Name = "memoryPeak", Order = 11)] public ReportLoadingMemoryPeak MemoryPeak { get; set; }
        [DataMember(Name = "memoryEnd", Order = 12, EmitDefaultValue = false)] public ReportLoadingMemory MemoryEnd { get; set; }
        [DataMember(Name = "registeredAssetCount", Order = 13, EmitDefaultValue = false)] public int? RegisteredAssetCount { get; set; }
        [DataMember(Name = "anyDatabaseCachedAtEnd", Order = 14, EmitDefaultValue = false)] public bool? AnyDatabaseCachedAtEnd { get; set; }
        [DataMember(Name = "observedUncachedToCachedTransition", Order = 15)] public bool ObservedUncachedToCachedTransition { get; set; }
        [DataMember(Name = "limitations", Order = 16, EmitDefaultValue = false)] public List<string> Limitations { get; set; }
        /// <summary>The interrupted load directly before this one; its limitations are those of the enclosing trace.</summary>
        [DataMember(Name = "previousInterruptedLoad", Order = 17, EmitDefaultValue = false)] public ReportLoadingTrace PreviousInterruptedLoad { get; set; }

        public static ReportLoadingTrace FromSnapshot(LoadingTraceSnapshot snapshot)
        {
            var report = FromSnapshotWithoutLimitations(snapshot);
            if (report == null) return null;
            report.Limitations = BuildLimitations();
            report.PreviousInterruptedLoad = FromSnapshotWithoutLimitations(snapshot.PreviousInterrupted);
            return report;
        }

        private static ReportLoadingTrace FromSnapshotWithoutLimitations(LoadingTraceSnapshot snapshot)
        {
            if (snapshot == null) return null;
            return new ReportLoadingTrace
            {
                ModStartedAtUtc = Stamp(snapshot.ModStartedAtUtc),
                LoadStartedAtUtc = Stamp(snapshot.StartedAtUtc),
                LoadCompletedAtUtc = Stamp(snapshot.CompletedAtUtc),
                LoadInterruptedAtUtc = Stamp(snapshot.InterruptedAtUtc),
                Outcome = OutcomeName(snapshot.Outcome),
                InterruptionReason = snapshot.InterruptionReason,
                Purpose = snapshot.Purpose,
                Milestones = snapshot.Milestones.Select(x => new ReportLoadingMilestone { Name = x.Name, AtUtc = Stamp(x.AtUtc) }).ToList(),
                ObservationCount = snapshot.ObservationCount,
                MemorySamples = snapshot.Samples.Select(ToReportSample).ToList(),
                MemoryPeak = new ReportLoadingMemoryPeak
                {
                    UnityAllocatedBytes = snapshot.PeakUnityAllocated.Bytes,
                    UnityAllocatedAtUtc = Stamp(snapshot.PeakUnityAllocated.AtUtc),
                    ProcessWorkingSetBytes = snapshot.PeakProcessWorkingSet.Bytes,
                    ProcessWorkingSetAtUtc = Stamp(snapshot.PeakProcessWorkingSet.AtUtc),
                    GraphicsDriverAllocatedBytes = snapshot.PeakGraphicsDriverAllocated.Bytes,
                    GraphicsDriverAllocatedAtUtc = Stamp(snapshot.PeakGraphicsDriverAllocated.AtUtc)
                },
                MemoryEnd = snapshot.End == null ? null : ToReportSample(snapshot.End),
                RegisteredAssetCount = snapshot.End?.RegisteredAssetCount,
                AnyDatabaseCachedAtEnd = snapshot.End?.AnyDatabaseCached,
                ObservedUncachedToCachedTransition = snapshot.ObservedUncachedToCachedTransition
            };
        }

        private static List<string> BuildLimitations() => new List<string>
        {
            "Unity allocated memory is not total physical RAM; process working set is a separate RAM indicator.",
            "Graphics driver allocation is not physical VRAM usage; VRAM usage is unavailable.",
            "Unity reports graphics driver allocation only in development players and the editor; release players such as the shipped game report 0, which is exported as absent rather than as zero bytes.",
            "Registered asset count is an AssetDatabase catalog count, not a count of files read during this load.",
            "The assetRegistrationChanged and assetCacheStateChanged milestones mark the first sample that differed from the first observation at load start; the change happened after the previous sample. Their absence means no sampled change, not proof that nothing changed.",
            "The aggregate cache flag is true when at least one registered database is cached; it is not an all-ready state.",
            "A false observedUncachedToCachedTransition means no sampled transition was seen, not proof that no cache changed.",
            "Loaded byte count and cache rebuild/failure events are unavailable without I/O or cache instrumentation.",
            "Blocked Unity frames may leave gaps between loading memory samples.",
            "Memory peaks are peaks of the two-second samples; shorter spikes can be missed. A peak time is the sample time, not the exact moment of the peak.",
            "An interrupted load is a gameplay load that ended without the game reporting its completion, because another load started or loading finished outside gameplay; the trace does not record why the load failed.",
            "Game loading complete is the game callback, not proof that every simulation or UI job is finished."
        };

        private static string OutcomeName(LoadingTraceOutcome outcome)
        {
            switch (outcome)
            {
                case LoadingTraceOutcome.Completed: return "completed";
                case LoadingTraceOutcome.Interrupted: return "interrupted";
                default: return "inProgress";
            }
        }

        private static ReportLoadingMemory ToReportSample(LoadingMemorySample sample) => new ReportLoadingMemory
        {
            AtUtc = Stamp(sample.AtUtc),
            UnityAllocatedBytes = sample.UnityAllocatedBytes,
            ProcessWorkingSetBytes = sample.ProcessWorkingSetBytes,
            GraphicsDriverAllocatedBytes = sample.GraphicsDriverAllocatedBytes,
            RegisteredAssetCount = sample.RegisteredAssetCount,
            AnyDatabaseCached = sample.AnyDatabaseCached
        };
        private static string Stamp(DateTimeOffset? value) => value.HasValue ? Stamp(value.Value) : null;
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
        [DataMember(Name = "registeredAssetCount", Order = 5, EmitDefaultValue = false)] public int? RegisteredAssetCount { get; set; }
        [DataMember(Name = "anyDatabaseCached", Order = 6, EmitDefaultValue = false)] public bool? AnyDatabaseCached { get; set; }
    }

    /// <summary>Each metric's largest sampled value and the sample time it was first reached.</summary>
    [DataContract]
    public sealed class ReportLoadingMemoryPeak
    {
        [DataMember(Name = "unityAllocatedBytes", Order = 1, EmitDefaultValue = false)] public long? UnityAllocatedBytes { get; set; }
        [DataMember(Name = "unityAllocatedAtUtc", Order = 2, EmitDefaultValue = false)] public string UnityAllocatedAtUtc { get; set; }
        [DataMember(Name = "processWorkingSetBytes", Order = 3, EmitDefaultValue = false)] public long? ProcessWorkingSetBytes { get; set; }
        [DataMember(Name = "processWorkingSetAtUtc", Order = 4, EmitDefaultValue = false)] public string ProcessWorkingSetAtUtc { get; set; }
        [DataMember(Name = "graphicsDriverAllocatedBytes", Order = 5, EmitDefaultValue = false)] public long? GraphicsDriverAllocatedBytes { get; set; }
        [DataMember(Name = "graphicsDriverAllocatedAtUtc", Order = 6, EmitDefaultValue = false)] public string GraphicsDriverAllocatedAtUtc { get; set; }
    }
}
