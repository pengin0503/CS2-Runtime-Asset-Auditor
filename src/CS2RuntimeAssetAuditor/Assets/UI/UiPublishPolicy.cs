using System;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    public enum UiPublishDecision
    {
        Skip,
        Publish
    }

    // Decides when the UI snapshot is rebuilt. Building it maps the asset page and every finding and serializes
    // the result, so it runs only when something visible can have changed, and while a scan is running at most
    // once per progress interval.
    public static class UiPublishPolicy
    {
        /// <summary>
        /// Nothing is built while the panel is hidden; changes observed meanwhile are deferred by the caller and
        /// published once as soon as the panel is shown again.
        /// </summary>
        public static UiPublishDecision Decide(bool panelVisible, bool deferredChanges, bool dataChanged, bool statusChanged,
            bool scanActive, TimeSpan sinceLastPublish, TimeSpan progressInterval)
        {
            if (!panelVisible)
                return UiPublishDecision.Skip;
            if (deferredChanges)
                return UiPublishDecision.Publish;
            return Decide(dataChanged, statusChanged, scanActive, sinceLastPublish, progressInterval);
        }

        public static UiPublishDecision Decide(bool dataChanged, bool statusChanged, bool scanActive, TimeSpan sinceLastPublish, TimeSpan progressInterval)
        {
            if (dataChanged)
                return UiPublishDecision.Publish;
            if (!statusChanged)
                return UiPublishDecision.Skip;
            if (scanActive && sinceLastPublish < progressInterval)
                return UiPublishDecision.Skip;
            return UiPublishDecision.Publish;
        }
    }
}
