using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Diagnostics;
using CS2RuntimeAssetAuditor.Assets.Core.Findings;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.Export
{
    public sealed class AuditReportBuilder
    {
        private readonly PrivacySanitizer _sanitizer;

        public AuditReportBuilder(PrivacySanitizer sanitizer)
        {
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
        }

        public AuditReport Build(
            IEnumerable<PrefabRecord> catalog,
            long catalogGeneration,
            DateTimeOffset? catalogCapturedAt,
            CensusSnapshot? census,
            CapabilityReport capabilities,
            string modVersion,
            DateTimeOffset generatedAt,
            IEnumerable<DiagnosticAggregate>? diagnostics = null,
            IEnumerable<Finding>? findings = null,
            AssetAnalysisSnapshot? analysis = null,
            ExportScope scope = ExportScope.Full,
            IEnumerable<PrefabKey>? includedKeys = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            if (string.IsNullOrWhiteSpace(modVersion)) throw new ArgumentException("A mod version is required.", nameof(modVersion));
            if (census != null && census.CatalogGeneration != catalogGeneration)
                throw new InvalidOperationException("Cannot export a Census snapshot against a different Prefab catalog generation.");
            if (analysis != null && analysis.CatalogGeneration != catalogGeneration)
                throw new InvalidOperationException("Cannot export an Asset Analysis snapshot against a different Prefab catalog generation.");
            if (!Enum.IsDefined(typeof(ExportScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));

            var records = catalog.ToArray();
            // Findings produced by an analysis are owned by their Prefab entry; explicitly supplied findings have no owner.
            var allFindings = findings != null
                ? findings.Select(finding => new OwnedFinding(finding, null)).ToArray()
                : (analysis?.Prefabs ?? Array.Empty<PrefabAnalysisEntry>())
                    .SelectMany(entry => entry.Findings.Select(finding => new OwnedFinding(finding, entry.Key)))
                    .ToArray();
            var keySet = includedKeys == null ? null : new HashSet<PrefabKey>(includedKeys);
            var scopedRecords = ScopeRecords(records, scope, keySet);
            var scopedCensus = ScopeCensus(census?.Entries ?? Array.Empty<CensusEntry>(), scope, keySet);
            var scopedFindings = ScopeFindings(allFindings, scope, keySet);
            var scopedAnalysis = ScopeAnalysis(analysis, scope, keySet);

            return new AuditReport
            {
                SchemaVersion = ReportSchema.SchemaVersion,
                RuleSetVersion = ReportSchema.RuleSetVersion,
                QueryProfileVersion = census?.QueryProfileVersion,
                ScanOptions = new ReportScanOptions
                {
                    WasCensusScanned = census != null,
                    CollectSubordinateObjects = census == null ? (bool?)null : census.ScanOptions.CollectSubordinateObjects,
                    CollectNetworkEdges = census == null ? (bool?)null : census.ScanOptions.CollectNetworkEdges
                },
                ModVersion = _sanitizer.SanitizeText(modVersion),
                GameVersion = _sanitizer.SanitizeText(capabilities.GameVersion),
                GeneratedAt = FormatTime(generatedAt),
                CatalogCapturedAt = catalogCapturedAt.HasValue ? FormatTime(catalogCapturedAt.Value) : null,
                CensusCapturedAt = census == null ? null : FormatTime(census.CapturedAt),
                CatalogGeneration = catalogGeneration,
                CensusCatalogGeneration = census?.CatalogGeneration,
                WorldGeneration = census?.WorldGeneration ?? analysis?.WorldGeneration,
                CapabilityReport = new ReportCapabilityReport
                {
                    Compatibility = capabilities.Compatibility.ToString(),
                    Capabilities = capabilities.Capabilities.Select(MapCapability).ToArray()
                },
                Catalog = scopedRecords
                    .OrderBy(record => record.Key.PrefabType, StringComparer.Ordinal)
                    .ThenBy(record => record.Key.PrefabId, StringComparer.Ordinal)
                    .Select(MapPrefab).ToArray(),
                Census = scopedCensus
                    .OrderBy(entry => entry.Key.PrefabType, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Key.PrefabId, StringComparer.Ordinal)
                    .Select(MapCensusEntry).ToArray(),
                Diagnostics = (diagnostics ?? Array.Empty<DiagnosticAggregate>())
                    .OrderBy(diagnostic => diagnostic.Code.Value, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                    .Select(MapDiagnostic).ToArray(),
                ExportScope = scope.ToString(),
                Analysis = new ReportAnalysis
                {
                    Findings = scopedFindings
                        .OrderBy(owned => owned.Finding.RuleId, StringComparer.Ordinal)
                        .ThenBy(owned => owned.Owner?.PrefabType ?? string.Empty, StringComparer.Ordinal)
                        .ThenBy(owned => owned.Owner?.PrefabId ?? string.Empty, StringComparer.Ordinal)
                        .ThenBy(owned => owned.Finding.Title, StringComparer.Ordinal)
                        .Select(MapFinding).ToArray(),
                    Assets = scopedAnalysis.Prefabs.Select(MapAssetAnalysis).ToArray(),
                    RenderAssets = scopedAnalysis.RenderAssets.Select(MapRenderAssetAnalysis).ToArray()
                }
            };
        }

        private static IEnumerable<PrefabRecord> ScopeRecords(IEnumerable<PrefabRecord> records, ExportScope scope, HashSet<PrefabKey>? keys)
        {
            if (scope == ExportScope.Census || scope == ExportScope.Findings) return Array.Empty<PrefabRecord>();
            if (scope == ExportScope.Filtered || scope == ExportScope.Selected)
                return keys == null ? Array.Empty<PrefabRecord>() : records.Where(record => keys.Contains(record.Key));
            return records;
        }

        private static IEnumerable<CensusEntry> ScopeCensus(IEnumerable<CensusEntry> entries, ExportScope scope, HashSet<PrefabKey>? keys)
        {
            if (scope == ExportScope.Findings) return Array.Empty<CensusEntry>();
            if (scope == ExportScope.Filtered || scope == ExportScope.Selected)
                return keys == null ? Array.Empty<CensusEntry>() : entries.Where(entry => keys.Contains(entry.Key));
            return entries;
        }

        private static IEnumerable<OwnedFinding> ScopeFindings(IEnumerable<OwnedFinding> findings, ExportScope scope, HashSet<PrefabKey>? keys)
        {
            if (scope == ExportScope.Census) return Array.Empty<OwnedFinding>();
            if (scope != ExportScope.Filtered && scope != ExportScope.Selected) return findings;
            if (keys == null) return Array.Empty<OwnedFinding>();
            var ids = new HashSet<string>(keys.Select(key => key.PrefabId), StringComparer.Ordinal);
            return findings.Where(owned => owned.Owner.HasValue
                ? keys.Contains(owned.Owner.Value)
                : owned.Finding.Evidence.Any(evidence => evidence.StartsWith("asset=", StringComparison.Ordinal) && ids.Contains(evidence.Substring("asset=".Length))));
        }

        private static ScopedAnalysis ScopeAnalysis(AssetAnalysisSnapshot? analysis, ExportScope scope, HashSet<PrefabKey>? keys)
        {
            if (analysis == null || scope == ExportScope.Census || scope == ExportScope.Findings)
                return ScopedAnalysis.Empty;

            IEnumerable<PrefabAnalysisEntry> prefabs = analysis.Prefabs;
            if (scope == ExportScope.Filtered || scope == ExportScope.Selected)
                prefabs = keys == null ? Array.Empty<PrefabAnalysisEntry>() : prefabs.Where(entry => keys.Contains(entry.Key));
            var prefabArray = prefabs.ToArray();
            var referencedRenderKeys = new HashSet<RenderAssetKey>(prefabArray.SelectMany(entry => entry.Relations).Select(relation => relation.RenderAssetKey));
            var renderAssets = (scope == ExportScope.Full
                    ? analysis.RenderAssets
                    : analysis.RenderAssets.Where(record => referencedRenderKeys.Contains(record.RenderAsset.Key)))
                .ToArray();
            return new ScopedAnalysis(prefabArray, renderAssets);
        }

        private ReportCapability MapCapability(CapabilityStatus status) => new ReportCapability
        {
            Id = status.Id.ToString(),
            State = status.State.ToString(),
            Detail = status.Detail == null ? null : _sanitizer.SanitizeText(status.Detail)
        };

        private ReportPrefab MapPrefab(PrefabRecord record)
        {
            var evidence = record.OriginEvidence;
            return new ReportPrefab
            {
                PrefabId = _sanitizer.SanitizeText(record.Key.PrefabId),
                PrefabType = _sanitizer.SanitizeText(record.Key.PrefabType),
                DisplayName = _sanitizer.SanitizeText(record.DisplayName),
                Traits = record.Traits.ToString(),
                IsBuiltin = evidence.IsBuiltin,
                IsSubscribedMod = evidence.IsSubscribedMod,
                IsPackaged = evidence.IsPackaged,
                DlcPrerequisiteIds = SanitizeIdentifiers(evidence.DlcPrerequisiteIds),
                AssetPackMembership = SanitizeIdentifiers(evidence.AssetPackMembership),
                AssetDatabaseSource = evidence.AssetDatabaseSource == null ? null : _sanitizer.SanitizeText(evidence.AssetDatabaseSource),
                ParadoxModsPlatformId = evidence.ParadoxModsPlatformId == null ? null : _sanitizer.SanitizeText(evidence.ParadoxModsPlatformId)
            };
        }

        private ReportCensusEntry MapCensusEntry(CensusEntry entry)
        {
            var counters = entry.Counters;
            return new ReportCensusEntry
            {
                PrefabId = _sanitizer.SanitizeText(entry.Key.PrefabId),
                PrefabType = _sanitizer.SanitizeText(entry.Key.PrefabType),
                Presence = entry.Presence.ToString(),
                Counters = new ReportCensusCounters
                {
                    TopLevelObjects = MapObservation(counters.TopLevelObjects),
                    SubordinateObjects = MapObservation(counters.SubordinateObjects),
                    LiveObjectReferences = MapObservation(counters.LiveObjectReferences),
                    NetworkEdges = MapObservation(counters.NetworkEdges)
                }
            };
        }

        private ReportAssetAnalysis MapAssetAnalysis(PrefabAnalysisEntry entry) => new ReportAssetAnalysis
        {
            PrefabId = _sanitizer.SanitizeText(entry.Key.PrefabId),
            PrefabType = _sanitizer.SanitizeText(entry.Key.PrefabType),
            RenderCoverage = entry.RenderCoverage.ToString(),
            Lod0Vertices = MapObservation(entry.Lod0Vertices),
            Lod1RetentionPercent = MapObservation(entry.Lod1RetentionPercent),
            MaterialCount = MapObservation(entry.MaterialCount),
            UniqueTextureCount = MapObservation(entry.UniqueTextureCount),
            EstimatedTexturePayload = MapObservation(entry.EstimatedTexturePayload),
            RenderRelations = entry.Relations.Select(relation => new ReportRenderRelation
            {
                Kind = relation.RelationKind.ToString(),
                RenderAssetId = _sanitizer.SanitizeText(relation.RenderAssetKey.RenderAssetId),
                RenderAssetType = _sanitizer.SanitizeText(relation.RenderAssetKey.RenderAssetType),
                LodLevel = relation.LodLevel
            }).ToArray()
        };

        private ReportRenderAssetAnalysis MapRenderAssetAnalysis(RenderAssetAnalysisRecord record) => new ReportRenderAssetAnalysis
        {
            RenderAssetId = _sanitizer.SanitizeText(record.RenderAsset.Key.RenderAssetId),
            RenderAssetType = _sanitizer.SanitizeText(record.RenderAsset.Key.RenderAssetType),
            DisplayName = _sanitizer.SanitizeText(record.RenderAsset.DisplayName),
            Geometry = record.Geometry == null ? null : MapGeometry(record.Geometry),
            Surfaces = record.Surfaces.Select(MapSurface).ToArray(),
            Textures = record.Textures.Select(MapTexture).ToArray(),
            DeepInspection = record.RenderAsset.DeepInspection == null ? null : MapDeepInspection(record.RenderAsset.DeepInspection)
        };

        private ReportGeometry MapGeometry(GeometryObservation geometry) => new ReportGeometry
        {
            GeometryAssetId = _sanitizer.SanitizeText(geometry.GeometryAssetId),
            MeshCount = MapObservation(geometry.MeshCount),
            TotalVertexCount = MapObservation(geometry.TotalVertexCount),
            TotalIndexCount = MapObservation(geometry.TotalIndexCount),
            SubMeshCount = MapObservation(geometry.SubMeshCount),
            CompressedDataSize = MapObservation(geometry.CompressedDataSize),
            Meshes = geometry.Meshes.Select(mesh => new ReportMesh
            {
                MeshIndex = mesh.MeshIndex,
                VertexCount = MapObservation(mesh.VertexCount),
                IndexCount = MapObservation(mesh.IndexCount),
                IndexFormat = MapObservation(mesh.IndexFormat),
                SubMeshes = mesh.SubMeshes.Select(MapSubMesh).ToArray()
            }).ToArray()
        };

        private ReportSubMesh MapSubMesh(SubMeshObservation subMesh) => new ReportSubMesh
        {
            MeshIndex = subMesh.MeshIndex,
            SubMeshIndex = subMesh.SubMeshIndex,
            Topology = _sanitizer.SanitizeText(subMesh.Topology),
            IndexCount = MapObservation(subMesh.IndexCount),
            VertexCount = MapObservation(subMesh.VertexCount),
            TriangleCount = MapObservation(subMesh.TriangleCount),
            Bounds = subMesh.Bounds.HasValue ? new ReportBounds
            {
                CenterX = subMesh.Bounds.CenterX,
                CenterY = subMesh.Bounds.CenterY,
                CenterZ = subMesh.Bounds.CenterZ,
                ExtentsX = subMesh.Bounds.ExtentsX,
                ExtentsY = subMesh.Bounds.ExtentsY,
                ExtentsZ = subMesh.Bounds.ExtentsZ
            } : null
        };

        private ReportSurface MapSurface(SurfaceObservation surface) => new ReportSurface
        {
            SurfaceAssetId = _sanitizer.SanitizeText(surface.SurfaceAssetId),
            MaterialTemplateHash = MapObservation(surface.MaterialTemplateHash),
            IsVirtualTexturingMaterial = MapObservation(surface.IsVirtualTexturingMaterial),
            IsCurrentlyUsingVirtualTexturing = MapObservation(surface.IsCurrentlyUsingVirtualTexturing),
            FloatPropertyCount = surface.FloatPropertyCount,
            IntPropertyCount = surface.IntPropertyCount,
            VectorPropertyCount = surface.VectorPropertyCount,
            ColorPropertyCount = surface.ColorPropertyCount,
            Keywords = surface.Keywords.Select(_sanitizer.SanitizeText).ToArray(),
            TextureAssetIds = surface.TextureAssetIds.Select(_sanitizer.SanitizeText).ToArray()
        };

        private ReportTexture MapTexture(TextureObservation texture) => new ReportTexture
        {
            TextureAssetId = _sanitizer.SanitizeText(texture.TextureAssetId),
            Width = MapObservation(texture.Width),
            Height = MapObservation(texture.Height),
            Depth = MapObservation(texture.Depth),
            Format = MapObservation(texture.Format),
            Dimension = MapObservation(texture.Dimension),
            MipsCount = MapObservation(texture.MipsCount),
            FilterMode = MapObservation(texture.FilterMode),
            WrapMode = MapObservation(texture.WrapMode),
            AnisoLevel = MapObservation(texture.AnisoLevel),
            EstimatedLogicalPayload = MapObservation(texture.EstimatedLogicalPayload)
        };

        private ReportDeepInspection MapDeepInspection(DeepInspectionObservation inspection) => new ReportDeepInspection
        {
            Availability = inspection.Availability.ToString(),
            CapturedAt = FormatTime(inspection.CapturedAt),
            DiagnosticCode = inspection.DiagnosticCode == null ? null : _sanitizer.SanitizeText(inspection.DiagnosticCode),
            SurfaceAssetIds = inspection.SurfaceAssetIds.Select(_sanitizer.SanitizeText).ToArray(),
            Materials = inspection.Materials.Select(material => new ReportMaterialBinding
            {
                MaterialName = _sanitizer.SanitizeText(material.MaterialName),
                ShaderName = _sanitizer.SanitizeText(material.ShaderName),
                ShaderKeywords = material.ShaderKeywords.Select(_sanitizer.SanitizeText).ToArray(),
                RenderQueue = material.RenderQueue,
                PassCount = material.PassCount,
                EnableInstancing = material.EnableInstancing
            }).ToArray()
        };

        private ReportFinding MapFinding(OwnedFinding owned)
        {
            var finding = owned.Finding;
            return new ReportFinding
            {
                RuleId = _sanitizer.SanitizeText(finding.RuleId),
                Status = finding.Status.ToString(),
                Category = finding.Category.ToString(),
                Title = _sanitizer.SanitizeText(finding.Title),
                Explanation = _sanitizer.SanitizeText(finding.Explanation),
                Evidence = finding.Evidence.Select(_sanitizer.SanitizeText).ToArray(),
                Basis = finding.Basis.ToString(),
                RuleVersion = _sanitizer.SanitizeText(finding.RuleVersion),
                PrefabId = owned.Owner.HasValue ? _sanitizer.SanitizeText(owned.Owner.Value.PrefabId) : null,
                PrefabType = owned.Owner.HasValue ? _sanitizer.SanitizeText(owned.Owner.Value.PrefabType) : null
            };
        }

        private ReportObservation MapObservation(Observation<long> observation) => MapLongObservation(
            observation.Availability, observation.Origin, observation.CapturedAt, observation.HasValue ? observation.Value : (long?)null, observation.DiagnosticCode);

        private ReportObservation MapObservation(Observation<int> observation) => MapLongObservation(
            observation.Availability, observation.Origin, observation.CapturedAt, observation.HasValue ? observation.Value : (long?)null, observation.DiagnosticCode);

        private ReportDoubleObservation MapObservation(Observation<double> observation) => new ReportDoubleObservation
        {
            Availability = observation.Availability.ToString(),
            Origin = observation.Origin.ToString(),
            CapturedAt = FormatTime(observation.CapturedAt),
            Value = observation.HasValue ? observation.Value : (double?)null,
            DiagnosticCode = observation.DiagnosticCode == null ? null : _sanitizer.SanitizeText(observation.DiagnosticCode)
        };

        private ReportStringObservation MapObservation(Observation<string> observation) => new ReportStringObservation
        {
            Availability = observation.Availability.ToString(),
            Origin = observation.Origin.ToString(),
            CapturedAt = FormatTime(observation.CapturedAt),
            Value = observation.HasValue ? _sanitizer.SanitizeText(observation.Value) : null,
            DiagnosticCode = observation.DiagnosticCode == null ? null : _sanitizer.SanitizeText(observation.DiagnosticCode)
        };

        private ReportBooleanObservation MapObservation(Observation<bool> observation) => new ReportBooleanObservation
        {
            Availability = observation.Availability.ToString(),
            Origin = observation.Origin.ToString(),
            CapturedAt = FormatTime(observation.CapturedAt),
            Value = observation.HasValue ? observation.Value : (bool?)null,
            DiagnosticCode = observation.DiagnosticCode == null ? null : _sanitizer.SanitizeText(observation.DiagnosticCode)
        };

        private ReportObservation MapLongObservation(Availability availability, ObservationOrigin origin, DateTimeOffset capturedAt, long? value, string? diagnosticCode) => new ReportObservation
        {
            Availability = availability.ToString(),
            Origin = origin.ToString(),
            CapturedAt = FormatTime(capturedAt),
            Value = value,
            DiagnosticCode = diagnosticCode == null ? null : _sanitizer.SanitizeText(diagnosticCode)
        };

        private ReportDiagnostic MapDiagnostic(DiagnosticAggregate diagnostic) => new ReportDiagnostic
        {
            Code = diagnostic.Code.Value,
            Message = _sanitizer.SanitizeText(diagnostic.Message),
            Count = diagnostic.Count,
            FirstSeenAt = FormatTime(diagnostic.FirstSeenAt),
            LastSeenAt = FormatTime(diagnostic.LastSeenAt)
        };

        private string[]? SanitizeIdentifiers(IReadOnlyList<string>? identifiers) => identifiers?.Select(_sanitizer.SanitizeText).ToArray();
        private static string FormatTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

        private sealed class OwnedFinding
        {
            public OwnedFinding(Finding finding, PrefabKey? owner)
            {
                Finding = finding;
                Owner = owner;
            }
            public Finding Finding { get; }
            public PrefabKey? Owner { get; }
        }

        private sealed class ScopedAnalysis
        {
            public static ScopedAnalysis Empty { get; } = new ScopedAnalysis(Array.Empty<PrefabAnalysisEntry>(), Array.Empty<RenderAssetAnalysisRecord>());
            public ScopedAnalysis(PrefabAnalysisEntry[] prefabs, RenderAssetAnalysisRecord[] renderAssets)
            {
                Prefabs = prefabs;
                RenderAssets = renderAssets;
            }
            public PrefabAnalysisEntry[] Prefabs { get; }
            public RenderAssetAnalysisRecord[] RenderAssets { get; }
        }
    }
}
