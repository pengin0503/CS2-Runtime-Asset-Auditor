using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class UpdateMethodResolverTests
{
    private sealed class OverloadedUpdateSystem
    {
        private void OnUpdate()
        {
        }

        private void OnUpdate(int phase)
        {
            _ = phase;
        }
    }

    [Test]
    public void Resolve_selects_parameterless_OnUpdate_when_overloads_exist()
    {
        var method = UpdateMethodResolver.Resolve(typeof(OverloadedUpdateSystem));

        Assert.That(method, Is.Not.Null);
        Assert.That(method!.GetParameters(), Is.Empty);
    }
}
