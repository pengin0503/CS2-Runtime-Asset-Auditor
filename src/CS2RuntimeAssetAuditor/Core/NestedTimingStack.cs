using System;

namespace CS2RuntimeAssetAuditor.Core
{
    /// <summary>
    /// Tracks nested managed update calls so each system is charged only its own (exclusive) time.
    /// A game system may update other systems from inside its own update; charging the parent its inclusive
    /// time would count every nested child twice in rankings and additive totals.
    /// </summary>
    /// <remarks>
    /// <see cref="Push"/> returns a token (the stack depth after the push). If a nested call throws, its pop never
    /// happens; the next pop of an outer token discards the abandoned frames, whose time stays in the outer frame.
    /// </remarks>
    public sealed class NestedTimingStack
    {
        private long[] _starts = new long[32];
        private long[] _childTicks = new long[32];
        private int _depth;

        public int Depth => _depth;

        public int Push(long timestamp)
        {
            if (_depth == _starts.Length)
            {
                Array.Resize(ref _starts, _depth * 2);
                Array.Resize(ref _childTicks, _depth * 2);
            }
            _starts[_depth] = timestamp;
            _childTicks[_depth] = 0L;
            _depth++;
            return _depth;
        }

        public bool TryPop(int token, long timestamp, out long inclusiveTicks, out long exclusiveTicks)
        {
            inclusiveTicks = 0L;
            exclusiveTicks = 0L;
            if (token <= 0 || token > _depth)
                return false;

            var index = token - 1;
            inclusiveTicks = Math.Max(0L, timestamp - _starts[index]);
            exclusiveTicks = Math.Max(0L, inclusiveTicks - _childTicks[index]);
            _depth = index;
            if (_depth > 0)
                _childTicks[_depth - 1] += inclusiveTicks;
            return true;
        }

        public void Reset() => _depth = 0;
    }
}
