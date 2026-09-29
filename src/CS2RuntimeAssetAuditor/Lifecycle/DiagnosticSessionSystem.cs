using System;
using Colossal.Serialization.Entities;
using CS2RuntimeAssetAuditor.Export;
using Game;

namespace CS2RuntimeAssetAuditor.Lifecycle
{
    /// <summary>
    /// Starts a new diagnostic city session whenever a gameplay city finishes loading and closes it when
    /// another load begins. The game keeps one World alive across loads, so World identity cannot mark
    /// session boundaries; runtime and asset systems observe <see cref="Coordination.DiagnosticSessionRegistry.Generation"/>
    /// and drop state that belongs to an earlier city.
    /// </summary>
    public sealed partial class DiagnosticSessionSystem : GameSystemBase
    {
        protected override void OnUpdate() { }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Mod.Sessions.Close();
            if (mode == GameMode.Game)
            {
                Mod.LoadingTrace.Begin(DateTimeOffset.UtcNow, purpose.ToString());
                World.GetOrCreateSystemManaged<LoadingMetricsSystem>().Begin();
            }
            else
            {
                Mod.LoadingTrace.Clear();
            }
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            if (Mod.LoadingTrace.IsLoading)
                Mod.LoadingTrace.Mark(serializationContext.purpose == Purpose.LoadGame ? "saveRestored" : "gameLoaded",
                    DateTimeOffset.UtcNow);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game)
            {
                World.GetOrCreateSystemManaged<LoadingMetricsSystem>().CaptureNow();
                Mod.LoadingTrace.Complete(DateTimeOffset.UtcNow, cityOperable: true);
            }
            if (mode == GameMode.Game)
            {
                var context = Mod.Sessions.Begin(UnityEngine.Application.version, BuildIdentityProvider.Current, DateTimeOffset.UtcNow);
                Mod.Log.Info($"Diagnostic city session started: {context.SessionId}");
            }
            else
            {
                Mod.Sessions.Close();
            }
        }
    }
}
