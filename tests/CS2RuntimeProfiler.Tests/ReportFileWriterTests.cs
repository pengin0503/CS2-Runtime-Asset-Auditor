using System;
using System.IO;
using CS2RuntimeProfiler.Export;
using NUnit.Framework;

namespace CS2RuntimeProfiler.Tests;

public class ReportFileWriterTests
{
    [Test]
    public void Existing_report_name_uses_a_suffix_without_overwriting_the_existing_file()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var existingPath = Path.Combine(directory, "capture.json");
            File.WriteAllText(existingPath, "existing");

            var writtenPath = ReportFileWriter.WriteUnique(directory, "capture", stream =>
                stream.WriteByte((byte)'n'));

            Assert.That(Path.GetFileName(writtenPath), Is.EqualTo("capture-1.json"));
            Assert.That(File.ReadAllText(existingPath), Is.EqualTo("existing"));
            Assert.That(File.ReadAllBytes(writtenPath), Is.EqualTo(new byte[] { (byte)'n' }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Write_failure_is_returned_once_and_removes_the_partial_report()
    {
        var directory = CreateTemporaryDirectory();
        var attempts = 0;
        try
        {
            Assert.Throws<IOException>(() => ReportFileWriter.WriteUnique(directory, "capture", stream =>
            {
                attempts++;
                stream.WriteByte((byte)'p');
                throw new IOException("simulated write failure");
            }));

            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(directory), Is.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "CS2RuntimeProfilerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
