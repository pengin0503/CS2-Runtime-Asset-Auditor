using CS2RuntimeProfiler.Collectors;
using CS2RuntimeProfiler.Core;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class ReflectionMetricAccessorTests
{
    private sealed class FakeTarget
    {
        public int Present = 12;
        public string Text => "not numeric";
        public int Throwing => throw new InvalidOperationException("boom");
    }

    [Test]
    public void Missing_member_returns_unavailable_instead_of_throwing()
    {
        var accessor = ReflectionMetricAccessor.Create(typeof(FakeTarget), "DoesNotExist");
        var result = accessor.TryRead(new FakeTarget());

        Assert.That(result.Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Reason, Does.Contain("DoesNotExist"));
    }

    [Test]
    public void Existing_numeric_member_is_read_without_mutation()
    {
        var accessor = ReflectionMetricAccessor.Create(typeof(FakeTarget), "Present");
        var target = new FakeTarget();
        var result = accessor.TryRead(target);

        Assert.That(result.Availability, Is.EqualTo(MetricAvailability.Available));
        Assert.That(result.Value, Is.EqualTo(12d));
        Assert.That(target.Present, Is.EqualTo(12));
    }

    [Test]
    public void Non_numeric_member_is_unavailable()
    {
        var accessor = ReflectionMetricAccessor.Create(typeof(FakeTarget), "Text");
        var result = accessor.TryRead(new FakeTarget());

        Assert.That(result.Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Reason, Does.Contain("numeric"));
    }

    [Test]
    public void Getter_exception_is_contained_as_unavailable()
    {
        var accessor = ReflectionMetricAccessor.Create(typeof(FakeTarget), "Throwing");
        var result = accessor.TryRead(new FakeTarget());

        Assert.That(result.Availability, Is.EqualTo(MetricAvailability.Unavailable));
        Assert.That(result.Value, Is.Null);
        Assert.That(result.Reason, Does.Contain("boom"));
    }
}
