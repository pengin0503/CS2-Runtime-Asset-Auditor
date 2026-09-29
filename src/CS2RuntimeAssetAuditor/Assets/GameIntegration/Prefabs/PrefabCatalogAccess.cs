using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs
{
    public sealed class PrefabCatalogAccess : IPrefabCatalogAccess
    {
        private readonly World _world;
        private Entity[] _capturedEntities = Array.Empty<Entity>();
        private int _capturedEntityCount;
        private int _nextEntityIndex;
        private bool _hasWorkingCapture;
        private bool _deferPublication;
        private bool _hasPendingPublication;
        private bool _pendingContentChanged;
        private DateTimeOffset _workingCapturedAt;
        private DateTimeOffset _pendingCapturedAt;
        private List<PrefabRecord> _workingRecords = new List<PrefabRecord>();
        private Dictionary<PrefabKey, PrefabRecord> _workingByKey = new Dictionary<PrefabKey, PrefabRecord>();
        private Dictionary<Entity, PrefabKey> _workingEntityKeys = new Dictionary<Entity, PrefabKey>();
        private IReadOnlyList<PrefabRecord> _pendingRecords = Array.AsReadOnly(Array.Empty<PrefabRecord>());
        private IReadOnlyDictionary<Entity, PrefabKey> _pendingEntityKeys =
            new ReadOnlyDictionary<Entity, PrefabKey>(new Dictionary<Entity, PrefabKey>());
        private IReadOnlyList<PrefabRecord> _publishedRecords = Array.AsReadOnly(Array.Empty<PrefabRecord>());
        private IReadOnlyDictionary<Entity, PrefabKey> _publishedEntityKeys =
            new ReadOnlyDictionary<Entity, PrefabKey>(new Dictionary<Entity, PrefabKey>());

        public PrefabCatalogAccess(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public bool IsWorking => _hasWorkingCapture;

        public bool HasPendingItems => _hasWorkingCapture && _nextEntityIndex < _capturedEntities.Length;

        public bool HasPendingPublication => _hasPendingPublication;

        public int CapturedEntityCount => _capturedEntityCount;

        public int ProcessedEntityCount { get; private set; }

        public int UnresolvedEntityCount { get; private set; }

        public long CatalogGeneration { get; private set; }

        public long PendingCatalogGeneration => _hasPendingPublication && _pendingContentChanged
            ? checked(CatalogGeneration + 1)
            : CatalogGeneration;

        public long CompletedCaptureCount { get; private set; }

        public DateTimeOffset CatalogCapturedAt { get; private set; }

        public DateTimeOffset PendingCapturedAt => _hasPendingPublication ? _pendingCapturedAt : DateTimeOffset.MinValue;

        public IReadOnlyList<PrefabRecord> PublishedRecords => _publishedRecords;

        public IReadOnlyDictionary<Entity, PrefabKey> RuntimeEntityKeys => _publishedEntityKeys;

        public IReadOnlyList<PrefabRecord> PendingRecords => _pendingRecords;

        public IReadOnlyDictionary<Entity, PrefabKey> PendingRuntimeEntityKeys => _pendingEntityKeys;

        public void BeginCapture(bool deferPublication = false)
        {
            if (_hasWorkingCapture || _hasPendingPublication)
                throw new InvalidOperationException("A Prefab catalog capture or pending publication is already active.");
            if (!_world.IsCreated)
                throw new InvalidOperationException("The current game world is no longer available.");

            var entityManager = _world.EntityManager;
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PrefabData>());
            NativeArray<Entity> captured = default;
            try
            {
                captured = query.ToEntityArray(Allocator.Temp);
                _capturedEntities = new Entity[captured.Length];
                _capturedEntityCount = captured.Length;
                for (var index = 0; index < captured.Length; index++)
                    _capturedEntities[index] = captured[index];
            }
            finally
            {
                if (captured.IsCreated)
                    captured.Dispose();
                query.Dispose();
            }

            _nextEntityIndex = 0;
            _workingCapturedAt = DateTimeOffset.UtcNow;
            _deferPublication = deferPublication;
            ProcessedEntityCount = 0;
            UnresolvedEntityCount = 0;
            _workingRecords = new List<PrefabRecord>(_capturedEntities.Length);
            _workingByKey = new Dictionary<PrefabKey, PrefabRecord>();
            _workingEntityKeys = new Dictionary<Entity, PrefabKey>();
            _hasWorkingCapture = true;
        }

        public int ProcessNextSlice(int maximumItems)
        {
            if (maximumItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (!_hasWorkingCapture)
                return 0;

            var prefabSystem = _world.GetExistingSystemManaged<PrefabSystem>();
            if (prefabSystem == null)
            {
                CancelCapture();
                return 0;
            }

            var processedThisSlice = 0;
            var entityManager = _world.EntityManager;
            while (_nextEntityIndex < _capturedEntities.Length && processedThisSlice < maximumItems)
            {
                var entity = _capturedEntities[_nextEntityIndex++];
                processedThisSlice++;
                ProcessedEntityCount++;

                try
                {
                    if (!entityManager.Exists(entity))
                    {
                        UnresolvedEntityCount++;
                        continue;
                    }

                    var prefabData = entityManager.GetComponentData<PrefabData>(entity);
                    if (!prefabSystem.TryGetPrefab<PrefabBase>(prefabData, out var prefab) || prefab == null)
                    {
                        UnresolvedEntityCount++;
                        continue;
                    }

                    var traits = Classify(prefab);
                    var prefabId = GetStablePrefabId(prefab);
                    var key = new PrefabKey(prefabId, PrefabClassifier.GetTypeId(traits));
                    var asset = prefab.asset;
                    var evidence = SourceMetadataReader.Read(
                        prefab.isBuiltin,
                        prefab.isSubscribedMod,
                        prefab.isPackaged,
                        assetDatabaseSource: string.IsNullOrWhiteSpace(asset?.identifier) ? null : asset!.identifier);
                    var displayName = GetDisplayName(prefab, prefabId);
                    if (!_workingByKey.ContainsKey(key))
                    {
                        var record = new PrefabRecord(key, displayName, traits, evidence);
                        _workingByKey.Add(key, record);
                        _workingRecords.Add(record);
                    }
                    _workingEntityKeys[entity] = key;
                }
                catch
                {
                    // A single malformed Prefab does not invalidate other catalog entries.
                    UnresolvedEntityCount++;
                }
            }

            if (_nextEntityIndex >= _capturedEntities.Length)
            {
                if (_deferPublication)
                    StageWorkingCapture();
                else
                    PublishWorkingCapture();
            }

            return processedThisSlice;
        }

        public void CommitPendingCapture()
        {
            if (!_hasPendingPublication)
                throw new InvalidOperationException("No pending Prefab catalog capture is available to publish.");

            if (_pendingContentChanged)
            {
                _publishedRecords = _pendingRecords;
                _publishedEntityKeys = _pendingEntityKeys;
                CatalogGeneration = checked(CatalogGeneration + 1);
            }
            CatalogCapturedAt = _pendingCapturedAt;
            CompletedCaptureCount = checked(CompletedCaptureCount + 1);
            DiscardPendingCapture();
        }

        public void DiscardPendingCapture()
        {
            _hasPendingPublication = false;
            _pendingContentChanged = false;
            _pendingCapturedAt = DateTimeOffset.MinValue;
            _pendingRecords = Array.AsReadOnly(Array.Empty<PrefabRecord>());
            _pendingEntityKeys = new ReadOnlyDictionary<Entity, PrefabKey>(new Dictionary<Entity, PrefabKey>());
        }

        public void CancelCapture()
        {
            ClearWorkingCapture();
            DiscardPendingCapture();
        }

        public void ResetForWorld()
        {
            CancelCapture();
            _publishedRecords = Array.AsReadOnly(Array.Empty<PrefabRecord>());
            _publishedEntityKeys = new ReadOnlyDictionary<Entity, PrefabKey>(new Dictionary<Entity, PrefabKey>());
            CatalogGeneration = 0;
            CompletedCaptureCount = 0;
            CatalogCapturedAt = DateTimeOffset.MinValue;
            _capturedEntityCount = 0;
            ProcessedEntityCount = 0;
            UnresolvedEntityCount = 0;
        }

        private void StageWorkingCapture()
        {
            _pendingRecords = Array.AsReadOnly(_workingRecords.ToArray());
            _pendingEntityKeys = new ReadOnlyDictionary<Entity, PrefabKey>(
                new Dictionary<Entity, PrefabKey>(_workingEntityKeys));
            _pendingCapturedAt = _workingCapturedAt;
            _pendingContentChanged = WorkingCaptureChangesPublishedCatalog();
            _hasPendingPublication = true;
            ClearWorkingCapture();
        }

        private void PublishWorkingCapture()
        {
            if (WorkingCaptureChangesPublishedCatalog())
            {
                _publishedRecords = Array.AsReadOnly(_workingRecords.ToArray());
                _publishedEntityKeys = new ReadOnlyDictionary<Entity, PrefabKey>(
                    new Dictionary<Entity, PrefabKey>(_workingEntityKeys));
                CatalogGeneration = checked(CatalogGeneration + 1);
            }
            CatalogCapturedAt = _workingCapturedAt;
            CompletedCaptureCount = checked(CompletedCaptureCount + 1);
            ClearWorkingCapture();
        }

        // The generation identifies catalog content that Census and Analysis snapshots are bound to.
        // An identical recapture keeps the generation so those snapshots stay current.
        private bool WorkingCaptureChangesPublishedCatalog()
        {
            if (CompletedCaptureCount == 0)
                return true;
            if (!PrefabCatalogContent.HasSameRecords(_publishedRecords, _workingRecords))
                return true;
            if (_publishedEntityKeys.Count != _workingEntityKeys.Count)
                return true;
            foreach (var pair in _workingEntityKeys)
            {
                if (!_publishedEntityKeys.TryGetValue(pair.Key, out var publishedKey) || publishedKey != pair.Value)
                    return true;
            }
            return false;
        }

        private void ClearWorkingCapture()
        {
            _capturedEntities = Array.Empty<Entity>();
            _nextEntityIndex = 0;
            _hasWorkingCapture = false;
            _deferPublication = false;
            _workingCapturedAt = DateTimeOffset.MinValue;
            _workingRecords = new List<PrefabRecord>();
            _workingByKey = new Dictionary<PrefabKey, PrefabRecord>();
            _workingEntityKeys = new Dictionary<Entity, PrefabKey>();
        }

        // Follows the Game.Prefabs hierarchy: StaticObjectPrefab is the base of BuildingPrefab, BuildingExtensionPrefab
        // and ActivityPropPrefab. Extensions are not buildings in that hierarchy and are classified on their own so
        // that peer comparison never mixes them with props or with whole buildings.
        private static PrefabTraits Classify(PrefabBase prefab)
        {
            var hasServiceBuildingMarker = false;
            var hasTreeMarker = false;
            var hasPlantMarker = false;
            if (prefab.components != null)
            {
                foreach (var component in prefab.components)
                {
                    if (component is CityServiceBuilding)
                        hasServiceBuildingMarker = true;
                    if (component is TreeObject)
                        hasTreeMarker = true;
                    if (component is PlantObject)
                        hasPlantMarker = true;
                }
            }

            var isStaticObject = prefab is StaticObjectPrefab;
            var isBuilding = prefab is BuildingPrefab;
            var isBuildingExtension = prefab is BuildingExtensionPrefab;
            var isTree = isStaticObject && hasTreeMarker;
            var isPlant = isStaticObject && !isTree && hasPlantMarker;
            var isProp = isStaticObject && !isBuilding && !isBuildingExtension && !isTree && !isPlant;
            return PrefabClassifier.Classify(
                isBuilding: isBuilding,
                isServiceBuilding: isBuilding && hasServiceBuildingMarker,
                isProp: isProp,
                isTree: isTree,
                isVehicle: prefab is VehiclePrefab,
                isNetwork: prefab is NetPrefab,
                isRenderOnly: prefab is RenderPrefab,
                isBuildingExtension: isBuildingExtension,
                isPlant: isPlant);
        }

        private static string GetStablePrefabId(PrefabBase prefab)
        {
            var asset = prefab.asset;
            if (!string.IsNullOrWhiteSpace(asset?.identifier))
                return asset!.identifier;
            if (!string.IsNullOrWhiteSpace(asset?.uniqueName))
                return asset!.uniqueName;
            if (!string.IsNullOrWhiteSpace(prefab.name))
                return prefab.name;
            throw new InvalidOperationException("A stable Prefab identifier is unavailable.");
        }

        private static string GetDisplayName(PrefabBase prefab, string prefabId)
        {
            if (!string.IsNullOrWhiteSpace(prefab.name))
                return prefab.name;
            var assetName = prefab.asset?.name;
            if (assetName != null && !string.IsNullOrWhiteSpace(assetName))
                return assetName;
            return prefabId;
        }
    }
}
