using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace CS2RuntimeAssetAuditor.Export
{
    /// <summary>
    /// Writes a <c>[DataContract]</c> object graph to a JSON <see cref="XmlDictionaryWriter"/> with the same calls
    /// <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/> makes, so the runtime's own JSON
    /// writer produces the same bytes. On the game's Mono runtime the serializer walks the graph through reflection
    /// for every value and took about 8 s of a 70 MB report's export freeze; this writer reads members through
    /// delegates built once per type.
    /// <para>
    /// Only the shapes the report types use are supported: sealed classes without base contracts, strings,
    /// <see cref="int"/>, <see cref="long"/>, <see cref="float"/>, <see cref="double"/>, <see cref="bool"/>, their
    /// nullable forms, and arrays or <see cref="List{T}"/> of these. <see cref="TryCreate"/> checks the whole type
    /// graph before anything is written and returns null for any other shape, so the caller can fall back to the
    /// serializer instead of writing different output.
    /// </para>
    /// </summary>
    internal sealed class DataContractJsonWriter
    {
        private const string TypeAttribute = "type";
        private const string ItemElement = "item";

        private readonly IRootWriter _root;

        private DataContractJsonWriter(IRootWriter root)
        {
            _root = root;
        }

        /// <summary>A writer for <paramref name="rootType"/>, or null when its graph uses an unsupported shape.</summary>
        public static DataContractJsonWriter? TryCreate(Type rootType)
        {
            try
            {
                var plans = new Dictionary<Type, object>();
                return CreateValueWriter(rootType, plans) is IRootWriter root && IsContractClass(rootType)
                    ? new DataContractJsonWriter(root)
                    : null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        public void WriteObject(XmlDictionaryWriter writer, object graph)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            writer.WriteStartElement("root");
            _root.WriteRoot(writer, graph);
            writer.WriteEndElement();
        }

        private static void WriteType(XmlDictionaryWriter writer, string type) => writer.WriteAttributeString(TypeAttribute, type);

        private interface IRootWriter
        {
            void WriteRoot(XmlDictionaryWriter writer, object value);
        }

        // Writes the attributes and content of an element that the caller has started. Values are typed so numbers
        // are never boxed on the way.
        private abstract class ValueWriter<T>
        {
            public abstract void WriteContent(XmlDictionaryWriter writer, T value);
        }

        private sealed class StringValueWriter : ValueWriter<string?>
        {
            public override void WriteContent(XmlDictionaryWriter writer, string? value)
            {
                if (value == null)
                    WriteType(writer, "null");
                else
                    writer.WriteValue(value);
            }
        }

        private sealed class Int32Writer : ValueWriter<int>
        {
            public override void WriteContent(XmlDictionaryWriter writer, int value) { WriteType(writer, "number"); writer.WriteValue(value); }
        }

        private sealed class Int64Writer : ValueWriter<long>
        {
            public override void WriteContent(XmlDictionaryWriter writer, long value) { WriteType(writer, "number"); writer.WriteValue(value); }
        }

        private sealed class DoubleWriter : ValueWriter<double>
        {
            public override void WriteContent(XmlDictionaryWriter writer, double value) { WriteType(writer, "number"); writer.WriteValue(value); }
        }

        private sealed class SingleWriter : ValueWriter<float>
        {
            public override void WriteContent(XmlDictionaryWriter writer, float value) { WriteType(writer, "number"); writer.WriteValue(value); }
        }

        private sealed class BooleanWriter : ValueWriter<bool>
        {
            public override void WriteContent(XmlDictionaryWriter writer, bool value) { WriteType(writer, "boolean"); writer.WriteValue(value); }
        }

        private sealed class NullableWriter<T> : ValueWriter<T?> where T : struct
        {
            private readonly ValueWriter<T> _value;

            public NullableWriter(ValueWriter<T> value)
            {
                _value = value;
            }

            public override void WriteContent(XmlDictionaryWriter writer, T? value)
            {
                if (value.HasValue)
                    _value.WriteContent(writer, value.Value);
                else
                    WriteType(writer, "null");
            }
        }

        private sealed class ObjectWriter<T> : ValueWriter<T>, IRootWriter where T : class
        {
            private readonly ClassPlan<T> _plan;

            public ObjectWriter(ClassPlan<T> plan)
            {
                _plan = plan;
            }

            public void WriteRoot(XmlDictionaryWriter writer, object value) => WriteContent(writer, (T)value);

            public override void WriteContent(XmlDictionaryWriter writer, T value)
            {
                if (value == null)
                {
                    WriteType(writer, "null");
                    return;
                }
                WriteType(writer, "object");
                foreach (var member in _plan.Members)
                    member.Write(writer, value);
            }
        }

        private sealed class ArrayWriter<TItem> : ValueWriter<TItem[]>
        {
            private readonly ValueWriter<TItem> _item;

            public ArrayWriter(ValueWriter<TItem> item)
            {
                _item = item;
            }

            public override void WriteContent(XmlDictionaryWriter writer, TItem[] value)
            {
                if (value == null)
                {
                    WriteType(writer, "null");
                    return;
                }
                WriteType(writer, "array");
                for (var index = 0; index < value.Length; index++)
                {
                    writer.WriteStartElement(ItemElement);
                    _item.WriteContent(writer, value[index]);
                    writer.WriteEndElement();
                }
            }
        }

        private sealed class ListWriter<TItem> : ValueWriter<List<TItem>>
        {
            private readonly ValueWriter<TItem> _item;

            public ListWriter(ValueWriter<TItem> item)
            {
                _item = item;
            }

            public override void WriteContent(XmlDictionaryWriter writer, List<TItem> value)
            {
                if (value == null)
                {
                    WriteType(writer, "null");
                    return;
                }
                WriteType(writer, "array");
                for (var index = 0; index < value.Count; index++)
                {
                    writer.WriteStartElement(ItemElement);
                    _item.WriteContent(writer, value[index]);
                    writer.WriteEndElement();
                }
            }
        }

        private sealed class ClassPlan<T>
        {
            public MemberPlan<T>[] Members = Array.Empty<MemberPlan<T>>();
        }

        private abstract class MemberPlan<TOwner>
        {
            public abstract void Write(XmlDictionaryWriter writer, TOwner owner);
        }

        private sealed class MemberPlan<TOwner, TValue> : MemberPlan<TOwner>
        {
            private readonly string _name;
            private readonly Func<TOwner, TValue> _get;
            private readonly bool _emitDefaultValue;
            private readonly ValueWriter<TValue> _value;

            public MemberPlan(string name, Func<TOwner, TValue> get, bool emitDefaultValue, ValueWriter<TValue> value)
            {
                _name = name;
                _get = get;
                _emitDefaultValue = emitDefaultValue;
                _value = value;
            }

            public override void Write(XmlDictionaryWriter writer, TOwner owner)
            {
                var value = _get(owner);
                if (!_emitDefaultValue && EqualityComparer<TValue>.Default.Equals(value, default!))
                    return;
                writer.WriteStartElement(_name);
                _value.WriteContent(writer, value);
                writer.WriteEndElement();
            }
        }

        private static bool IsContractClass(Type type) =>
            type.IsClass && type.IsSealed && type.BaseType == typeof(object)
            && type.GetCustomAttributes(typeof(DataContractAttribute), inherit: false).Length > 0;

        // Returns a ValueWriter<type>; generic instances are created through reflection once per type.
        private static object CreateValueWriter(Type type, Dictionary<Type, object> plans)
        {
            if (type == typeof(string)) return new StringValueWriter();
            if (type == typeof(int)) return new Int32Writer();
            if (type == typeof(long)) return new Int64Writer();
            if (type == typeof(double)) return new DoubleWriter();
            if (type == typeof(float)) return new SingleWriter();
            if (type == typeof(bool)) return new BooleanWriter();

            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return Construct(typeof(NullableWriter<>), underlying, CreateValueWriter(underlying, plans));
            if (type.IsArray && type.GetArrayRank() == 1)
                return Construct(typeof(ArrayWriter<>), type.GetElementType()!, CreateValueWriter(type.GetElementType()!, plans));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return Construct(typeof(ListWriter<>), type.GetGenericArguments()[0], CreateValueWriter(type.GetGenericArguments()[0], plans));
            if (IsContractClass(type))
                return Construct(typeof(ObjectWriter<>), type, GetPlan(type, plans));
            throw Unsupported(type);
        }

        private static object Construct(Type genericDefinition, Type argument, object parameter) =>
            Activator.CreateInstance(genericDefinition.MakeGenericType(argument), parameter)!;

        private static object GetPlan(Type type, Dictionary<Type, object> plans)
        {
            if (plans.TryGetValue(type, out var existing))
                return existing;
            // Registered before its members are planned, so a type that contains itself is planned once.
            var plan = Activator.CreateInstance(typeof(ClassPlan<>).MakeGenericType(type))!;
            plans[type] = plan;

            var members = new List<(string Name, int Order, object Plan)>();
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var member in type.GetMembers(Flags))
            {
                if (!(member is PropertyInfo) && !(member is FieldInfo))
                    continue;
                var attribute = (DataMemberAttribute?)member.GetCustomAttributes(typeof(DataMemberAttribute), inherit: false).FirstOrDefault();
                if (attribute == null)
                    continue;
                var name = string.IsNullOrEmpty(attribute.Name) ? member.Name : attribute.Name;
                var valueType = member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;
                var factory = typeof(DataContractJsonWriter)
                    .GetMethod(nameof(CreateMemberPlan), BindingFlags.Static | BindingFlags.NonPublic)!
                    .MakeGenericMethod(type, valueType);
                var memberPlan = factory.Invoke(null, new object[] { member, name, attribute.EmitDefaultValue, CreateValueWriter(valueType, plans) })!;
                members.Add((name, attribute.Order, memberPlan));
            }

            // The serializer's order: members without an Order (-1) first, then by Order; names break ties ordinally.
            var ordered = members
                .OrderBy(member => member.Order)
                .ThenBy(member => member.Name, StringComparer.Ordinal)
                .Select(member => member.Plan)
                .ToArray();
            var typed = Array.CreateInstance(typeof(MemberPlan<>).MakeGenericType(type), ordered.Length);
            Array.Copy(ordered, typed, ordered.Length);
            plan.GetType().GetField("Members")!.SetValue(plan, typed);
            return plan;
        }

        private static MemberPlan<TOwner> CreateMemberPlan<TOwner, TValue>(MemberInfo member, string name, bool emitDefaultValue, ValueWriter<TValue> value)
        {
            Func<TOwner, TValue> get;
            if (member is PropertyInfo property)
            {
                var getter = property.GetGetMethod(nonPublic: true) ?? throw Unsupported(property.PropertyType);
                get = (Func<TOwner, TValue>)Delegate.CreateDelegate(typeof(Func<TOwner, TValue>), getter);
            }
            else
            {
                var field = (FieldInfo)member;
                get = owner => (TValue)field.GetValue(owner)!;
            }
            return new MemberPlan<TOwner, TValue>(name, get, emitDefaultValue, value);
        }

        private static NotSupportedException Unsupported(Type type) =>
            new NotSupportedException($"The report writer does not support {type.FullName}; the serializer is used instead.");
    }
}
