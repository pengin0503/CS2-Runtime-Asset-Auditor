using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Advisor.Settings;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class AdvisorSettingOperationsTests
    {
        [Test]
        public void Session_undo_without_confirmation_reports_settings_that_need_it_and_keeps_them_applied()
        {
            var gateway = new FakeGateway();
            var operations = Create(gateway);
            operations.Apply(new[] { Recommendation("display", "High", "Low", SettingApplyBehavior.ConfirmationRequired) }, "display", "Low", confirmed: true);
            operations.Apply(new[] { Recommendation("shadow", "High", "Low") }, "shadow", "Low", confirmed: false);

            operations.UndoSession(confirmed: false);

            Assert.Multiple(() =>
            {
                Assert.That(gateway.Values["shadow"], Is.EqualTo("High"), "a plain setting is restored");
                Assert.That(gateway.Values["display"], Is.EqualTo("Low"), "a confirmation setting is not written silently");
                Assert.That(operations.LastAction!.Kind, Is.EqualTo(AdvisorActionKind.UndoSession));
                Assert.That(operations.LastAction.Succeeded, Is.False);
                Assert.That(operations.LastAction.FailureReason, Is.EqualTo("ConfirmationRequired"));
                Assert.That(operations.LastAction.ConfirmationRequiredSettingIds, Is.EqualTo(new[] { "display" }));
            });
        }

        [Test]
        public void Confirmed_session_undo_restores_confirmation_settings_too()
        {
            var gateway = new FakeGateway();
            var session = new SettingChangeSession();
            var operations = Create(gateway, session);
            operations.Apply(new[] { Recommendation("display", "High", "Low", SettingApplyBehavior.ConfirmationRequired) }, "display", "Low", confirmed: true);
            operations.Apply(new[] { Recommendation("shadow", "High", "Low") }, "shadow", "Low", confirmed: false);

            operations.UndoSession(confirmed: true);

            Assert.Multiple(() =>
            {
                Assert.That(gateway.Values["display"], Is.EqualTo("High"));
                Assert.That(gateway.Values["shadow"], Is.EqualTo("High"));
                Assert.That(operations.LastAction!.Succeeded, Is.True);
                Assert.That(operations.LastAction.SucceededCount, Is.EqualTo(2));
                Assert.That(session.PlanSessionUndo(), Is.Empty);
            });
        }

        [Test]
        public void Apply_that_still_needs_confirmation_is_not_recorded_as_a_failed_change()
        {
            var gateway = new FakeGateway();
            var session = new SettingChangeSession();
            var operations = Create(gateway, session);

            var result = operations.Apply(new[] { Recommendation("display", "High", "Low", SettingApplyBehavior.ConfirmationRequired) }, "display", "Low", confirmed: false);

            Assert.Multiple(() =>
            {
                Assert.That(result.FailureReason, Is.EqualTo("ConfirmationRequired"));
                Assert.That(session.Changes, Is.Empty);
                Assert.That(operations.LastAction!.ConfirmationRequiredSettingIds, Is.EqualTo(new[] { "display" }));
            });
        }

        [Test]
        public void Stale_recommendation_is_reported_instead_of_silently_ignored()
        {
            var gateway = new FakeGateway();
            gateway.Values["shadow"] = "Medium";
            var operations = Create(gateway);

            operations.Apply(new[] { Recommendation("shadow", "High", "Low") }, "shadow", "Low", confirmed: false);

            Assert.Multiple(() =>
            {
                Assert.That(operations.LastAction!.Succeeded, Is.False);
                Assert.That(operations.LastAction.FailureReason, Is.EqualTo("StaleOrUnavailableRecommendation"));
                Assert.That(operations.LastAction.DisplayName, Is.EqualTo("Display shadow"));
            });
        }

        [Test]
        public void Applied_change_carries_the_recommendation_display_name()
        {
            var session = new SettingChangeSession();
            var operations = Create(new FakeGateway(), session);
            operations.Apply(new[] { Recommendation("shadow", "High", "Low") }, "shadow", "Low", confirmed: false);
            Assert.That(session.Changes.Single().DisplayName, Is.EqualTo("Display shadow"));
        }

        private static AdvisorSettingOperations Create(FakeGateway gateway, SettingChangeSession? session = null)
            => new AdvisorSettingOperations(() => gateway, session ?? new SettingChangeSession());

        private static SettingRecommendation Recommendation(string id, string current, string recommended,
            SettingApplyBehavior behavior = SettingApplyBehavior.ApplyRequired)
            => new SettingRecommendation(id, "Display " + id, current, recommended, RecommendationDirection.LowerRecommended,
                RecommendationPriority.High, AdvisorConfidence.High, "measured", new[] { "gpu" }, SettingCapabilityState.Available, behavior);

        private sealed class FakeGateway : IGameSettingGateway
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string> { ["display"] = "High", ["shadow"] = "High" };

            public IReadOnlyList<GameSettingDescriptor> GetCatalog() => Array.Empty<GameSettingDescriptor>();
            public string Read(string settingId) => Values.TryGetValue(settingId, out var value) ? value : null!;

            public SettingApplyResult Apply(string settingId, string value, bool confirmed = false)
            {
                var result = new SettingApplyResult { Requested = value, ObservedBefore = Read(settingId) };
                if (settingId == "display" && !confirmed)
                {
                    result.FailureReason = "ConfirmationRequired";
                    return result;
                }
                Values[settingId] = value;
                result.ObservedAfter = value;
                result.Succeeded = true;
                return result;
            }

            public SettingApplyResult Restore(string settingId, string expectedCurrentValue, string originalValue, bool confirmed = false)
                => Read(settingId) != expectedCurrentValue
                    ? new SettingApplyResult { FailureReason = "ExternallyModified" }
                    : Apply(settingId, originalValue, confirmed);
        }
    }
}
