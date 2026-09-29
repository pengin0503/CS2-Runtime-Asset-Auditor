using System;
using System.Collections.Generic;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    /// <summary>
    /// A least-recently-used cache with a fixed capacity. Asset audits use it for metadata that would
    /// otherwise pin every surface and texture asset object of a large playset in memory.
    /// </summary>
    public sealed class BoundedLruCache<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new LinkedList<KeyValuePair<TKey, TValue>>();

        public BoundedLruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer ?? EqualityComparer<TKey>.Default);
        }

        public int Capacity { get; }
        public int Count => _map.Count;
        public long Hits { get; private set; }
        public long Misses { get; private set; }
        public long Evictions { get; private set; }

        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                Hits++;
                value = node.Value.Value;
                return true;
            }
            Misses++;
            value = default!;
            return false;
        }

        public void Add(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                _map.Remove(key);
            }
            var node = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
            _order.AddFirst(node);
            _map[key] = node;
            while (_map.Count > Capacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
                Evictions++;
            }
        }

        public void Clear()
        {
            _map.Clear();
            _order.Clear();
        }
    }
}
