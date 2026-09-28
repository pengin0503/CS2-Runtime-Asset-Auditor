using System;

namespace CS2RuntimeAssetAuditor.Assets.Core.Observations
{
    public sealed class Observation<T>
    {
        private readonly T _value;

        private Observation(
            Availability availability,
            ObservationOrigin origin,
            DateTimeOffset capturedAt,
            T value,
            string? diagnosticCode)
        {
            Availability = availability;
            Origin = origin;
            CapturedAt = capturedAt;
            _value = value;
            DiagnosticCode = diagnosticCode;
        }

        public Availability Availability { get; }

        public ObservationOrigin Origin { get; }

        public DateTimeOffset CapturedAt { get; }

        public string? DiagnosticCode { get; }

        public bool HasValue => Availability == Availability.Available;

        public T Value
        {
            get
            {
                if (!HasValue)
                    throw new InvalidOperationException("This observation has no available value.");
                return _value;
            }
        }

        public bool TryGetValue(out T value)
        {
            if (HasValue)
            {
                value = _value;
                return true;
            }

            value = default!;
            return false;
        }

        public static Observation<T> FromValue(T value, ObservationOrigin origin, DateTimeOffset capturedAt)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));
            return new Observation<T>(Availability.Available, origin, capturedAt, value, null);
        }

        public static Observation<T> Unavailable(
            Availability availability,
            ObservationOrigin origin,
            DateTimeOffset capturedAt,
            string? diagnosticCode = null)
        {
            if (availability == Availability.Available)
                throw new ArgumentException("Use FromValue for available observations.", nameof(availability));
            if (diagnosticCode != null && availability != Availability.Failed)
                throw new ArgumentException("A diagnostic code is only valid for a failed observation.", nameof(diagnosticCode));
            return new Observation<T>(availability, origin, capturedAt, default!, diagnosticCode);
        }
    }
}
