using System;
using System.IO;
using System.Text;

namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// Writes diagnostic-log lines with a size cap so a log left on for a long session cannot fill the disk.
    /// Once a line would pass the cap, that line and every later one are dropped; the file stays valid CSV
    /// because nothing is written in their place. Each accepted line is flushed so a crash keeps what was measured.
    /// </summary>
    public sealed class DiagnosticLogWriter : IDisposable
    {
        public const long DefaultMaxBytes = 32L * 1024L * 1024L;

        private readonly TextWriter _writer;
        private readonly Encoding _encoding;
        private readonly long _maxBytes;
        private readonly int _newLineBytes;
        private long _writtenBytes;

        public DiagnosticLogWriter(TextWriter writer, Encoding encoding, long maxBytes = DefaultMaxBytes)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _maxBytes = maxBytes;
            _newLineBytes = _encoding.GetByteCount(_writer.NewLine);
        }

        public long WrittenBytes => _writtenBytes;
        public bool IsLimitReached { get; private set; }

        /// <returns>False when the line was dropped because of the size cap.</returns>
        public bool TryWriteLine(string line)
        {
            if (IsLimitReached)
                return false;

            var bytes = _encoding.GetByteCount(line ?? string.Empty) + _newLineBytes;
            if (_writtenBytes + bytes > _maxBytes)
            {
                IsLimitReached = true;
                return false;
            }

            _writer.WriteLine(line);
            _writer.Flush();
            _writtenBytes += bytes;
            return true;
        }

        public void Dispose() => _writer.Dispose();
    }
}
