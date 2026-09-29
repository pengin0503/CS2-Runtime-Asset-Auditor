using System;
using System.IO;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    [TestFixture]
    public sealed class InvestigationAdvisorIntegrationContractTests
    {
        [Test]
        public void Experiment_commands_use_existing_operations_and_exact_capture_request()
        {
            var source = File.ReadAllText(FindSource());
            Assert.That(Section(source, "public SettingApplyResult ApplyExperimentChange", "public bool StartExperimentFollowUpCapture"),
                Does.Contain("Operations.Apply("));
            var followUp = Section(source, "public bool StartExperimentFollowUpCapture", "public void CancelExperiment");
            Assert.That(followUp, Does.Contain("_capture?.RequestManualCapture()"));
            Assert.That(followUp, Does.Contain("_experiment.RecordFollowUpStarted(capture.Id)"));
            Assert.That(followUp, Does.Not.Contain("CompletedSessions"));
            Assert.That(Section(source, "public SettingApplyResult UndoExperimentChange", "private void ObserveTestedSettingIntegrity"),
                Does.Contain("Operations.Undo("));
        }

        [Test]
        public void Observation_uses_pure_guard_and_coordinator_without_new_writer_or_comparison()
        {
            var source = File.ReadAllText(FindSource());
            var observation = Section(source, "private void ObserveExperimentFollowUp", "\n    }\n}");
            Assert.That(observation, Does.Contain("InvestigationExperimentGuard.IsExpectedFollowUp("));
            Assert.That(observation, Does.Contain("InvestigationExperimentGuard.SelectQualifyingChanges("));
            Assert.That(observation, Does.Contain("ObserveTestedSettingIntegrity(true)"),
                "The tested value must be read again immediately before showing a comparison.");
            Assert.That(observation, Does.Contain("_experiment.CompleteFollowUp("));
            Assert.That(observation, Does.Not.Contain("AdvisorComparison.Compare("));
            Assert.That(Section(source, "private void ObserveTestedSettingIntegrity", "private void ObserveExperimentFollowUp"),
                Does.Contain("Gateway.Read(current.SettingId)"));
            Assert.That(Section(source, "private void ObserveSessionChange", "public bool DiagnoseCompletedCapture"),
                Does.Not.Contain("Operations.Undo("));
            Assert.That(Section(source, "private void ObserveSessionChange", "public bool DiagnoseCompletedCapture"),
                Does.Contain("_experiment.Clear()"),
                "A terminal experiment from the previous city must not be projected into the new city.");
        }

        [Test]
        public void Ordinary_undo_paths_track_effective_other_setting_mutations()
        {
            var source = File.ReadAllText(FindSource());
            Assert.That(Section(source, "public SettingApplyResult UndoSetting", "public IReadOnlyList<SettingApplyResult> UndoSession"),
                Does.Contain("InvalidateOnOtherSettingMutation(settingId, result)"));
            Assert.That(Section(source, "public IReadOnlyList<SettingApplyResult> UndoSession", "public SettingApplyResult ResolveConflict"),
                Does.Contain("InvalidateOnOtherSettingMutation(planned[index].SettingId, results[index])"));
            Assert.That(Section(source, "public SettingApplyResult ResolveConflict", "public InvestigationExperiment CurrentExperiment"),
                Does.Contain("InvalidateOnOtherSettingMutation(settingId, result)"));
        }

        private static string Section(string source, string first, string last)
        {
            var start = source.IndexOf(first, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), first);
            var end = source.IndexOf(last, start + first.Length, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), last);
            return source.Substring(start, end - start);
        }

        private static string FindSource()
        {
            var root = Environment.GetEnvironmentVariable("CS2_AUDITOR_REPO_ROOT");
            var directory = new DirectoryInfo(string.IsNullOrEmpty(root)
                ? TestContext.CurrentContext.TestDirectory : root);
            while (directory != null)
            {
                var path = Path.Combine(directory.FullName, "src/CS2RuntimeAssetAuditor/Advisor/AdvisorSystem.cs");
                if (File.Exists(path)) return path;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("AdvisorSystem.cs was not found in the checkout.");
        }
    }
}
