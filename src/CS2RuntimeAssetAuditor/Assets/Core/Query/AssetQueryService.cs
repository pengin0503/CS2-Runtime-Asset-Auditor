using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.Core.Query
{
    public sealed class AssetQueryService
    {
        public const int MaximumPageSize = 200;
        private readonly IReadOnlyList<PrefabRecord> _catalog;
        private readonly CensusSnapshot? _census;

        public AssetQueryService(IEnumerable<PrefabRecord> catalog, CensusSnapshot? census, long? catalogGeneration = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            _catalog = Array.AsReadOnly(catalog.ToArray());
            _census = census != null && catalogGeneration.HasValue && census.CatalogGeneration != catalogGeneration.Value ? null : census;
        }

        public AssetPage Query(AssetQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var ordered = MatchingRows(query);
            var limit = Math.Min(query.Limit, MaximumPageSize);
            var items = ordered.Skip(query.Offset).Take(limit).ToArray();
            return new AssetPage(Array.AsReadOnly(items), ordered.Length, query.Offset, limit);
        }

        public IReadOnlyList<PrefabKey> QueryMatchingKeys(AssetQuery query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            return Array.AsReadOnly(MatchingRows(query).Select(item => item.Asset.Key).ToArray());
        }

        private AssetPageItem[] MatchingRows(AssetQuery query)
        {
            var rows = _catalog
                .Where(record => MatchesSearch(record, query.SearchText))
                .Where(record => MatchesTraits(record, query.TraitFilter))
                .Where(record => MatchesSource(record.OriginEvidence, query.SourceFilter))
                .Select(CreateItem)
                .Where(item => !query.PresenceFilter.HasValue || item.Presence == query.PresenceFilter.Value)
                .ToArray();
            return Sort(rows, query.Sort);
        }

        private AssetPageItem CreateItem(PrefabRecord record)
        {
            CensusEntry? entry = null;
            if (_census != null) _census.TryGetEntry(record.Key, out entry!);
            var countKind = GetCountKind(record.Traits);
            return new AssetPageItem(record, entry, GetInstances(entry, countKind), countKind, entry?.Presence ?? CensusPresence.Unknown);
        }

        private static Observation<long> GetInstances(CensusEntry? entry, CensusCountKind countKind)
        {
            if (countKind == CensusCountKind.None) return Observation<long>.Unavailable(Availability.NotApplicable, ObservationOrigin.Derived, DateTimeOffset.MinValue);
            if (entry == null) return Observation<long>.Unavailable(Availability.NotScanned, ObservationOrigin.Ecs, DateTimeOffset.MinValue);
            switch (countKind)
            {
                case CensusCountKind.TopLevelObjects: return entry.Counters.TopLevelObjects;
                case CensusCountKind.SubordinateObjects: return entry.Counters.SubordinateObjects;
                case CensusCountKind.LiveObjectReferences: return entry.Counters.LiveObjectReferences;
                case CensusCountKind.NetworkEdges: return entry.Counters.NetworkEdges;
                default: return Observation<long>.Unavailable(Availability.NotScanned, ObservationOrigin.Ecs, DateTimeOffset.MinValue);
            }
        }

        private static CensusCountKind GetCountKind(PrefabTraits traits)
        {
            if (traits.HasFlag(PrefabTraits.Network)) return CensusCountKind.NetworkEdges;
            if (traits.HasFlag(PrefabTraits.Prop) || traits.HasFlag(PrefabTraits.Vehicle)) return CensusCountKind.LiveObjectReferences;
            if (traits.HasFlag(PrefabTraits.Building) || traits.HasFlag(PrefabTraits.ServiceBuilding) || traits.HasFlag(PrefabTraits.Tree)) return CensusCountKind.TopLevelObjects;
            return CensusCountKind.None;
        }

        private static bool MatchesSearch(PrefabRecord record, string? searchText)
        {
            if (searchText == null || searchText.Length == 0) return true;
            var comparison = StringComparison.OrdinalIgnoreCase;
            var evidence = record.OriginEvidence;
            return record.DisplayName.IndexOf(searchText, comparison) >= 0
                || record.Key.PrefabId.IndexOf(searchText, comparison) >= 0
                || record.Key.PrefabType.IndexOf(searchText, comparison) >= 0
                || Contains(evidence.AssetDatabaseSource, searchText, comparison)
                || Contains(evidence.ParadoxModsPlatformId, searchText, comparison)
                || ContainsAny(evidence.DlcPrerequisiteIds, searchText, comparison)
                || ContainsAny(evidence.AssetPackMembership, searchText, comparison);
        }

        private static bool Contains(string? value, string search, StringComparison comparison) => value != null && value.IndexOf(search, comparison) >= 0;
        private static bool ContainsAny(IReadOnlyList<string>? values, string search, StringComparison comparison) => values != null && values.Any(value => value.IndexOf(search, comparison) >= 0);
        private static bool MatchesTraits(PrefabRecord record, PrefabTraits? filter) => !filter.HasValue || (record.Traits & filter.Value) != PrefabTraits.None;

        private static bool MatchesSource(AssetOriginEvidence evidence, AssetSourceFilter filter)
        {
            switch (filter)
            {
                case AssetSourceFilter.Any: return true;
                case AssetSourceFilter.Builtin: return evidence.IsBuiltin == true;
                case AssetSourceFilter.SubscribedMod: return evidence.IsSubscribedMod == true;
                case AssetSourceFilter.Packaged: return evidence.IsPackaged == true;
                case AssetSourceFilter.UserProvided: return evidence.IsBuiltin == false && evidence.IsSubscribedMod == false && evidence.IsPackaged == false;
                case AssetSourceFilter.Unknown: return !evidence.IsBuiltin.HasValue && !evidence.IsSubscribedMod.HasValue && !evidence.IsPackaged.HasValue;
                default: return false;
            }
        }

        private static AssetPageItem[] Sort(IEnumerable<AssetPageItem> rows, AssetSort sort)
        {
            IOrderedEnumerable<AssetPageItem> ordered;
            switch (sort)
            {
                case AssetSort.DisplayNameDescending: ordered = rows.OrderByDescending(item => item.Asset.DisplayName, StringComparer.OrdinalIgnoreCase); break;
                case AssetSort.PrefabIdAscending: ordered = rows.OrderBy(item => item.Asset.Key.PrefabId, StringComparer.OrdinalIgnoreCase); break;
                case AssetSort.InstancesDescending: ordered = rows.OrderByDescending(item => item.Instances.HasValue ? item.Instances.Value : long.MinValue); break;
                default: ordered = rows.OrderBy(item => item.Asset.DisplayName, StringComparer.OrdinalIgnoreCase); break;
            }
            return ordered.ThenBy(item => item.Asset.Key.PrefabType, StringComparer.Ordinal).ThenBy(item => item.Asset.Key.PrefabId, StringComparer.Ordinal).ToArray();
        }
    }
}
