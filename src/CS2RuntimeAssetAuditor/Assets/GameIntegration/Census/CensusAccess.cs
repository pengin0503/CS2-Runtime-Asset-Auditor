using System;
using System.Collections.Generic;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.Vehicles;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Census
{
    public sealed class CensusAccess : IDisposable
    {
        private readonly World _world;
        private readonly CensusCaptureBuffers _buffers = new CensusCaptureBuffers();
        private EntityQuery? _topLevelObjectQuery;
        private EntityQuery? _subordinateObjectQuery;
        private EntityQuery? _networkEdgeQuery;
        private IReadOnlyDictionary<Entity, PrefabKey> _catalogKeys = new Dictionary<Entity, PrefabKey>();
        private CensusReducer? _reducer;
        private ScanOptions? _scanOptions;
        private int _topLevelIndex;
        private int _subordinateIndex;
        private int _networkIndex;
        private bool _objectQueriesReady;
        private bool _networkQueryReady;

        public CensusAccess(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public int CapturedObjectReferenceCount => _buffers.TopLevelObjectCount + _buffers.SubordinateObjectCount;

        public int CapturedNetworkEdgeCount => _buffers.NetworkEdgeCount;

        public int ProcessedObjectReferenceCount { get; private set; }

        public int ProcessedNetworkEdgeCount { get; private set; }

        public int UnmatchedPrefabReferenceCount { get; private set; }

        public bool ObjectCaptureJobsCompleted => _buffers.ObjectCaptureJobsCompleted;

        public bool NetworkCaptureJobCompleted => _buffers.NetworkCaptureJobCompleted;

        public bool NetworkCaptureJobsCompleted => _scanOptions?.CollectNetworkEdges != true || _buffers.NetworkCaptureJobCompleted;

        public bool ObjectReductionCompleted => _objectQueriesReady
            && _topLevelIndex >= _buffers.TopLevelObjectCount
            && _subordinateIndex >= _buffers.SubordinateObjectCount;

        public bool NetworkReductionCompleted => _networkQueryReady && _networkIndex >= _buffers.NetworkEdgeCount;

        public bool CleanupPending => _buffers.CleanupPending;

        public void BeginObjectCapture(
            IReadOnlyDictionary<Entity, PrefabKey> catalogKeys,
            CensusReducer reducer,
            ScanOptions scanOptions)
        {
            if (catalogKeys == null)
                throw new ArgumentNullException(nameof(catalogKeys));
            if (reducer == null)
                throw new ArgumentNullException(nameof(reducer));
            if (scanOptions == null)
                throw new ArgumentNullException(nameof(scanOptions));
            if (!_world.IsCreated)
                throw new InvalidOperationException("The current game world is no longer available.");

            _catalogKeys = catalogKeys;
            _reducer = reducer;
            _scanOptions = scanOptions;
            _topLevelIndex = 0;
            _subordinateIndex = 0;
            ProcessedObjectReferenceCount = 0;
            UnmatchedPrefabReferenceCount = 0;
            try
            {
                var exclusions = new[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Overridden>()
                };
                var common = new[]
                {
                    ComponentType.ReadOnly<Game.Objects.Object>(),
                    ComponentType.ReadOnly<PrefabRef>()
                };
                var topLevelExclusions = new[]
                {
                    exclusions[0], exclusions[1], exclusions[2],
                    ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>()
                };
                _topLevelObjectQuery = CreateQuery(common, null, topLevelExclusions);

                EntityQuery? subordinateQuery = null;
                if (scanOptions.CollectSubordinateObjects)
                {
                    subordinateQuery = CreateQuery(common,
                        new[] { ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<Controller>() },
                        exclusions);
                    _subordinateObjectQuery = subordinateQuery;
                }

                _buffers.ScheduleObjectCapture(_topLevelObjectQuery.Value, subordinateQuery);
            }
            catch
            {
                _buffers.RequestDisposal();
                if (!_buffers.CleanupPending)
                    _buffers.CompleteCleanup();
                throw;
            }
        }

        public void CompleteObjectCapture()
        {
            _buffers.CompleteObjectCapture();
            _objectQueriesReady = true;
        }

        public int ReduceObjectSlice(int maximumItems)
        {
            if (maximumItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (!_objectQueriesReady || _reducer == null)
                throw new InvalidOperationException("Object capture must complete before reduction.");

            var processed = 0;
            while (_topLevelIndex < _buffers.TopLevelObjectCount && processed < maximumItems)
            {
                AddObjectReference(_buffers.TopLevelObjectReferences[_topLevelIndex++], isSubordinate: false);
                ProcessedObjectReferenceCount++;
                processed++;
            }
            while (_subordinateIndex < _buffers.SubordinateObjectCount && processed < maximumItems)
            {
                AddObjectReference(_buffers.SubordinateObjectReferences[_subordinateIndex++], isSubordinate: true);
                ProcessedObjectReferenceCount++;
                processed++;
            }

            return processed;
        }

        public void ReleaseObjectCapture()
        {
            _buffers.DisposeObjectArrays();
            DisposeQuery(ref _topLevelObjectQuery);
            DisposeQuery(ref _subordinateObjectQuery);
            _objectQueriesReady = false;
            _topLevelIndex = 0;
            _subordinateIndex = 0;
        }

        public void BeginNetworkCapture()
        {
            _networkIndex = 0;
            ProcessedNetworkEdgeCount = 0;
            _networkQueryReady = false;
            if (_scanOptions?.CollectNetworkEdges != true)
            {
                _networkQueryReady = true;
                return;
            }
            if (!_world.IsCreated)
                throw new InvalidOperationException("The current game world is no longer available.");

            var exclusions = new[]
            {
                ComponentType.ReadOnly<Temp>(),
                ComponentType.ReadOnly<Deleted>(),
                ComponentType.ReadOnly<Overridden>(),
                ComponentType.ReadOnly<Owner>(),
                ComponentType.ReadOnly<Controller>()
            };
            var all = new[]
            {
                ComponentType.ReadOnly<Game.Net.Edge>(),
                ComponentType.ReadOnly<PrefabRef>()
            };
            _networkEdgeQuery = CreateQuery(all, null, exclusions);
            _buffers.ScheduleNetworkCapture(_networkEdgeQuery.Value);
        }

        public void CompleteNetworkCapture()
        {
            if (_scanOptions?.CollectNetworkEdges == true)
            {
                _buffers.CompleteNetworkCapture();
                _networkQueryReady = true;
            }
        }

        public int ReduceNetworkSlice(int maximumItems)
        {
            if (maximumItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumItems));
            if (!_networkQueryReady || _reducer == null)
                throw new InvalidOperationException("Network capture must complete before reduction.");
            if (_scanOptions?.CollectNetworkEdges != true)
                return 0;

            var processed = 0;
            while (_networkIndex < _buffers.NetworkEdgeCount && processed < maximumItems)
            {
                var prefabReference = _buffers.NetworkEdgeReferences[_networkIndex++];
                if (_catalogKeys.TryGetValue(prefabReference.m_Prefab, out var key))
                    CensusSample.ForNetworkEdge(key).AddTo(_reducer);
                else
                    UnmatchedPrefabReferenceCount++;
                ProcessedNetworkEdgeCount++;
                processed++;
            }
            return processed;
        }

        public void ReleaseNetworkCapture()
        {
            if (_scanOptions?.CollectNetworkEdges == true)
            {
                _buffers.DisposeNetworkArray();
                DisposeQuery(ref _networkEdgeQuery);
            }
            _networkQueryReady = false;
            _networkIndex = 0;
        }

        public void RequestCancellation()
        {
            _buffers.RequestDisposal();
            if (!_buffers.CleanupPending)
                CompleteCleanup();
        }

        public void CompleteCleanup()
        {
            _buffers.CompleteCleanup();
            DisposeQuery(ref _topLevelObjectQuery);
            DisposeQuery(ref _subordinateObjectQuery);
            DisposeQuery(ref _networkEdgeQuery);
            _objectQueriesReady = false;
            _networkQueryReady = false;
            _catalogKeys = new Dictionary<Entity, PrefabKey>();
            _reducer = null;
            _scanOptions = null;
            _topLevelIndex = 0;
            _subordinateIndex = 0;
            _networkIndex = 0;
        }

        public void FinishScan()
        {
            _catalogKeys = new Dictionary<Entity, PrefabKey>();
            _reducer = null;
            _scanOptions = null;
            _topLevelIndex = 0;
            _subordinateIndex = 0;
            _networkIndex = 0;
            _objectQueriesReady = false;
            _networkQueryReady = false;
        }

        public CensusSnapshot BuildSnapshot()
        {
            return _reducer?.BuildSnapshot() ?? throw new InvalidOperationException("No Census reducer is active.");
        }

        public void Dispose()
        {
            _buffers.Dispose();
            DisposeQuery(ref _topLevelObjectQuery);
            DisposeQuery(ref _subordinateObjectQuery);
            DisposeQuery(ref _networkEdgeQuery);
            _catalogKeys = new Dictionary<Entity, PrefabKey>();
            _reducer = null;
            _scanOptions = null;
        }

        private void AddObjectReference(PrefabRef prefabReference, bool isSubordinate)
        {
            if (_catalogKeys.TryGetValue(prefabReference.m_Prefab, out var key))
                CensusSample.ForObject(key, isSubordinate).AddTo(_reducer!);
            else
                UnmatchedPrefabReferenceCount++;
        }

        private EntityQuery CreateQuery(ComponentType[] all, ComponentType[]? any, ComponentType[] none)
        {
            var description = new EntityQueryDesc
            {
                All = all,
                Any = any,
                None = none
            };
            return _world.EntityManager.CreateEntityQuery(new[] { description });
        }

        private static void DisposeQuery(ref EntityQuery? query)
        {
            if (!query.HasValue)
                return;
            query.Value.Dispose();
            query = null;
        }
    }
}
