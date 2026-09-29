using CS2RuntimeAssetAuditor.Core;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

public class NestedTimingStackTests
{
    [Test]
    public void Parent_is_charged_only_its_own_time_when_it_updates_children()
    {
        var stack = new NestedTimingStack();
        var parent = stack.Push(0);
        var childA = stack.Push(10);
        Assert.That(stack.TryPop(childA, 40, out var inclusiveA, out var exclusiveA), Is.True);
        var childB = stack.Push(50);
        Assert.That(stack.TryPop(childB, 70, out _, out var exclusiveB), Is.True);
        Assert.That(stack.TryPop(parent, 100, out var inclusiveParent, out var exclusiveParent), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(inclusiveA, Is.EqualTo(30));
            Assert.That(exclusiveA, Is.EqualTo(30));
            Assert.That(exclusiveB, Is.EqualTo(20));
            Assert.That(inclusiveParent, Is.EqualTo(100));
            Assert.That(exclusiveParent, Is.EqualTo(50), "100 total minus 30 + 20 spent in children");
            Assert.That(exclusiveA + exclusiveB + exclusiveParent, Is.EqualTo(inclusiveParent), "self times add up to wall time");
            Assert.That(stack.Depth, Is.Zero);
        });
    }

    [Test]
    public void Deep_nesting_credits_each_level_once()
    {
        var stack = new NestedTimingStack();
        var outer = stack.Push(0);
        var middle = stack.Push(10);
        var inner = stack.Push(20);
        stack.TryPop(inner, 30, out _, out var innerSelf);
        stack.TryPop(middle, 50, out _, out var middleSelf);
        stack.TryPop(outer, 60, out _, out var outerSelf);

        Assert.That(new[] { innerSelf, middleSelf, outerSelf }, Is.EqualTo(new long[] { 10, 30, 20 }));
    }

    [Test]
    public void Frames_abandoned_by_a_throwing_child_are_discarded_by_the_outer_pop()
    {
        var stack = new NestedTimingStack();
        var outer = stack.Push(0);
        stack.Push(10); // child threw; its postfix never ran

        Assert.That(stack.TryPop(outer, 50, out var inclusive, out var exclusive), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(inclusive, Is.EqualTo(50));
            Assert.That(exclusive, Is.EqualTo(50), "an unfinished child's time stays with the parent");
            Assert.That(stack.Depth, Is.Zero);
        });
    }

    [Test]
    public void Stale_tokens_after_a_reset_are_ignored()
    {
        var stack = new NestedTimingStack();
        var token = stack.Push(0);
        stack.Reset();
        Assert.That(stack.TryPop(token, 10, out _, out _), Is.False);
        Assert.That(stack.TryPop(0, 10, out _, out _), Is.False);
    }

    [Test]
    public void Stack_grows_beyond_its_initial_capacity()
    {
        var stack = new NestedTimingStack();
        var tokens = new int[100];
        for (var i = 0; i < tokens.Length; i++) tokens[i] = stack.Push(i);
        for (var i = tokens.Length - 1; i >= 0; i--) Assert.That(stack.TryPop(tokens[i], 1000, out _, out _), Is.True);
        Assert.That(stack.Depth, Is.Zero);
    }
}
