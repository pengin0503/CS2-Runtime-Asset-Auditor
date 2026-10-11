using CS2RuntimeAssetAuditor.Assets.Core.Rendering;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests.Assets;

// Issue #52: a Prefab with a resolved and an unresolved mesh reference is not reported as fully resolved.
public class MeshReferenceCoverageTests
{
    [TestCase(2, 0, 0, RenderCoverage.Supported, null)]
    [TestCase(1, 1, 0, RenderCoverage.Failed, MeshReferenceCoverage.MissingReference)]
    [TestCase(3, 1, 1, RenderCoverage.Failed, MeshReferenceCoverage.MissingReference)]
    [TestCase(1, 0, 1, RenderCoverage.Unknown, MeshReferenceCoverage.UnsupportedReference)]
    [TestCase(0, 0, 2, RenderCoverage.Unknown, MeshReferenceCoverage.UnsupportedReference)]
    [TestCase(0, 2, 0, RenderCoverage.Failed, MeshReferenceCoverage.MissingReference)]
    [TestCase(0, 0, 0, RenderCoverage.Unknown, MeshReferenceCoverage.NoMeshRelations)]
    public void Only_fully_resolved_references_are_supported(int resolved, int missing, int unsupported, RenderCoverage coverage, string? code)
    {
        var result = MeshReferenceCoverage.Classify(resolved, missing, unsupported);
        Assert.That(result.Coverage, Is.EqualTo(coverage));
        Assert.That(result.DiagnosticCode, Is.EqualTo(code));
    }
}
