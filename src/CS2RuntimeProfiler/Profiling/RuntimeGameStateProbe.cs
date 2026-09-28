using System;
using System.Reflection;
using CS2RuntimeProfiler.Core;
using Game.SceneFlow;

namespace CS2RuntimeProfiler.Profiling
{
    internal static class RuntimeGameStateProbe
    {
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool IsAutomaticCaptureAllowed()
        {
            return IsAutomaticCaptureAllowed(simulationSystem: null);
        }

        public static bool IsAutomaticCaptureAllowed(object simulationSystem)
        {
            var gameManager = GameManager.instance;
            if (gameManager == null)
                return false;

            var loading = TryReadBool(gameManager,
                new[] { "isGameLoading", "IsGameLoading" },
                new[] { "m_IsGameLoading", "isGameLoading" },
                new[] { "get_isGameLoading" },
                fallback: false);
            var paused = simulationSystem != null
                && RuntimePauseStateReader.TryRead(simulationSystem, out var simulationPaused)
                && simulationPaused;

            return AutomaticCapturePolicy.IsAllowed(loading, paused);
        }

        private static bool TryReadBool(
            object instance,
            string[] propertyNames,
            string[] fieldNames,
            string[] getterNames,
            bool fallback)
        {
            if (instance == null)
                return fallback;

            try
            {
                var type = instance.GetType();
                foreach (var name in propertyNames ?? Array.Empty<string>())
                {
                    var property = type.GetProperty(name, InstanceFlags);
                    if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                        return (bool)property.GetValue(instance, null);
                }

                foreach (var name in getterNames ?? Array.Empty<string>())
                {
                    var getter = type.GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null);
                    if (getter != null && getter.ReturnType == typeof(bool))
                        return (bool)getter.Invoke(instance, null);
                }

                foreach (var name in fieldNames ?? Array.Empty<string>())
                {
                    var field = type.GetField(name, InstanceFlags);
                    if (field != null && field.FieldType == typeof(bool))
                        return (bool)field.GetValue(instance);
                }
            }
            catch
            {
                // Compatibility gate: if a future build changes a member, leave only that signal fail-open.
            }

            return fallback;
        }
    }
}
