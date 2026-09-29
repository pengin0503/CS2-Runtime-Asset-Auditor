using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Census
{
    /// <summary>
    /// Copies the PrefabRef of every counted entity on the main thread, then reduces the copies over later frames.
    /// The copy is synchronous on purpose: EntityQuery.ToComponentDataListAsync returns a job the ECS dependency
    /// manager does not track, so structural changes made by other systems could move or free chunks while it
    /// still reads them. ToComponentDataArray only waits for jobs writing PrefabRef and reads chunks while no
    /// structural change can run. The reduction reads the copies, never the live chunks.
    /// </summary>
    public sealed class CensusAccess : IDisposable
    {
        private readonly World _world;
        private NativeArray<PrefabRef> _topLevelObjects;
        private NativeArray<PrefabRef> _subordinateObjects;
        private NativeArray<PrefabRef> _networkEdges;
        private IReadOnlyDictionary<Entity, PrefabKey> _catalogKeys = new Dictionary<Entity, PrefabKey>();
        private CensusReducer? _reducer;
        private ScanOptions? _scanOptions;
        private int _topLevelIndex;
        private int _subordinateIndex;
        private int _networkIndex;
        private bool _objectsCaptured;
        private bool _networkCaptured;

        public CensusAccess(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public int CapturedObjectReferenceCount => Length(_topLevelObjects) + Length(_subordinateObjects);

        public int CapturedNetworkEdgeCount => Length(_networkEdges);

        public int ProcessedObjectReferenceCount { get; private set; }

        public int ProcessedNetworkEdgeCount { get; private set; }

        public int UnmatchedPrefabReferenceCount { get; private set; }

        public bool ObjectReductionCompleted => _objectsCaptured
            && _topLevelIndex >= Length(_topLevelObjects)
            && _subordinateIndex >= Length(_subordinateObjects);

        public bool NetworkReductionCompleted => _networkCaptured && _networkIndex >= Length(_networkEdges);

        public void CaptureObjects(IReadOnlyDictionary<Entity, PrefabKey> catalogKeys, CensusReducer reducer, ScanOptions scanOptions)
        {
            if (catalogKeys == null) throw new ArgumentNullException(nameof(catalogKeys));
            if (reducer == null) throw new ArgumentNullException(nameof(reducer));
            if (scanOptions == null) throw new ArgumentNullException(nameof(scanOptions));
            if (!_world.IsCreated)
                throw new InvalidOperationException("The current game world is no longer available.");

            Reset();
            UnmatchedPrefabReferenceCount = 0;
            _catalogKeys = catalogKeys;
            _reducer = reducer;
            _scanOptions = scanOptions;

            var owned = new[] { ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>() };
            var common = new[] { ComponentType.ReadOnly<Game.Objects.Object>(), ComponentType.ReadOnly<PrefabRef>() };
            _topLevelObjects = Capture(common, null, Excluded(owned));
            if (scanOptions.CollectSubordinateObjects)
                _subordinateObjects = Capture(common, owned, Excluded());
            _objectsCaptured = true;
        }

        public int ReduceObjectSlice(int maximumItems)
        {
            if (maximumItems <= 0) throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (!_objectsCaptured || _reducer == null)
                throw new InvalidOperationException("Objects must be captured before reduction.");

            var processed = 0;
            while (_topLevelIndex < Length(_topLevelObjects) && processed < maximumItems)
            {
                AddObjectReference(_topLevelObjects[_topLevelIndex++], isSubordinate: false);
                processed++;
            }
            while (_subordinateIndex < Length(_subordinateObjects) && processed < maximumItems)
            {
                AddObjectReference(_subordinateObjects[_subordinateIndex++], isSubordinate: true);
                processed++;
            }
            ProcessedObjectReferenceCount += processed;
            return processed;
        }

        public void ReleaseObjectCapture()
        {
            DisposeIfCreated(ref _topLevelObjects);
            DisposeIfCreated(ref _subordinateObjects);
        }

        public void CaptureNetwork()
        {
            if (_reducer == null || _scanOptions == null)
                throw new InvalidOperationException("Object capture must start the census before network capture.");
            _networkIndex = 0;
            ProcessedNetworkEdgeCount = 0;
            if (_scanOptions.CollectNetworkEdges)
            {
                if (!_world.IsCreated)
                    throw new InvalidOperationException("The current game world is no longer available.");
                _networkEdges = Capture(
                    new[] { ComponentType.ReadOnly<Game.Net.Edge>(), ComponentType.ReadOnly<PrefabRef>() },
                    null,
                    Excluded(new[] { ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>() }));
            }
            _networkCaptured = true;
        }

        public int ReduceNetworkSlice(int maximumItems)
        {
            if (maximumItems <= 0) throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (!_networkCaptured || _reducer == null)
                throw new InvalidOperationException("Network edges must be captured before reduction.");

            var processed = 0;
            while (_networkIndex < Length(_networkEdges) && processed < maximumItems)
            {
                if (_catalogKeys.TryGetValue(_networkEdges[_networkIndex++].m_Prefab, out var key))
                    CensusSample.ForNetworkEdge(key).AddTo(_reducer);
                else
                    UnmatchedPrefabReferenceCount++;
                processed++;
            }
            ProcessedNetworkEdgeCount += processed;
            return processed;
        }

        public CensusSnapshot BuildSnapshot()
        {
            return _reducer?.BuildSnapshot() ?? throw new InvalidOperationException("No Census reducer is active.");
        }

        /// <summary>Releases the copies and forgets the scan; used on completion, cancellation and failure.</summary>
        public void Reset()
        {
            ReleaseObjectCapture();
            DisposeIfCreated(ref _networkEdges);
            _catalogKeys = new Dictionary<Entity, PrefabKey>();
            _reducer = null;
            _scanOptions = null;
            _topLevelIndex = 0;
            _subordinateIndex = 0;
            _networkIndex = 0;
            _objectsCaptured = false;
            _networkCaptured = false;
            ProcessedObjectReferenceCount = 0;
            ProcessedNetworkEdgeCount = 0;
        }

        public void Dispose() => Reset();

        private void AddObjectReference(PrefabRef prefabReference, bool isSubordinate)
        {
            if (_catalogKeys.TryGetValue(prefabReference.m_Prefab, out var key))
                CensusSample.ForObject(key, isSubordinate).AddTo(_reducer!);
            else
                UnmatchedPrefabReferenceCount++;
        }

        private NativeArray<PrefabRef> Capture(ComponentType[] all, ComponentType[]? any, ComponentType[] none)
        {
            var query = _world.EntityManager.CreateEntityQuery(new[] { new EntityQueryDesc { All = all, Any = any, None = none } });
            try
            {
                return query.ToComponentDataArray<PrefabRef>(Allocator.Persistent);
            }
            finally
            {
                query.Dispose();
            }
        }

        // Preview (Temp), removed (Deleted) and replaced (Overridden) entities are not part of the city.
        private static ComponentType[] Excluded(params ComponentType[] additional)
        {
            var excluded = new List<ComponentType>
            {
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Deleted>(),
                ComponentType.ReadOnly<Overridden>()
            };
            excluded.AddRange(additional);
            return excluded.ToArray();
        }

        private static int Length(NativeArray<PrefabRef> array) => array.IsCreated ? array.Length : 0;

        private static void DisposeIfCreated(ref NativeArray<PrefabRef> array)
        {
            if (array.IsCreated)
                array.Dispose();
            array = default;
        }
    }
}
