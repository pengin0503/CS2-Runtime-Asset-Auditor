using System;

namespace CS2RuntimeAssetAuditor.Core
{
    [Flags]
    public enum PanelBindingUpdate
    {
        None = 0,
        /// <summary>The open state (and the UI scale that goes with it).</summary>
        Visibility = 1,
        /// <summary>The lightweight HUD snapshot shown while the panel is closed.</summary>
        Hud = 2,
        /// <summary>The full panel snapshot; costly, so only while the panel is open.</summary>
        Snapshot = 4
    }

    /// <summary>
    /// The panel's open state and which bindings each event refreshes. Setting the state is idempotent, so a close
    /// request (the game's Back/Escape input action) delivered twice can never reopen the panel. A closed panel only
    /// refreshes the HUD; opening it rebuilds the full snapshot at once instead of waiting for the next refresh.
    /// </summary>
    public sealed class PanelVisibility
    {
        public bool IsVisible { get; private set; }

        /// <summary>Returns true when the state changed.</summary>
        public bool Set(bool visible)
        {
            if (IsVisible == visible)
                return false;
            IsVisible = visible;
            return true;
        }

        /// <summary>The bindings to update for a visibility request; none when the state did not change.</summary>
        public PanelBindingUpdate Request(bool visible)
            => Set(visible) ? PanelBindingUpdate.Visibility | RefreshTick() : PanelBindingUpdate.None;

        /// <summary>The bindings to update on a periodic refresh.</summary>
        public PanelBindingUpdate RefreshTick()
            => PanelBindingUpdate.Hud | (IsVisible ? PanelBindingUpdate.Snapshot : PanelBindingUpdate.None);
    }
}
