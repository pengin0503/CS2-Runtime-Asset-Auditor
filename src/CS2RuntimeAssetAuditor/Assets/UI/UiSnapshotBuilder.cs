using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Query;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using CS2RuntimeAssetAuditor.Assets.Core.Scanning;
using CS2RuntimeAssetAuditor.Assets.GameIntegration;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    public sealed class UiSnapshotBuilder
    {
        private CensusSnapshot? _cachedCensus;
        private UiCensusCounts? _cachedCensusCounts;

        public UiSnapshot Build(AssetAuditSystem? auditSystem, AssetPage assetPage, ScanOptions scanOptions, string modVersion)
        {
            if (scanOptions == null)
                throw new ArgumentNullException(nameof(scanOptions));
            return Build(auditSystem, assetPage, new UiScanOptions
            {
                CollectSubordinateObjects = scanOptions.CollectSubordinateObjects,
                CollectNetworkEdges = scanOptions.CollectNetworkEdges
            }, modVersion);
        }

        public UiSnapshot Build(AssetAuditSystem? auditSystem, AssetPage assetPage, UiScanOptions settings, string modVersion)
        {
            if (assetPage == null)
                throw new ArgumentNullException(nameof(assetPage));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(modVersion))
                throw new ArgumentException("A mod version is required.", nameof(modVersion));

            var session = auditSystem?.CurrentScan;
            var progress = session?.Progress;
            var census = auditSystem?.PublishedCensus;
            var capabilities = auditSystem?.Capabilities;
            var catalog = auditSystem?.CatalogRecords ?? Array.Empty<PrefabRecord>();
            var catalogGeneration = auditSystem?.CatalogGeneration ?? 0;
            var catalogCapturedAt = auditSystem != null && catalogGeneration > 0 ? FormatTime(auditSystem.CatalogCapturedAt) : null;
            var publishedAnalysis = auditSystem?.PublishedAnalysis;
            var analysis = publishedAnalysis != null
                && publishedAnalysis.WorldGeneration == auditSystem?.WorldGeneration
                && publishedAnalysis.CatalogGeneration == catalogGeneration
                ? publishedAnalysis
                : null;

            return new UiSnapshot
            {
                ScanStatus = new UiScanStatus
                {
                    State = auditSystem?.DiagnosticWorkStatus == "WaitingForRuntimeCapture"
                        ? "WaitingForRuntimeCapture"
                        : session?.State.ToString() ?? ScanState.Idle.ToString(),
                    QueuedBecauseRuntimeCapture = auditSystem?.DiagnosticWorkStatus == "WaitingForRuntimeCapture",
                    InterruptedByRuntimeCapture = session?.State == ScanState.InterruptedByRuntimeCapture
                        || auditSystem?.DiagnosticWorkStatus == "InterruptedByRuntimeCapture",
                    Stage = FormatStage(session?.Stage ?? ScanStage.Idle),
                    StageNumber = progress?.StageNumber ?? 0,
                    TotalStages = progress?.TotalStages ?? 8,
                    CompletedItems = progress?.CompletedItems,
                    TotalItems = progress?.TotalItems
                },
                Summary = new UiSummary
                {
                    GameVersion = capabilities?.GameVersion ?? Core.ProjectInfo.TargetGameVersion,
                    ModVersion = modVersion,
                    Compatibility = capabilities?.Compatibility.ToString() ?? "Untested",
                    Capabilities = capabilities?.Capabilities.Select(capability => new UiCapability
                    {
                        Id = capability.Id.ToString(),
                        State = capability.State.ToString(),
                        Detail = capability.Detail
                    }).ToArray() ?? new UiCapability[0],
                    CatalogCount = catalog.Count,
                    CatalogGeneration = catalogGeneration,
                    CatalogCapturedAt = catalogCapturedAt,
                    CensusWasScanned = census != null,
                    CensusCapturedAt = census == null ? null : FormatTime(census.CapturedAt),
                    CensusCatalogGeneration = census?.CatalogGeneration,
                    CensusMatchesCatalog = census != null && census.CatalogGeneration == catalogGeneration,
                    QueryProfileVersion = census?.QueryProfileVersion,
                    CensusCounts = GetCensusCounts(census)
                },
                AssetPage = MapPage(assetPage, analysis),
                Settings = CopySettings(settings),
                Findings = MapFindings(analysis)
            };
        }

        public static string Serialize<T>(T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static bool TryDeserialize<T>(string? json, out T value) where T : class
        {
            value = null!;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    value = serializer.ReadObject(stream) as T ?? null!;
                return value != null;
            }
            catch
            {
                return false;
            }
        }

        private UiCensusCounts GetCensusCounts(CensusSnapshot? census)
        {
            if (ReferenceEquals(census, _cachedCensus) && _cachedCensusCounts != null)
                return _cachedCensusCounts;
            _cachedCensus = census;
            _cachedCensusCounts = new UiCensusCounts
            {
                TopLevelObjects = Aggregate(census, entry => entry.Counters.TopLevelObjects),
                SubordinateObjects = Aggregate(census, entry => entry.Counters.SubordinateObjects),
                LiveObjectReferences = Aggregate(census, entry => entry.Counters.LiveObjectReferences),
                NetworkEdges = Aggregate(census, entry => entry.Counters.NetworkEdges)
            };
            return _cachedCensusCounts;
        }

        private static UiCensusCounts MapCounters(CensusEntry? entry) => new UiCensusCounts
        {
            TopLevelObjects = entry == null ? Unscanned() : MapObservation(entry.Counters.TopLevelObjects),
            SubordinateObjects = entry == null ? Unscanned() : MapObservation(entry.Counters.SubordinateObjects),
            LiveObjectReferences = entry == null ? Unscanned() : MapObservation(entry.Counters.LiveObjectReferences),
            NetworkEdges = entry == null ? Unscanned() : MapObservation(entry.Counters.NetworkEdges)
        };

        private static UiAssetPage MapPage(AssetPage page, AssetAnalysisSnapshot? analysis)
        {
            return new UiAssetPage
            {
                Offset = page.Offset,
                Limit = page.Limit,
                TotalCount = page.TotalCount,
                Items = page.Items.Select(item => MapAssetRow(item, analysis)).ToArray()
            };
        }

        private static UiAssetRow MapAssetRow(AssetPageItem item, AssetAnalysisSnapshot? analysis)
        {
            PrefabAnalysisEntry? entry = null;
            if (analysis != null && analysis.TryGetPrefab(item.Asset.Key, out var resolvedEntry))
                entry = resolvedEntry;

            return new UiAssetRow
            {
                PrefabId = item.Asset.Key.PrefabId,
                PrefabType = item.Asset.Key.PrefabType,
                DisplayName = item.Asset.DisplayName,
                SourceLabel = GetSourceLabel(item.Asset.OriginEvidence),
                Traits = item.Asset.Traits.ToString().Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries),
                CountKind = item.CountKind.ToString(),
                Instances = MapObservation(item.Instances),
                Presence = item.Presence.ToString(),
                Counters = MapCounters(item.CensusEntry),
                RenderCoverage = entry == null ? Availability.NotScanned.ToString() : FormatRenderCoverage(entry.RenderCoverage),
                EstimatedTexturePayload = entry == null ? Unscanned() : MapObservation(entry.EstimatedTexturePayload),
                FindingCount = entry?.Findings.Count ?? 0,
                Lod0Vertices = entry == null ? Unscanned() : MapObservation(entry.Lod0Vertices),
                Lod1RetentionPercent = entry == null ? UnscannedDouble() : MapObservation(entry.Lod1RetentionPercent),
                MaterialCount = entry == null ? Unscanned() : MapObservation(entry.MaterialCount),
                UniqueTextureCount = entry == null ? Unscanned() : MapObservation(entry.UniqueTextureCount),
                RenderRelations = entry?.Relations.Select(relation => new UiRenderRelation
                {
                    Kind = relation.RelationKind.ToString(),
                    From = FormatPrefabKey(relation.PrefabKey),
                    To = FormatRenderKey(relation.RenderAssetKey),
                    LodLevel = relation.LodLevel
                }).ToArray() ?? new UiRenderRelation[0]
            };
        }

        private static UiFinding[] MapFindings(AssetAnalysisSnapshot? analysis)
        {
            if (analysis == null)
                return new UiFinding[0];
            return analysis.Prefabs
                .SelectMany(entry => entry.Findings.Select(finding => MapFinding(finding, entry.Key)))
                .OrderBy(finding => finding.Status, StringComparer.Ordinal)
                .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
                .ThenBy(finding => finding.PrefabId, StringComparer.Ordinal)
                .ToArray();
        }

        private static UiFinding MapFinding(Finding finding, PrefabKey key) => new UiFinding
        {
            RuleId = finding.RuleId,
            Status = finding.Status.ToString(),
            Category = finding.Category.ToString(),
            Title = finding.Title,
            Explanation = finding.Explanation,
            Evidence = finding.Evidence.ToArray(),
            Basis = finding.Basis.ToString(),
            RuleVersion = finding.RuleVersion,
            PrefabId = key.PrefabId,
            PrefabType = key.PrefabType
        };

        private static UiObservation Aggregate(CensusSnapshot? census, Func<CensusEntry, Observation<long>> select)
        {
            if (census == null)
                return Unscanned();
            long total = 0;
            var foundApplicable = false;
            foreach (var entry in census.Entries)
            {
                var observation = select(entry);
                if (observation.Availability == Availability.NotApplicable)
                    continue;
                foundApplicable = true;
                if (!observation.HasValue)
                    return MapObservation(observation);
                total = checked(total + observation.Value);
            }
            if (!foundApplicable)
                return new UiObservation { Availability = Availability.NotApplicable.ToString(), Origin = ObservationOrigin.Derived.ToString(), CapturedAt = FormatTime(census.CapturedAt) };
            return new UiObservation { Availability = Availability.Available.ToString(), Value = total, Origin = ObservationOrigin.Derived.ToString(), CapturedAt = FormatTime(census.CapturedAt) };
        }

        private static UiObservation MapObservation(Observation<long> observation) => new UiObservation
        {
            Availability = observation.Availability.ToString(),
            Value = observation.HasValue ? observation.Value : (long?)null,
            Origin = observation.Origin.ToString(),
            CapturedAt = FormatTime(observation.CapturedAt),
            DiagnosticCode = observation.DiagnosticCode
        };

        private static UiDoubleObservation MapObservation(Observation<double> observation) => new UiDoubleObservation
        {
            Availability = observation.Availability.ToString(),
            Value = observation.HasValue ? observation.Value : (double?)null,
            Origin = observation.Origin.ToString(),
            CapturedAt = FormatTime(observation.CapturedAt),
            DiagnosticCode = observation.DiagnosticCode
        };

        private static string FormatRenderCoverage(RenderCoverage coverage)
        {
            switch (coverage)
            {
                case RenderCoverage.Supported: return Availability.Available.ToString();
                case RenderCoverage.NotApplicable: return Availability.NotApplicable.ToString();
                case RenderCoverage.Failed: return Availability.Failed.ToString();
                default: return "Unknown";
            }
        }

        private static UiObservation Unscanned() => new UiObservation { Availability = Availability.NotScanned.ToString(), Origin = ObservationOrigin.Derived.ToString() };
        private static UiDoubleObservation UnscannedDouble() => new UiDoubleObservation { Availability = Availability.NotScanned.ToString(), Origin = ObservationOrigin.Derived.ToString() };

        private static UiScanOptions CopySettings(UiScanOptions settings) => new UiScanOptions
        {
            CollectSubordinateObjects = settings.CollectSubordinateObjects,
            CollectNetworkEdges = settings.CollectNetworkEdges,
            FrameBudgetMs = settings.FrameBudgetMs,
            ProgressUpdateMs = settings.ProgressUpdateMs,
            RefreshCatalogAtScanStart = settings.RefreshCatalogAtScanStart,
            EnableHeuristicFindings = settings.EnableHeuristicFindings,
            EnablePeerOutliers = settings.EnablePeerOutliers,
            ComparisonPopulation = settings.ComparisonPopulation,
            ShowNoticeFindings = settings.ShowNoticeFindings,
            PageSize = settings.PageSize,
            MetadataCacheLimit = settings.MetadataCacheLimit,
            DeepInspectionLimit = settings.DeepInspectionLimit,
            UiScale = settings.UiScale
        };

        private static string GetSourceLabel(AssetOriginEvidence evidence)
        {
            var labels = new List<string>();
            if (evidence.IsBuiltin == true) labels.Add("Built-in");
            if (evidence.IsSubscribedMod == true) labels.Add("Subscribed mod");
            if (evidence.IsPackaged == true) labels.Add("Packaged");
            if (labels.Count > 0) return string.Join(" + ", labels);
            if (evidence.IsBuiltin == false && evidence.IsSubscribedMod == false && evidence.IsPackaged == false) return "No source flags";
            return "Unknown";
        }

        private static string FormatPrefabKey(PrefabKey key) => key.PrefabType + ":" + key.PrefabId;
        private static string FormatRenderKey(RenderAssetKey key) => key.RenderAssetType + ":" + key.RenderAssetId;
        private static string FormatTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

        private static string FormatStage(ScanStage stage)
        {
            switch (stage)
            {
                case ScanStage.Preparing: return "Preparing";
                case ScanStage.CapturingCatalog: return "Capturing Prefab catalog";
                case ScanStage.ProcessingCatalog: return "Processing catalog";
                case ScanStage.CapturingObjectCensus: return "Capturing object census";
                case ScanStage.ReducingObjectCensus: return "Reducing object census";
                case ScanStage.CapturingNetworkCensus: return "Capturing network census";
                case ScanStage.ReducingNetworkCensus: return "Reducing network census";
                case ScanStage.ResolvingRenderGraph: return "Resolving render graph";
                case ScanStage.CollectingGeometry: return "Collecting geometry metadata";
                case ScanStage.CollectingSurfaceTexture: return "Collecting surface and texture metadata";
                case ScanStage.EvaluatingFindings: return "Evaluating findings";
                case ScanStage.DeepInspecting: return "Deep inspecting selected asset";
                case ScanStage.Finalizing: return "Finalizing";
                case ScanStage.Completed: return "Complete";
                default: return "Idle";
            }
        }
    }
}
