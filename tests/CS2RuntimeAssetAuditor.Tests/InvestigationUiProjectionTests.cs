using System;
using System.IO;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class InvestigationUiProjectionTests
    {
        [Test]
        public void Absent_experiment_projects_null_and_observed_fields_come_from_current_result()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CS2RuntimeAssetAuditor.sln")))
                directory = directory.Parent;
            Assert.That(directory, Is.Not.Null);
            var source = File.ReadAllText(Path.Combine(directory.FullName, "src/CS2RuntimeAssetAuditor/UI/ProfilerUISystem.cs"));
            Assert.That(source, Does.Contain("WriteAdvisorExperiment(writer, state.Experiment)"));
            var start = source.IndexOf("private static void WriteAdvisorExperiment(", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            var method = source.Substring(start, source.IndexOf("private static void WriteAdvisorAction", start, StringComparison.Ordinal) - start);
            Assert.That(method, Does.Contain("if (experiment == null)"));
            Assert.That(method, Does.Contain("writer.WriteNull()"));
            foreach (var field in new[] { "experimentId", "state", "validity", "invalidationReason", "baselineCaptureId",
                "followUpCaptureId", "settingId", "settingDisplayName", "originalValue", "testedValue",
                "changeAppliedAtUtc", "stabilizationReadyAtUtc", "completionOutcome", "lastFailureReason", "comparison" })
                Assert.That(method, Does.Contain("\"" + field + "\""), field);
        }
    }
}
