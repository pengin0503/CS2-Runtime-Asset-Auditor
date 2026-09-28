using System;
using System.Collections.Generic;
using CS2RuntimeProfiler.Advisor.Settings;
using CS2RuntimeProfiler.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests
{
    [TestFixture]
    public sealed class GameSettingGatewayPolicyTests
    {
        [Test]
        public void Invalid_requested_value_is_rejected_before_any_write()
        {
            var value = "High";
            var writes = 0;
            var gateway = Gateway(Descriptor(), () => value, next => { writes++; value = next; });
            var result = gateway.Apply("standard::quality", "Extreme");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ObservedBefore, Is.EqualTo("High"));
            Assert.That(value, Is.EqualTo("High"));
            Assert.That(writes, Is.Zero);
        }

        [Test]
        public void Custom_setter_is_used_when_verified_metadata_requires_it()
        {
            var value = "High";
            var direct = 0;
            var custom = 0;
            var descriptor = Descriptor();
            var adapter = new AutomaticSettingAdapter(descriptor, () => value,
                next => direct++, next => { custom++; value = next; }, next => next == "Medium", () => { });
            var result = new GameSettingGateway(new[] { adapter }).Apply(descriptor.SettingId, "Medium");
            Assert.That(result.Succeeded, Is.True);
            Assert.That(direct, Is.Zero);
            Assert.That(custom, Is.EqualTo(1));
            Assert.That(result.ObservedAfter, Is.EqualTo("Medium"));
        }

        [Test]
        public void Post_apply_value_is_verified_and_mismatch_is_a_failure()
        {
            var gateway = Gateway(Descriptor(), () => "High", _ => { });
            var result = gateway.Apply("standard::quality", "Medium");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ObservedAfter, Is.EqualTo("High"));
        }

        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void Hidden_disabled_or_read_only_entries_are_never_written(bool userFacing, bool enabled, bool writable)
        {
            var descriptor = Descriptor();
            descriptor.IsUserFacing = userFacing;
            descriptor.IsCurrentlyEnabled = enabled;
            descriptor.IsWritable = writable;
            var writes = 0;
            var gateway = Gateway(descriptor, () => "High", _ => writes++);
            Assert.That(gateway.Apply(descriptor.SettingId, "Medium").Succeeded, Is.False);
            Assert.That(writes, Is.Zero);
        }

        [Test]
        public void Confirmation_sensitive_change_waits_for_explicit_acknowledgement()
        {
            var value = "High";
            var descriptor = Descriptor();
            descriptor.ApplyBehavior = SettingApplyBehavior.ConfirmationRequired;
            var gateway = Gateway(descriptor, () => value, next => value = next);
            Assert.That(gateway.Apply(descriptor.SettingId, "Medium").FailureReason, Is.EqualTo("ConfirmationRequired"));
            Assert.That(value, Is.EqualTo("High"));
            Assert.That(gateway.Apply(descriptor.SettingId, "Medium", confirmed: true).Succeeded, Is.True);
        }

        [Test]
        public void Confirmation_sensitive_undo_requires_its_own_explicit_acknowledgement()
        {
            var value = "Medium";
            var descriptor = Descriptor();
            descriptor.ApplyBehavior = SettingApplyBehavior.ConfirmationRequired;
            var gateway = Gateway(descriptor, () => value, next => value = next);
            Assert.That(gateway.Restore(descriptor.SettingId, "Medium", "High").Succeeded, Is.False);
            Assert.That(value, Is.EqualTo("Medium"));
            Assert.That(gateway.Restore(descriptor.SettingId, "Medium", "High", confirmed: true).Succeeded, Is.True);
        }

        [Test]
        public void One_throwing_adapter_does_not_prevent_a_different_setting_from_working()
        {
            var first = Descriptor();
            var second = Descriptor(); second.SettingId = "standard::other";
            var value = "High";
            var gateway = new GameSettingGateway(new[]
            {
                new AutomaticSettingAdapter(first, () => throw new InvalidOperationException(), _ => { }, null, _ => true, () => { }),
                new AutomaticSettingAdapter(second, () => value, next => value = next, null, _ => true, () => { })
            });
            Assert.That(gateway.Apply(first.SettingId, "Medium").Succeeded, Is.False);
            Assert.That(gateway.Apply(second.SettingId, "Medium").Succeeded, Is.True);
        }

        [Test]
        public void Restore_refuses_to_overwrite_a_newer_external_value()
        {
            var value = "Low";
            var gateway = Gateway(Descriptor(), () => value, next => value = next);
            var result = gateway.Restore("standard::quality", "Medium", "High");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(value, Is.EqualTo("Low"));
        }

        [Test]
        public void Standard_options_entry_without_a_verified_adapter_is_catalogued_read_only()
        {
            var descriptor = Descriptor();
            var gateway = new GameSettingGateway(Array.Empty<AutomaticSettingAdapter>(),
                new SingleCatalog(descriptor));
            var entry = gateway.GetCatalog()[0];
            Assert.That(entry.IsReadable, Is.True);
            Assert.That(entry.IsWritable, Is.False);
            Assert.That(entry.ApplyBehavior, Is.EqualTo(SettingApplyBehavior.ReadOnlyForAdvisor));
            Assert.That(descriptor.IsWritable, Is.True, "Do not mutate the source catalog snapshot.");
        }

        private sealed class SingleCatalog : IStandardGameSettingCatalog
        {
            private readonly GameSettingDescriptor _descriptor;
            public SingleCatalog(GameSettingDescriptor descriptor) => _descriptor = descriptor;
            public IReadOnlyList<GameSettingDescriptor> GetCatalog() => new[] { _descriptor };
        }

        private static GameSettingGateway Gateway(GameSettingDescriptor descriptor, Func<string> read, Action<string> write)
            => new GameSettingGateway(new[]
            { new AutomaticSettingAdapter(descriptor, read, write, null, next =>
                Array.IndexOf(new[] { "Low", "Medium", "High" }, next) >= 0, () => { }) });

        private static GameSettingDescriptor Descriptor() => new GameSettingDescriptor
        {
            SettingId = "standard::quality", DisplayName = "Quality", Category = "graphics",
            CurrentValue = "High", AllowedValues = new[] { "Low", "Medium", "High" },
            ValueKind = SettingValueKind.Enumeration, IsUserFacing = true, IsReadable = true,
            IsWritable = true, HasSafeReversibleWritePath = true,
            IsCurrentlyVisible = true, IsCurrentlyEnabled = true,
            CapabilityState = SettingCapabilityState.Available,
            ApplyBehavior = SettingApplyBehavior.ApplyRequired
        };
    }
}
