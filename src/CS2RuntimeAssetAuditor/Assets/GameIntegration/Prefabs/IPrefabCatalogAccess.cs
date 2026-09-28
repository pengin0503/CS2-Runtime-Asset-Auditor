using System.Collections.Generic;
using System;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Prefabs
{
    public interface IPrefabCatalogAccess
    {
        bool IsWorking { get; }

        bool HasPendingItems { get; }

        bool HasPendingPublication { get; }

        int CapturedEntityCount { get; }

        int ProcessedEntityCount { get; }

        int UnresolvedEntityCount { get; }

        long CatalogGeneration { get; }

        long PendingCatalogGeneration { get; }

        // Number of captures that completed and were published (or committed). Unlike CatalogGeneration,
        // it advances even when a capture found the catalog unchanged.
        long CompletedCaptureCount { get; }

        DateTimeOffset CatalogCapturedAt { get; }

        DateTimeOffset PendingCapturedAt { get; }

        IReadOnlyList<PrefabRecord> PublishedRecords { get; }

        IReadOnlyDictionary<Entity, PrefabKey> RuntimeEntityKeys { get; }

        IReadOnlyList<PrefabRecord> PendingRecords { get; }

        IReadOnlyDictionary<Entity, PrefabKey> PendingRuntimeEntityKeys { get; }

        void BeginCapture(bool deferPublication = false);

        int ProcessNextSlice(int maximumItems);

        void CommitPendingCapture();

        void DiscardPendingCapture();

        void CancelCapture();

        void ResetForWorld();
    }
}
