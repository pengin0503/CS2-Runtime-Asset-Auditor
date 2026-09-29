using System;
using System.IO;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.AdapterTests
{
    [TestFixture]
    public sealed class InvestigationCaptureContractTests
    {
        [Test]
        public void Explicit_manual_request_returns_only_the_session_created_by_that_request()
        {
            var source = File.ReadAllText(FindSource());
            var start = source.IndexOf("public CaptureSession RequestManualCapture()", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "The request must return a CaptureSession.");
            var end = source.IndexOf("protected override void OnDestroy()", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            var method = source.Substring(start, end - start);
            var before = method.IndexOf("var before = _controller?.CurrentSession", StringComparison.Ordinal);
            var request = method.IndexOf("_controller?.RequestManualCapture(", StringComparison.Ordinal);
            var after = method.IndexOf("var created = _controller?.CurrentSession", StringComparison.Ordinal);
            Assert.That(before, Is.GreaterThanOrEqualTo(0));
            Assert.That(request, Is.GreaterThan(before));
            Assert.That(after, Is.GreaterThan(request));
            Assert.That(method, Does.Contain("!ReferenceEquals(before, created)"));
            Assert.That(method, Does.Contain("return started ? created : null"));
            Assert.That(method, Does.Not.Contain("CompletedSessions"));
        }

        private static string FindSource()
        {
            var root = Environment.GetEnvironmentVariable("CS2_AUDITOR_REPO_ROOT");
            var directory = new DirectoryInfo(string.IsNullOrEmpty(root)
                ? TestContext.CurrentContext.TestDirectory : root);
            while (directory != null)
            {
                var path = Path.Combine(directory.FullName, "src/CS2RuntimeAssetAuditor/Profiling/CaptureRuntimeSystem.cs");
                if (File.Exists(path)) return path;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("CaptureRuntimeSystem.cs was not found in the checkout.");
        }
    }
}
