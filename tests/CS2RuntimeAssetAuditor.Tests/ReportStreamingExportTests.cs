using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CS2RuntimeAssetAuditor.Assets.Core.Capabilities;
using CS2RuntimeAssetAuditor.Assets.Core.Census;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;
using CS2RuntimeAssetAuditor.Assets.Core.Prefabs;
using CS2RuntimeAssetAuditor.Assets.Export;
using CS2RuntimeAssetAuditor.Export;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests;

// Issue #35: the export streams the report into the file and writes it without the serializer's per-value
// reflection. These tests hold the output to the bytes the former whole-string export wrote.
public class ReportStreamingExportTests
{
    private static readonly string[] Strings =
    {
        "plain", @"C:\Users\Alice\Documents\save.cok", "/home/alice/.config/x", "tab\tnew\nline\u0001end",
        "quote\" back\\slash /slash", "日本語の名前", "line\u2028separator", "", "Game.Simulation.SimulationSystem",
        @"\\server\share\file", "\uD83D\uDE00 emoji", "\uFFFE"
    };

    private static readonly double[] Doubles = { 0d, -1d, 1.5, double.NaN, double.PositiveInfinity, -0d, double.MaxValue, 1e-300, 123456789.123 };
    private static readonly float[] Floats = { 0f, 2.5f, float.NegativeInfinity, float.Epsilon, -3.4e38f };
    private static readonly long[] Longs = { 0L, long.MaxValue, long.MinValue, 42L };
    private static readonly int[] Ints = { 0, int.MinValue, 7 };

    [Test]
    public void Report_types_are_written_without_the_serializer()
    {
        Assert.That(RuntimeAssetAuditReportSerializer.UsesFastWriter, Is.True,
            "A report type uses a shape the fast writer does not support, so exports fall back to the slow serializer.");
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Fast_writer_makes_the_same_bytes_as_the_serializer_for_every_report_type(int variant)
    {
        var report = (RuntimeAssetAuditReport)Populate(typeof(RuntimeAssetAuditReport), new Filler(variant), depth: 0)!;
        var writer = DataContractJsonWriter.TryCreate(typeof(RuntimeAssetAuditReport));
        Assert.That(writer, Is.Not.Null);

        var expected = WriteJson(json => new DataContractJsonSerializer(typeof(RuntimeAssetAuditReport)).WriteObject(json, report));
        var actual = WriteJson(json => writer!.WriteObject(json, report));

        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void Streamed_export_matches_the_former_whole_string_export(int variant)
    {
        var report = (RuntimeAssetAuditReport)Populate(typeof(RuntimeAssetAuditReport), new Filler(variant), depth: 0)!;

        var expected = LegacyExport.Serialize(report);
        var actual = RuntimeAssetAuditReportSerializer.Serialize(report);

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual, Does.Not.Contain("/home/alice"));
    }

    [Test]
    public void Unsupported_shapes_fall_back_to_the_serializer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DataContractJsonWriter.TryCreate(typeof(WithDictionary)), Is.Null);
            Assert.That(DataContractJsonWriter.TryCreate(typeof(NotSealed)), Is.Null);
            Assert.That(DataContractJsonWriter.TryCreate(typeof(WithObject)), Is.Null);
        });
    }

    [Test]
    public void Text_written_in_pieces_is_sanitized_as_one_value()
    {
        var seen = new List<string>();
        var json = WriteJson(inner =>
        {
            var writer = new SanitizingJsonWriter(inner, value => { seen.Add(value); return value; });
            writer.WriteStartElement("root");
            writer.WriteAttributeString("type", "object");
            writer.WriteStartElement("when");
            writer.WriteString("/Date(");
            writer.WriteValue(1234L);
            writer.WriteString(")/");
            writer.WriteEndElement();
            writer.WriteStartElement("count");
            writer.WriteAttributeString("type", "number");
            writer.WriteValue(5L);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.Flush();
        });

        Assert.That(seen, Is.EqualTo(new[] { "/Date(1234)/" }));
        Assert.That(json, Is.EqualTo("{\"when\":\"\\/Date(1234)\\/\",\"count\":5}"));
    }

    [Test]
    public void Skipping_the_patterns_never_changes_a_redaction()
    {
        var sanitizer = new PrivacySanitizer("Kate", "WORKSTATION");
        var corpus = new[]
        {
            "", "plain text", "Kate", "kate's save", "Katerina", "my-kate", "Game.Kate", "Kate.Game", "\u212Aate",
            "KATE", "workstation", "WorkStation-2", "C:\\Users\\Kate\\x", "/home/kate/x", "a/b", "http://example.com/a/b",
            "日本語 Kate", "k\u0130te", "no names here", "KaTe\tWORKSTATION", "\\\\srv\\share"
        };

        foreach (var value in corpus)
            Assert.That(sanitizer.SanitizeText(value), Is.EqualTo(sanitizer.ApplyPatterns(value)), value);
        Assert.That(sanitizer.SanitizeText("\u212Aate"), Is.EqualTo(PrivacySanitizer.RedactedIdentifier));
    }

    [Test]
    public void Asset_report_keeps_each_observation_time_when_times_alternate()
    {
        var first = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
        var second = first.AddMinutes(5);
        var entries = Enumerable.Range(0, 6).Select(index =>
        {
            var at = index % 2 == 0 ? first : second;
            Observation<long> Value(long value) => Observation<long>.FromValue(value, ObservationOrigin.Ecs, at);
            return new CensusEntry(new PrefabKey("P" + index, "Building"), PrefabTraits.Building,
                new CensusCounters(Value(1), Value(2), Value(3), Value(4)), CensusPresence.Present);
        }).ToArray();
        var census = new CensusSnapshot(1, "profile", ScanOptions.Default, first, 1, entries);
        var records = entries.Select(entry => new PrefabRecord(entry.Key, entry.Key.PrefabId, PrefabTraits.Building | PrefabTraits.Prop,
            new AssetOriginEvidence(isBuiltin: true))).ToArray();
        var capabilities = new CapabilityReport("1.6.2f1", CompatibilityState.Supported, Array.Empty<CapabilityStatus>());

        var report = new AuditReportBuilder(new PrivacySanitizer("Kate", "WORKSTATION"))
            .BuildCurrent(records, 1, first, census, null, capabilities, "1.0", first);

        Assert.Multiple(() =>
        {
            for (var index = 0; index < report.Census.Length; index++)
            {
                var expected = (index % 2 == 0 ? first : second).ToString("O");
                Assert.That(report.Census[index].Counters.TopLevelObjects.CapturedAt, Is.EqualTo(expected));
                Assert.That(report.Census[index].Counters.TopLevelObjects.Availability, Is.EqualTo(Availability.Available.ToString()));
                Assert.That(report.Census[index].Presence, Is.EqualTo(CensusPresence.Present.ToString()));
            }
            Assert.That(report.Catalog[0].Traits, Is.EqualTo((PrefabTraits.Building | PrefabTraits.Prop).ToString()));
        });
    }

    private static string WriteJson(Action<XmlDictionaryWriter> write)
    {
        var output = new MemoryStream();
        using (var json = JsonReaderWriterFactory.CreateJsonWriter(output, Encoding.UTF8, ownsStream: false))
        {
            write(json);
            json.Flush();
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }

    // Fills every data member with values that vary by variant: nulls, empty and non-empty collections, special
    // numbers and strings that need escaping or redaction.
    private sealed class Filler
    {
        private readonly int _variant;
        private int _counter;

        public Filler(int variant)
        {
            _variant = variant;
            _counter = variant * 3;
        }

        public int Next() => _counter++;
        public bool UseNull() => (_variant + Next()) % 5 == 0;
        public int CollectionLength() => (_variant + Next()) % 3;
    }

    private static object? Populate(Type type, Filler filler, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return filler.UseNull() ? null : Populate(underlying, filler, depth);
        if (type == typeof(string))
            return filler.UseNull() ? null : Strings[filler.Next() % Strings.Length];
        if (type == typeof(double)) return Doubles[filler.Next() % Doubles.Length];
        if (type == typeof(float)) return Floats[filler.Next() % Floats.Length];
        if (type == typeof(long)) return Longs[filler.Next() % Longs.Length];
        if (type == typeof(int)) return Ints[filler.Next() % Ints.Length];
        if (type == typeof(bool)) return filler.Next() % 2 == 0;

        if (type.IsArray)
        {
            if (depth > 6 || filler.UseNull()) return null;
            var element = type.GetElementType()!;
            var array = Array.CreateInstance(element, filler.CollectionLength());
            for (var index = 0; index < array.Length; index++)
                array.SetValue(Populate(element, filler, depth + 1), index);
            return array;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            if (depth > 6 || filler.UseNull()) return null;
            var list = (IList)Activator.CreateInstance(type)!;
            var count = filler.CollectionLength();
            for (var index = 0; index < count; index++)
                list.Add(Populate(type.GetGenericArguments()[0], filler, depth + 1));
            return list;
        }

        if (depth > 6 || (depth > 0 && filler.UseNull())) return null;
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (property.GetCustomAttribute<DataMemberAttribute>() == null || property.SetMethod == null)
                continue;
            property.SetValue(instance, Populate(property.PropertyType, filler, depth + 1));
        }
        return instance;
    }

    [DataContract]
    private sealed class WithDictionary
    {
        [DataMember(Name = "values")] public Dictionary<string, string> Values { get; set; } = new();
    }

    [DataContract]
    private class NotSealed
    {
        [DataMember(Name = "value")] public string Value { get; set; } = string.Empty;
    }

    [DataContract]
    private sealed class WithObject
    {
        [DataMember(Name = "value")] public object? Value { get; set; }
    }

    // The export as it was before issue #35: the whole report serialized to one string, then every JSON string
    // literal decoded, sanitized and re-encoded with a regular expression over the text.
    private static class LegacyExport
    {
        private static readonly Regex JsonString = new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled);
        private static readonly DataContractJsonSerializer StringSerializer = new DataContractJsonSerializer(typeof(string));

        public static string Serialize(RuntimeAssetAuditReport report)
        {
            using var stream = new MemoryStream();
            new DataContractJsonSerializer(typeof(RuntimeAssetAuditReport)).WriteObject(stream, report);
            var json = Encoding.UTF8.GetString(stream.ToArray());
            return JsonString.Replace(json, match =>
            {
                var next = match.Index + match.Length;
                while (next < json.Length && char.IsWhiteSpace(json[next])) next++;
                if (next < json.Length && json[next] == ':') return match.Value;
                var value = Unescape(match.Value);
                var clean = ReportPrivacy.Sanitize(value);
                if (string.Equals(clean, value, StringComparison.Ordinal))
                    return match.Value;
                using var output = new MemoryStream();
                StringSerializer.WriteObject(output, clean);
                return Encoding.UTF8.GetString(output.ToArray());
            });
        }

        private static string Unescape(string quoted)
        {
            var body = quoted.Substring(1, quoted.Length - 2);
            if (body.IndexOf('\\') < 0)
                return body;
            var builder = new StringBuilder(body.Length);
            for (var index = 0; index < body.Length; index++)
            {
                var character = body[index];
                if (character != '\\' || index + 1 >= body.Length)
                {
                    builder.Append(character);
                    continue;
                }
                var escape = body[++index];
                switch (escape)
                {
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u' when index + 4 < body.Length
                        && int.TryParse(body.Substring(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                        builder.Append((char)code);
                        index += 4;
                        break;
                    default: builder.Append(escape); break;
                }
            }
            return builder.ToString();
        }
    }
}
