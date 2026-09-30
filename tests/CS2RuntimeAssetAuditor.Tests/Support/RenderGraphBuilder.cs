using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Rendering;

namespace CS2RuntimeAssetAuditor.Assets.GameIntegration.Rendering
{
    // Test support only: the mod feeds RenderGraphAccumulator incrementally from AssetAnalysisCollector.
    public sealed class RenderGraphBuilder
    {
        private readonly IReadOnlyList<IRenderAssetResolver> _resolvers;
        public RenderGraphBuilder(IEnumerable<IRenderAssetResolver> resolvers)
        {
            if (resolvers == null) throw new ArgumentNullException(nameof(resolvers));
            _resolvers = Array.AsReadOnly(resolvers.ToArray());
        }

        public RenderGraphSnapshot Build(IEnumerable<RenderGraphInput> inputs)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            var accumulator = new RenderGraphAccumulator(_resolvers);
            foreach (var input in inputs)
            {
                if (input == null) throw new ArgumentException("Render graph inputs cannot contain null entries.", nameof(inputs));
                accumulator.Add(input);
            }
            return accumulator.Snapshot();
        }
    }
}
