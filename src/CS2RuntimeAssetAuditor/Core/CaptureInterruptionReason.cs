namespace CS2RuntimeAssetAuditor.Core
{
    public enum CaptureInterruptionReason
    {
        /// <summary>An explicit caller request; monitoring resumes immediately.</summary>
        Requested,
        /// <summary>The user disabled monitoring; monitoring resumes immediately when re-enabled.</summary>
        MonitoringDisabled,
        /// <summary>The loaded city changed; the next city starts without a cooldown.</summary>
        SessionChanged,
        /// <summary>A profiler overhead or memory safety limit stopped the capture; a cooldown with backoff follows.</summary>
        SafetyLimit
    }
}
