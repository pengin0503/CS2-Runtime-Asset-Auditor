using System;
using System.Globalization;
using System.Reflection;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Collectors
{
    public sealed class ReflectionMetricReadResult
    {
        public ReflectionMetricReadResult(MetricAvailability availability, double? value, string reason)
        {
            Availability = availability;
            Value = value;
            Reason = reason;
        }

        public MetricAvailability Availability { get; }
        public double? Value { get; }
        public string Reason { get; }
    }

    public sealed class ReflectionMetricAccessor
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly string _memberName;
        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;
        private readonly string _unavailableReason;

        private ReflectionMetricAccessor(string memberName, FieldInfo field, PropertyInfo property, string unavailableReason)
        {
            _memberName = memberName;
            _field = field;
            _property = property;
            _unavailableReason = unavailableReason;
        }

        public static ReflectionMetricAccessor Create(Type targetType, string memberName)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("Member name is required.", nameof(memberName));

            var field = targetType.GetField(memberName, Flags);
            if (field != null)
            {
                if (!IsNumericType(field.FieldType))
                    return new ReflectionMetricAccessor(memberName, null, null, $"Member '{memberName}' is not numeric.");
                return new ReflectionMetricAccessor(memberName, field, null, null);
            }

            var property = targetType.GetProperty(memberName, Flags);
            if (property != null)
            {
                if (property.GetIndexParameters().Length != 0 || property.GetGetMethod(true) == null)
                    return new ReflectionMetricAccessor(memberName, null, null, $"Member '{memberName}' is not a readable scalar property.");
                if (!IsNumericType(property.PropertyType))
                    return new ReflectionMetricAccessor(memberName, null, null, $"Member '{memberName}' is not numeric.");
                return new ReflectionMetricAccessor(memberName, null, property, null);
            }

            return new ReflectionMetricAccessor(memberName, null, null, $"Member '{memberName}' was not found.");
        }

        public ReflectionMetricReadResult TryRead(object target)
        {
            if (_unavailableReason != null)
                return Unavailable(_unavailableReason);
            if (target == null)
                return Unavailable($"Cannot read '{_memberName}' from a null target.");

            try
            {
                object raw = _field != null ? _field.GetValue(target) : _property.GetValue(target, null);
                if (raw == null)
                    return Unavailable($"Member '{_memberName}' returned null.");

                return new ReflectionMetricReadResult(
                    MetricAvailability.Available,
                    Convert.ToDouble(raw, CultureInfo.InvariantCulture),
                    null);
            }
            catch (Exception ex)
            {
                var root = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                return Unavailable($"Reading '{_memberName}' failed: {root.Message}");
            }
        }

        private static ReflectionMetricReadResult Unavailable(string reason)
            => new ReflectionMetricReadResult(MetricAvailability.Unavailable, null, reason);

        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsEnum)
                return false;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }
    }
}
