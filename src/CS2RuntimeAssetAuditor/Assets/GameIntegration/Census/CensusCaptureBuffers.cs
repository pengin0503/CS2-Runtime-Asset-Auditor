using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Game.Prefabs;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Census
{
    public sealed class CensusCaptureBuffers : IDisposable
    {
        private NativeList<PrefabRef> _topLevelObjectReferences;
        private NativeList<PrefabRef> _subordinateObjectReferences;
        private NativeList<PrefabRef> _networkEdgeReferences;
        private JobHandle _topLevelCaptureHandle;
        private JobHandle _subordinateCaptureHandle;
        private JobHandle _networkCaptureHandle;
        private JobHandle _topLevelDisposeHandle;
        private JobHandle _subordinateDisposeHandle;
        private JobHandle _networkDisposeHandle;
        private bool _topLevelCaptureScheduled;
        private bool _subordinateCaptureScheduled;
        private bool _networkCaptureScheduled;
        private bool _topLevelDisposeScheduled;
        private bool _subordinateDisposeScheduled;
        private bool _networkDisposeScheduled;

        public int TopLevelObjectCount => _topLevelObjectReferences.IsCreated ? _topLevelObjectReferences.Length : 0;

        public int SubordinateObjectCount => _subordinateObjectReferences.IsCreated ? _subordinateObjectReferences.Length : 0;

        public int NetworkEdgeCount => _networkEdgeReferences.IsCreated ? _networkEdgeReferences.Length : 0;

        public NativeList<PrefabRef> TopLevelObjectReferences => _topLevelObjectReferences;

        public NativeList<PrefabRef> SubordinateObjectReferences => _subordinateObjectReferences;

        public NativeList<PrefabRef> NetworkEdgeReferences => _networkEdgeReferences;

        public bool ObjectCaptureJobsCompleted =>
            (_topLevelCaptureScheduled || _subordinateCaptureScheduled)
            && (!_topLevelCaptureScheduled || _topLevelCaptureHandle.IsCompleted)
            && (!_subordinateCaptureScheduled || _subordinateCaptureHandle.IsCompleted);

        public bool NetworkCaptureJobCompleted => _networkCaptureScheduled && _networkCaptureHandle.IsCompleted;

        public bool CleanupPending =>
            (_topLevelDisposeScheduled && !_topLevelDisposeHandle.IsCompleted)
            || (_subordinateDisposeScheduled && !_subordinateDisposeHandle.IsCompleted)
            || (_networkDisposeScheduled && !_networkDisposeHandle.IsCompleted);

        public void ScheduleObjectCapture(EntityQuery topLevelQuery, EntityQuery? subordinateQuery)
        {
            EnsureNoActiveCapture();
            _topLevelObjectReferences = topLevelQuery.ToComponentDataListAsync<PrefabRef>(Allocator.Persistent, out _topLevelCaptureHandle);
            _topLevelCaptureScheduled = true;
            if (subordinateQuery.HasValue)
            {
                _subordinateObjectReferences = subordinateQuery.Value
                    .ToComponentDataListAsync<PrefabRef>(Allocator.Persistent, out _subordinateCaptureHandle);
                _subordinateCaptureScheduled = true;
            }
        }

        public void CompleteObjectCapture()
        {
            if (!ObjectCaptureJobsCompleted)
                throw new InvalidOperationException("Object capture jobs must finish before their buffers are read.");
            if (_topLevelCaptureScheduled)
            {
                _topLevelCaptureHandle.Complete();
                _topLevelCaptureScheduled = false;
                _topLevelCaptureHandle = default;
            }
            if (_subordinateCaptureScheduled)
            {
                _subordinateCaptureHandle.Complete();
                _subordinateCaptureScheduled = false;
                _subordinateCaptureHandle = default;
            }
        }

        public void ScheduleNetworkCapture(EntityQuery networkQuery)
        {
            EnsureNoActiveCapture();
            _networkEdgeReferences = networkQuery.ToComponentDataListAsync<PrefabRef>(Allocator.Persistent, out _networkCaptureHandle);
            _networkCaptureScheduled = true;
        }

        public void CompleteNetworkCapture()
        {
            if (!NetworkCaptureJobCompleted)
                throw new InvalidOperationException("Network capture job must finish before its buffer is read.");
            _networkCaptureHandle.Complete();
            _networkCaptureScheduled = false;
            _networkCaptureHandle = default;
        }

        public void DisposeObjectArrays()
        {
            if (_topLevelCaptureScheduled || _subordinateCaptureScheduled)
                throw new InvalidOperationException("Object capture jobs must be completed before buffers are disposed.");
            DisposeIfCreated(ref _topLevelObjectReferences);
            DisposeIfCreated(ref _subordinateObjectReferences);
        }

        public void DisposeNetworkArray()
        {
            if (_networkCaptureScheduled)
                throw new InvalidOperationException("Network capture job must be completed before its buffer is disposed.");
            DisposeIfCreated(ref _networkEdgeReferences);
        }

        public void RequestDisposal()
        {
            DisposeAfter(ref _topLevelObjectReferences, _topLevelCaptureScheduled, _topLevelCaptureHandle,
                ref _topLevelCaptureScheduled, ref _topLevelCaptureHandle, ref _topLevelDisposeScheduled, ref _topLevelDisposeHandle);
            DisposeAfter(ref _subordinateObjectReferences, _subordinateCaptureScheduled, _subordinateCaptureHandle,
                ref _subordinateCaptureScheduled, ref _subordinateCaptureHandle, ref _subordinateDisposeScheduled, ref _subordinateDisposeHandle);
            DisposeAfter(ref _networkEdgeReferences, _networkCaptureScheduled, _networkCaptureHandle,
                ref _networkCaptureScheduled, ref _networkCaptureHandle, ref _networkDisposeScheduled, ref _networkDisposeHandle);
        }

        public void CompleteCleanup()
        {
            if (CleanupPending)
                throw new InvalidOperationException("Deferred capture-buffer cleanup is still in progress.");
            CompleteDisposeHandle(ref _topLevelDisposeScheduled, ref _topLevelDisposeHandle);
            CompleteDisposeHandle(ref _subordinateDisposeScheduled, ref _subordinateDisposeHandle);
            CompleteDisposeHandle(ref _networkDisposeScheduled, ref _networkDisposeHandle);
        }

        public void Dispose()
        {
            RequestDisposal();
            CompleteDisposeHandle(ref _topLevelDisposeScheduled, ref _topLevelDisposeHandle);
            CompleteDisposeHandle(ref _subordinateDisposeScheduled, ref _subordinateDisposeHandle);
            CompleteDisposeHandle(ref _networkDisposeScheduled, ref _networkDisposeHandle);
        }

        private void EnsureNoActiveCapture()
        {
            if (_topLevelCaptureScheduled || _subordinateCaptureScheduled || _networkCaptureScheduled
                || _topLevelObjectReferences.IsCreated || _subordinateObjectReferences.IsCreated || _networkEdgeReferences.IsCreated
                || _topLevelDisposeScheduled || _subordinateDisposeScheduled || _networkDisposeScheduled)
                throw new InvalidOperationException("Previous Census buffers have not been released.");
        }

        private static void DisposeAfter(
            ref NativeList<PrefabRef> array,
            bool captureScheduled,
            JobHandle captureHandle,
            ref bool scheduledFlag,
            ref JobHandle captureHandleToReset,
            ref bool disposeScheduled,
            ref JobHandle disposeHandle)
        {
            if (captureScheduled)
            {
                if (array.IsCreated)
                {
                    disposeHandle = array.Dispose(captureHandle);
                }
                else
                {
                    disposeHandle = captureHandle;
                }
                disposeScheduled = true;
                array = default;
            }
            else if (array.IsCreated)
            {
                array.Dispose();
                array = default;
            }
            scheduledFlag = false;
            captureHandleToReset = default;
        }

        private static void DisposeIfCreated(ref NativeList<PrefabRef> array)
        {
            if (!array.IsCreated)
                return;
            array.Dispose();
            array = default;
        }

        private static void CompleteDisposeHandle(ref bool scheduled, ref JobHandle handle)
        {
            if (!scheduled)
                return;
            handle.Complete();
            handle = default;
            scheduled = false;
        }
    }
}
