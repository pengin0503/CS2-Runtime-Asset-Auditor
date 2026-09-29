using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CS2RuntimeAssetAuditor.Core.DiagnosticLog
{
    /// <summary>
    /// The mod's own copy of its log lines, written next to the diagnostic files. In the real-play logs of
    /// 2026-09-29 the game's <c>CS2RuntimeAssetAuditor.Mod.log</c> kept only the OnLoad line, although later
    /// lines were logged and other mods' logs kept theirs, so the timing lines this mod relies on were lost.
    /// This file does not depend on the game's logger. When it reaches its size cap it is renamed to
    /// <c>.old</c> (replacing an earlier one) and a new file is started, so at most two files are kept.
    /// </summary>
    public sealed class ModEventLogFile : IDisposable
    {
        public const long DefaultMaxBytes = 4L * 1024L * 1024L;

        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private readonly string _path;
        private readonly long _maxBytes;
        private FileStream? _stream;
        private StreamWriter? _writer;

        public ModEventLogFile(string path, long maxBytes = DefaultMaxBytes)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A path is required.", nameof(path));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _path = path;
            _maxBytes = maxBytes;
            Open();
        }

        public string Path => _path;
        public string OldPath => _path + ".old";

        public static string FormatLine(DateTime localTime, string level, string message)
            => "[" + localTime.ToString("yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture) + "] ["
                + (level ?? string.Empty) + "] " + (message ?? string.Empty);

        public void Write(DateTime localTime, string level, string message)
        {
            var writer = _writer;
            if (writer == null)
                return;
            writer.WriteLine(FormatLine(localTime, level, message));
            writer.Flush();
            if (_stream != null && _stream.Length >= _maxBytes)
                Rotate();
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
            _stream = null;
        }

        private void Open()
        {
            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            if (File.Exists(_path) && new FileInfo(_path).Length >= _maxBytes)
                MoveToOld();
            _stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(_stream, Utf8);
        }

        private void Rotate()
        {
            Dispose();
            Open();
        }

        private void MoveToOld()
        {
            if (File.Exists(OldPath))
                File.Delete(OldPath);
            File.Move(_path, OldPath);
        }
    }
}
