using System;

namespace CS2RuntimeProfiler.Core
{
    public sealed class DeepCaptureStateMachine
    {
        private double _efficiencyThreshold;
        private double _sustainSeconds;
        private double _deepSeconds;
        private double _postSeconds;
        private double _cooldownSeconds;
        private bool _automaticCaptureEnabled;

        private double? _lowEfficiencySince;
        private double _stateEnteredAt;

        public DeepCaptureStateMachine(
            double efficiencyThreshold,
            double sustainSeconds,
            double deepSeconds,
            double postSeconds,
            double cooldownSeconds)
        {
            Configure(
                efficiencyThreshold,
                sustainSeconds,
                deepSeconds,
                postSeconds,
                cooldownSeconds,
                automaticCaptureEnabled: true);
            State = CaptureState.Monitoring;
        }

        public CaptureState State { get; private set; }
        public CaptureTrigger LastTrigger { get; private set; }

        public static DeepCaptureStateMachine CreateDefault()
        {
            return new DeepCaptureStateMachine(
                efficiencyThreshold: 0.80,
                sustainSeconds: 2,
                deepSeconds: 10,
                postSeconds: 5,
                cooldownSeconds: 30);
        }

        public void Configure(
            double efficiencyThreshold,
            double sustainSeconds,
            double deepSeconds,
            double postSeconds,
            double cooldownSeconds,
            bool automaticCaptureEnabled)
        {
            _efficiencyThreshold = Clamp(efficiencyThreshold, 0.01d, 1d);
            _sustainSeconds = Math.Max(0.1d, sustainSeconds);
            _deepSeconds = Math.Max(0.1d, deepSeconds);
            _postSeconds = Math.Max(0d, postSeconds);
            _cooldownSeconds = Math.Max(0d, cooldownSeconds);
            _automaticCaptureEnabled = automaticCaptureEnabled;

            if (!_automaticCaptureEnabled)
                _lowEfficiencySince = null;
        }

        public void Observe(double nowSeconds, double selectedSpeed, double actualSpeed)
        {
            Observe(nowSeconds, selectedSpeed, actualSpeed, automaticTriggerAllowed: true);
        }

        public void Observe(
            double nowSeconds,
            double selectedSpeed,
            double actualSpeed,
            bool automaticTriggerAllowed)
        {
            AdvanceTimedStates(nowSeconds);

            if (State != CaptureState.Monitoring)
                return;

            if (!_automaticCaptureEnabled || !automaticTriggerAllowed)
            {
                _lowEfficiencySince = null;
                return;
            }

            if (selectedSpeed <= 0)
            {
                _lowEfficiencySince = null;
                return;
            }

            var efficiency = SimulationEfficiency.Calculate(selectedSpeed, actualSpeed);
            if (efficiency >= _efficiencyThreshold)
            {
                _lowEfficiencySince = null;
                return;
            }

            if (!_lowEfficiencySince.HasValue)
            {
                _lowEfficiencySince = nowSeconds;
                return;
            }

            if (nowSeconds - _lowEfficiencySince.Value >= _sustainSeconds)
                StartCapture(nowSeconds, new CaptureTrigger(CaptureTriggerKind.AutomaticLowEfficiency, nowSeconds, efficiency));
        }

        public void RequestManualCapture(double nowSeconds)
        {
            if (State != CaptureState.Monitoring && State != CaptureState.Cooldown)
                return;

            StartCapture(nowSeconds, new CaptureTrigger(CaptureTriggerKind.Manual, nowSeconds, null));
        }

        public void ResetToMonitoring()
        {
            State = CaptureState.Monitoring;
            _stateEnteredAt = 0d;
            _lowEfficiencySince = null;
        }

        private void StartCapture(double nowSeconds, CaptureTrigger trigger)
        {
            State = CaptureState.DeepCapture;
            _stateEnteredAt = nowSeconds;
            _lowEfficiencySince = null;
            LastTrigger = trigger;
        }

        private void AdvanceTimedStates(double nowSeconds)
        {
            var advanced = true;
            while (advanced)
            {
                advanced = false;

                if (State == CaptureState.DeepCapture && nowSeconds - _stateEnteredAt >= _deepSeconds)
                {
                    _stateEnteredAt += _deepSeconds;
                    State = CaptureState.PostBuffer;
                    advanced = true;
                }
                else if (State == CaptureState.PostBuffer && nowSeconds - _stateEnteredAt >= _postSeconds)
                {
                    _stateEnteredAt += _postSeconds;
                    State = CaptureState.Cooldown;
                    advanced = true;
                }
                else if (State == CaptureState.Cooldown && nowSeconds - _stateEnteredAt >= _cooldownSeconds)
                {
                    _stateEnteredAt += _cooldownSeconds;
                    State = CaptureState.Monitoring;
                    _lowEfficiencySince = null;
                    advanced = true;
                }
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
