using System;
using System.Text;
using System.Xml;

namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>
    /// Passes a <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/> write through to the JSON
    /// writer and redacts every JSON string value on the way, so a report is sanitized while it is streamed to the
    /// file. It replaces sanitizing the finished JSON text, which needed the whole report as one string (several
    /// copies of a 70 MB report) and a regular-expression pass over it.
    /// <para>
    /// The serializer can write one string value in several pieces (a <see cref="DateTime"/> is written as
    /// "/Date(", a number and ")/"), so the pieces of an element's text are joined and sanitized as one value when
    /// the element ends, exactly as the whole-text pass saw them. Attribute values carry the JSON type and member
    /// names, which are schema rather than report data, and are passed through unchanged.
    /// </para>
    /// </summary>
    internal sealed class SanitizingJsonWriter : XmlDictionaryWriter
    {
        private readonly XmlDictionaryWriter _inner;
        private readonly Func<string, string> _sanitize;
        // The current element's text: most values arrive in one piece and are kept as that string; a second piece
        // moves them into the builder.
        private string? _text;
        private StringBuilder? _textBuilder;
        private int _attributeDepth;

        public SanitizingJsonWriter(XmlDictionaryWriter inner, Func<string, string> sanitize)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _sanitize = sanitize ?? throw new ArgumentNullException(nameof(sanitize));
        }

        private bool InAttribute => _attributeDepth > 0;

        public override WriteState WriteState => _inner.WriteState;
        public override string? XmlLang => _inner.XmlLang;
        public override XmlSpace XmlSpace => _inner.XmlSpace;
        public override XmlWriterSettings? Settings => _inner.Settings;

        // Text of the current element. It is written only when the element ends, or before anything that is not
        // more text, so a value is never split between the sanitizer and the output.
        private void AppendText(string? text)
        {
            if (text == null)
                return;
            if (_text == null)
            {
                _text = text;
                return;
            }
            if (_textBuilder == null)
                _textBuilder = new StringBuilder();
            if (_textBuilder.Length == 0)
                _textBuilder.Append(_text);
            _textBuilder.Append(text);
            _text = string.Empty;
        }

        private void FlushText()
        {
            if (_text == null)
                return;
            string value;
            if (_textBuilder != null && _textBuilder.Length > 0)
            {
                value = _textBuilder.ToString();
                _textBuilder.Clear();
            }
            else
            {
                value = _text;
            }
            _text = null;
            _inner.WriteString(_sanitize(value));
        }

        public override void WriteString(string? text)
        {
            if (InAttribute)
                _inner.WriteString(text);
            else
                AppendText(text);
        }

        public override void WriteString(XmlDictionaryString? value) => WriteString(value?.Value);

        public override void WriteChars(char[] buffer, int index, int count)
        {
            if (InAttribute)
                _inner.WriteChars(buffer, index, count);
            else
                AppendText(new string(buffer, index, count));
        }

        public override void WriteCharEntity(char ch)
        {
            if (InAttribute)
                _inner.WriteCharEntity(ch);
            else
                AppendText(ch.ToString());
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            if (InAttribute)
                _inner.WriteSurrogateCharEntity(lowChar, highChar);
            else
                AppendText(new string(new[] { highChar, lowChar }));
        }

        // A number written while string text is pending belongs to that string ("/Date(" + ticks + ")/");
        // otherwise it is a JSON number and passes through unchanged.
        private bool AppendToPendingText(string formatted)
        {
            if (InAttribute || _text == null)
                return false;
            AppendText(formatted);
            return true;
        }

        public override void WriteValue(long value)
        {
            if (!AppendToPendingText(XmlConvert.ToString(value)))
                _inner.WriteValue(value);
        }

        public override void WriteValue(int value)
        {
            if (!AppendToPendingText(XmlConvert.ToString(value)))
                _inner.WriteValue(value);
        }

        public override void WriteValue(bool value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(double value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(float value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(decimal value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(DateTime value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(DateTimeOffset value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(Guid value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(TimeSpan value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(UniqueId value) { FlushText(); _inner.WriteValue(value); }
        public override void WriteValue(XmlDictionaryString value) => WriteString(value?.Value);

        public override void WriteValue(string? value) => WriteString(value);

        public override void WriteValue(object value)
        {
            if (value is string text)
            {
                WriteString(text);
                return;
            }
            FlushText();
            _inner.WriteValue(value);
        }

        public override void WriteStartElement(string? prefix, string localName, string? ns)
        {
            FlushText();
            _inner.WriteStartElement(prefix, localName, ns);
        }

        public override void WriteStartElement(string? prefix, XmlDictionaryString localName, XmlDictionaryString? namespaceUri)
        {
            FlushText();
            _inner.WriteStartElement(prefix, localName, namespaceUri);
        }

        public override void WriteEndElement()
        {
            FlushText();
            _inner.WriteEndElement();
        }

        public override void WriteFullEndElement()
        {
            FlushText();
            _inner.WriteFullEndElement();
        }

        public override void WriteStartAttribute(string? prefix, string localName, string? ns)
        {
            FlushText();
            _attributeDepth++;
            _inner.WriteStartAttribute(prefix, localName, ns);
        }

        public override void WriteStartAttribute(string? prefix, XmlDictionaryString localName, XmlDictionaryString? namespaceUri)
        {
            FlushText();
            _attributeDepth++;
            _inner.WriteStartAttribute(prefix, localName, namespaceUri);
        }

        public override void WriteEndAttribute()
        {
            _inner.WriteEndAttribute();
            if (_attributeDepth > 0)
                _attributeDepth--;
        }

        public override void WriteStartDocument() { FlushText(); _inner.WriteStartDocument(); }
        public override void WriteStartDocument(bool standalone) { FlushText(); _inner.WriteStartDocument(standalone); }
        public override void WriteEndDocument() { FlushText(); _inner.WriteEndDocument(); }
        public override void WriteDocType(string name, string? pubid, string? sysid, string? subset) { FlushText(); _inner.WriteDocType(name, pubid, sysid, subset); }
        public override void WriteCData(string? text) { FlushText(); _inner.WriteCData(text); }
        public override void WriteComment(string? text) { FlushText(); _inner.WriteComment(text); }
        public override void WriteProcessingInstruction(string name, string? text) { FlushText(); _inner.WriteProcessingInstruction(name, text); }
        public override void WriteEntityRef(string name) { FlushText(); _inner.WriteEntityRef(name); }
        public override void WriteWhitespace(string? ws) { FlushText(); _inner.WriteWhitespace(ws); }
        public override void WriteRaw(char[] buffer, int index, int count) { FlushText(); _inner.WriteRaw(buffer, index, count); }
        public override void WriteRaw(string data) { FlushText(); _inner.WriteRaw(data); }
        public override void WriteBase64(byte[] buffer, int index, int count) { FlushText(); _inner.WriteBase64(buffer, index, count); }
        public override void WriteXmlnsAttribute(string? prefix, string namespaceUri) { FlushText(); _inner.WriteXmlnsAttribute(prefix, namespaceUri); }
        public override void WriteXmlnsAttribute(string? prefix, XmlDictionaryString namespaceUri) { FlushText(); _inner.WriteXmlnsAttribute(prefix, namespaceUri); }
        public override string? LookupPrefix(string ns) => _inner.LookupPrefix(ns);

        public override void Flush()
        {
            FlushText();
            _inner.Flush();
        }

        public override void Close()
        {
            FlushText();
            _inner.Close();
        }
    }
}
