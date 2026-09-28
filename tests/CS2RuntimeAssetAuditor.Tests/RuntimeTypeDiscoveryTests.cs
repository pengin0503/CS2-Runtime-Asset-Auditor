using System.Reflection;
using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class RuntimeTypeDiscoveryTests
{
    [Test]
    public void Assembly_enumeration_failure_isolated_to_that_assembly()
    {
        var assembly = typeof(RuntimeTypeDiscoveryTests).Assembly;
        var types = RuntimeTypeDiscovery.Enumerate(
            new[] { assembly },
            _ => throw new InvalidOperationException("blocked"));

        Assert.That(types, Is.Empty);
    }

    [Test]
    public void Reflection_type_load_exception_preserves_successfully_loaded_types()
    {
        var assembly = typeof(RuntimeTypeDiscoveryTests).Assembly;
        var exception = new ReflectionTypeLoadException(
            new Type?[] { typeof(string), null },
            new Exception[] { new TypeLoadException("missing") });

        var types = RuntimeTypeDiscovery.Enumerate(
            new[] { assembly },
            _ => throw exception);

        Assert.That(types, Is.EqualTo(new[] { typeof(string) }));
    }

    [Test]
    public void Partial_assembly_failure_preserves_loaded_types_and_reports_incomplete_discovery()
    {
        var assembly = typeof(RuntimeTypeDiscoveryTests).Assembly;
        var exception = new ReflectionTypeLoadException(
            new Type?[] { typeof(string), null },
            new Exception[] { new TypeLoadException("missing dependency") });

        var types = RuntimeTypeDiscovery.EnumerateWithStatus(
            new[] { assembly },
            out var isComplete,
            out var error,
            _ => throw exception);

        Assert.Multiple(() =>
        {
            Assert.That(types, Is.EqualTo(new[] { typeof(string) }));
            Assert.That(isComplete, Is.False);
            Assert.That(error, Does.Contain("missing dependency"));
        });
    }
}
