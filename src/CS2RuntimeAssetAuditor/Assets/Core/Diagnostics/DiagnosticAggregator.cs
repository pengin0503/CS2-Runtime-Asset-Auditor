using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2RuntimeAssetAuditor.Assets.Core.Diagnostics
{
    public sealed class DiagnosticAggregate
    {
        public DiagnosticAggregate(DiagnosticCode code, string message, int count, DateTimeOffset firstSeenAt, DateTimeOffset lastSeenAt)
        {
            Code = code;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Count = count;
            FirstSeenAt = firstSeenAt;
            LastSeenAt = lastSeenAt;
        }

        public DiagnosticCode Code { get; }
        public string Message { get; }
        public int Count { get; }
        public DateTimeOffset FirstSeenAt { get; }
        public DateTimeOffset LastSeenAt { get; }
    }

    public sealed class DiagnosticAggregator
    {
        private sealed class MutableAggregate
        {
            public MutableAggregate(DiagnosticCode code, string message, DateTimeOffset seenAt)
            {
                Code = code;
                Message = message;
                Count = 1;
                FirstSeenAt = seenAt;
                LastSeenAt = seenAt;
            }

            public DiagnosticCode Code { get; }
            public string Message { get; }
            public int Count { get; set; }
            public DateTimeOffset FirstSeenAt { get; }
            public DateTimeOffset LastSeenAt { get; set; }
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, MutableAggregate> _items = new Dictionary<string, MutableAggregate>(StringComparer.Ordinal);

        public int DistinctCount
        {
            get
            {
                lock (_gate)
                    return _items.Count;
            }
        }

        public long OccurrenceCount
        {
            get
            {
                lock (_gate)
                {
                    long total = 0;
                    foreach (var item in _items.Values)
                        total = checked(total + item.Count);
                    return total;
                }
            }
        }

        public void Add(string code, string message, DateTimeOffset? seenAt = null)
        {
            Add(new DiagnosticCode(code), message, seenAt ?? DateTimeOffset.UtcNow);
        }

        public void Add(DiagnosticCode code, string message, DateTimeOffset seenAt)
        {
            if (!code.IsValid)
                throw new ArgumentException("A valid diagnostic code is required.", nameof(code));
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var key = code.Value + "\0" + message;
            lock (_gate)
            {
                if (_items.TryGetValue(key, out var aggregate))
                {
                    aggregate.Count = checked(aggregate.Count + 1);
                    aggregate.LastSeenAt = seenAt;
                }
                else
                {
                    _items.Add(key, new MutableAggregate(code, message, seenAt));
                }
            }
        }

        public IReadOnlyList<DiagnosticAggregate> Snapshot()
        {
            lock (_gate)
            {
                return Array.AsReadOnly(_items.Values
                    .OrderBy(item => item.Code.Value, StringComparer.Ordinal)
                    .ThenBy(item => item.Message, StringComparer.Ordinal)
                    .Select(item => new DiagnosticAggregate(item.Code, item.Message, item.Count, item.FirstSeenAt, item.LastSeenAt))
                    .ToArray());
            }
        }
    }
}
