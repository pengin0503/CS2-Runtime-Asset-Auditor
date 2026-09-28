using System;
using System.Reflection;

namespace CS2RuntimeProfiler.Core
{
    /// <summary>
    /// Reads the simulation pause state through member names verified against the game runtime.
    /// Unknown or unreadable runtime layouts return false from TryRead so callers can fail-open
    /// only for this compatibility signal rather than guessing from simulation speed.
    /// </summary>
    public static class RuntimePauseStateReader
    {
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool TryRead(object instance, out bool paused)
        {
            paused = false;
            if (instance == null)
                return false;

            try
            {
                var type = instance.GetType();

                var method = type.GetMethod("IsPaused", InstanceFlags, null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(bool)
                    && method.Invoke(instance, null) is bool methodValue)
                {
                    paused = methodValue;
                    return true;
                }

                var field = type.GetField("m_Paused", InstanceFlags);
                if (field != null && field.FieldType == typeof(bool)
                    && field.GetValue(instance) is bool fieldValue)
                {
                    paused = fieldValue;
                    return true;
                }

                var property = type.GetProperty("paused", InstanceFlags);
                if (property != null
                    && property.PropertyType == typeof(bool)
                    && property.GetIndexParameters().Length == 0
                    && property.GetValue(instance, null) is bool propertyValue)
                {
                    paused = propertyValue;
                    return true;
                }
            }
            catch
            {
                // Runtime compatibility probe: unreadable members are treated as unknown.
            }

            return false;
        }
    }
}
