using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CS2RuntimeAssetAuditor.Assets.Core.Scanning
{
    /// <summary>
    /// Receives the reads of a <see cref="NestedReadCursor{TOwner,TSurface,TTexture}"/>. A read that throws is
    /// reported to the matching Failed method and the walk continues with the next item.
    /// </summary>
    public interface INestedReadVisitor<TOwner, TSurface, TTexture>
    {
        /// <summary>Starts an owner and returns its surfaces (null when it has none).</summary>
        IReadOnlyList<TSurface>? BeginOwner(TOwner owner);

        /// <summary>Reads one surface and returns its textures (null when it has none).</summary>
        IReadOnlyList<TTexture>? ReadSurface(TOwner owner, TSurface surface);

        void ReadTexture(TOwner owner, TTexture texture);
        void OwnerFailed(TOwner owner, Exception error);
        void SurfaceFailed(TOwner owner, TSurface surface, Exception error);
        void TextureFailed(TOwner owner, TTexture texture, Exception error);

        /// <summary>Called once per owner after its last surface and texture, including owners that failed.</summary>
        void CompleteOwner(TOwner owner);
    }

    /// <summary>
    /// Walks owners, their surfaces and each surface's textures one read at a time and can stop between any two
    /// reads, so a frame budget applies inside a render asset with many surfaces or textures, not only between
    /// render assets. Each slice makes at least one read; a single read is indivisible, so a slice can still
    /// exceed the budget by the length of that read, which <see cref="MaxStepMilliseconds"/> records.
    /// </summary>
    public sealed class NestedReadCursor<TOwner, TSurface, TTexture>
        where TSurface : class
        where TTexture : class
    {
        private readonly IReadOnlyList<TOwner> _owners;
        private readonly INestedReadVisitor<TOwner, TSurface, TTexture> _visitor;
        private readonly Func<double> _clockMilliseconds;
        private int _ownerIndex;
        private bool _ownerOpen;
        private IReadOnlyList<TSurface>? _surfaces;
        private int _surfaceIndex;
        private IReadOnlyList<TTexture>? _textures;
        private int _textureIndex;

        public NestedReadCursor(
            IReadOnlyList<TOwner> owners,
            INestedReadVisitor<TOwner, TSurface, TTexture> visitor,
            Func<double>? clockMilliseconds = null)
        {
            _owners = owners ?? throw new ArgumentNullException(nameof(owners));
            _visitor = visitor ?? throw new ArgumentNullException(nameof(visitor));
            _clockMilliseconds = clockMilliseconds ?? StopwatchMilliseconds;
        }

        public bool IsComplete => _ownerIndex >= _owners.Count;
        public int CompletedOwners => _ownerIndex;
        public long Steps { get; private set; }
        public double MaxStepMilliseconds { get; private set; }

        /// <summary>Makes reads until the budget is spent or the walk ends; returns true when every owner is complete.</summary>
        public bool Advance(double budgetMilliseconds)
        {
            var start = _clockMilliseconds();
            var steps = 0;
            while (!IsComplete && (steps == 0 || _clockMilliseconds() - start < budgetMilliseconds))
            {
                var stepStart = _clockMilliseconds();
                Step();
                steps++;
                var stepMilliseconds = _clockMilliseconds() - stepStart;
                if (stepMilliseconds > MaxStepMilliseconds)
                    MaxStepMilliseconds = stepMilliseconds;
            }
            Steps += steps;
            return IsComplete;
        }

        private void Step()
        {
            var owner = _owners[_ownerIndex];
            if (!_ownerOpen)
            {
                _ownerOpen = true;
                _surfaceIndex = 0;
                _textures = null;
                try { _surfaces = _visitor.BeginOwner(owner); }
                catch (Exception ex)
                {
                    _surfaces = null;
                    _visitor.OwnerFailed(owner, ex);
                }
                return;
            }

            if (_textures != null && _textureIndex < _textures.Count)
            {
                var texture = _textures[_textureIndex++];
                if (texture == null)
                    return;
                try { _visitor.ReadTexture(owner, texture); }
                catch (Exception ex) { _visitor.TextureFailed(owner, texture, ex); }
                return;
            }
            _textures = null;

            if (_surfaces != null && _surfaceIndex < _surfaces.Count)
            {
                var surface = _surfaces[_surfaceIndex++];
                if (surface == null)
                    return;
                try
                {
                    _textures = _visitor.ReadSurface(owner, surface);
                    _textureIndex = 0;
                }
                catch (Exception ex) { _visitor.SurfaceFailed(owner, surface, ex); }
                return;
            }

            _surfaces = null;
            _ownerOpen = false;
            _ownerIndex++;
            _visitor.CompleteOwner(owner);
        }

        private static double StopwatchMilliseconds() => Stopwatch.GetTimestamp() * 1000d / Stopwatch.Frequency;
    }
}
