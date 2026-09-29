using System;
using Colossal.Serialization.Entities;
using CS2RuntimeAssetAuditor.Core.Loading;
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
            // A gameplay load that never reported completion (a failed load returning to the menu, or another
            // load started over it) is kept as interrupted rather than discarded, so it can still be exported.
            if (Mod.LoadingTrace.IsLoading)
            {
                Mod.LoadingMetrics?.CaptureNow();
                Mod.LoadingTrace.Interrupt(DateTimeOffset.UtcNow, $"nextLoadStarted:{mode}/{purpose}");
                LogSummary();
            }
            if (mode == GameMode.Game)
            {
                Mod.LoadingTrace.Begin(DateTimeOffset.UtcNow, purpose.ToString());
                Mod.LoadingMetrics?.Begin();
                LogProgress(LoadingTraceRecorder.LoadStartedMilestone);
            }
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            if (!Mod.LoadingTrace.IsLoading) return;
            var milestone = serializationContext.purpose == Purpose.LoadGame ? "saveRestored" : "gameLoaded";
            Mod.LoadingTrace.Mark(milestone, DateTimeOffset.UtcNow);
            LogProgress(milestone);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (Mod.LoadingTrace.IsLoading)
            {
                Mod.LoadingMetrics?.CaptureNow();
                if (mode == GameMode.Game)
                    Mod.LoadingTrace.Complete(DateTimeOffset.UtcNow);
                else
                    Mod.LoadingTrace.Interrupt(DateTimeOffset.UtcNow, $"loadingCompletedAs:{mode}/{purpose}");
                LogSummary();
            }
            if (mode == GameMode.Game)
            {
                var context = Mod.Sessions.Begin(UnityEngine.Application.version, BuildIdentityProvider.Current, DateTimeOffset.UtcNow);
                Mod.Info($"Diagnostic city session started: {context.SessionId} {Mod.LoggerState}");
            }
            else
            {
                Mod.Sessions.Close();
            }
        }

        private static void LogProgress(string milestone) =>
            Mod.Info(LoadingTraceLogFormatter.FormatProgress(Mod.LoadingTrace.Snapshot(), milestone));

        private static void LogSummary() =>
            Mod.Info(LoadingTraceLogFormatter.FormatSummary(Mod.LoadingTrace.Snapshot()));
    }
}
