namespace CS2RuntimeProfiler.Core
{
    public enum MonitoringTransition
    {
        None,
        Disabled,
        Enabled
    }

    public sealed class MonitoringLifecycleGate
    {
        private bool _enabled;

        public MonitoringLifecycleGate(bool initiallyEnabled)
        {
            _enabled = initiallyEnabled;
        }

        public bool IsEnabled => _enabled;

        public MonitoringTransition Observe(bool enabled)
        {
            if (enabled == _enabled)
                return MonitoringTransition.None;

            _enabled = enabled;
            return enabled ? MonitoringTransition.Enabled : MonitoringTransition.Disabled;
        }
    }
}
