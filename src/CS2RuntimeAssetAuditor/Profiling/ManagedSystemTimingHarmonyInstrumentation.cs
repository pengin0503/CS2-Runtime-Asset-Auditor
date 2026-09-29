using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using CS2RuntimeAssetAuditor.Core;
using Unity.Entities;

namespace CS2RuntimeAssetAuditor.Profiling
{
    internal sealed class ManagedSystemTimingHarmonyInstrumentation : IDisposable
    {
        private const string HarmonyId = "CS2RuntimeAssetAuditor.ManagedSystemTiming";
        private static readonly Assembly ProfilerAssembly = typeof(ManagedSystemTimingHarmonyInstrumentation).Assembly;

        private object _harmony;
        private Type _harmonyType;
        private bool _installed;

        public bool TryInstall(out string reason)
        {
            reason = null;
            if (_installed)
                return true;

            try
            {
                var harmonyAssembly = HarmonyRuntimeResolver.Resolve(
                    AppDomain.CurrentDomain.GetAssemblies(),
                    Assembly.Load,
                    Assembly.LoadFrom,
                    GetProfilerAssemblyDirectory());
                if (harmonyAssembly == null)
                {
                    reason = "Harmony runtime is unavailable.";
                    return false;
                }

                _harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", throwOnError: false);
                var harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", throwOnError: false);
                if (_harmonyType == null || harmonyMethodType == null)
                {
                    reason = "Harmony types are unavailable.";
                    return false;
                }

                var harmonyCtor = _harmonyType.GetConstructor(new[] { typeof(string) });
                var harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmonyCtor == null || harmonyMethodCtor == null)
                {
                    reason = "Harmony constructor API is incompatible with this build.";
                    return false;
                }

                var original = typeof(SystemBase).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                var prefix = typeof(ManagedSystemTimingHarmonyInstrumentation).GetMethod(
                    nameof(Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                var postfix = typeof(ManagedSystemTimingHarmonyInstrumentation).GetMethod(
                    nameof(Postfix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (original == null || prefix == null || postfix == null)
                {
                    reason = "Unity.Entities.SystemBase.Update instrumentation boundary is unavailable.";
                    return false;
                }

                _harmony = harmonyCtor.Invoke(new object[] { HarmonyId });
                var prefixPatch = harmonyMethodCtor.Invoke(new object[] { prefix });
                var postfixPatch = harmonyMethodCtor.Invoke(new object[] { postfix });

                var patch = _harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => string.Equals(method.Name, "Patch", StringComparison.Ordinal))
                    .Select(method => new { Method = method, Parameters = method.GetParameters() })
                    .FirstOrDefault(candidate => candidate.Parameters.Length >= 3
                        && typeof(MethodBase).IsAssignableFrom(candidate.Parameters[0].ParameterType)
                        && candidate.Parameters.Any(parameter => string.Equals(parameter.Name, "prefix", StringComparison.OrdinalIgnoreCase))
                        && candidate.Parameters.Any(parameter => string.Equals(parameter.Name, "postfix", StringComparison.OrdinalIgnoreCase)));
                if (patch == null)
                {
                    reason = "Harmony Patch API is incompatible with this build.";
                    return false;
                }

                var arguments = new object[patch.Parameters.Length];
                arguments[0] = original;
                for (var i = 1; i < patch.Parameters.Length; i++)
                {
                    if (string.Equals(patch.Parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase))
                        arguments[i] = prefixPatch;
                    else if (string.Equals(patch.Parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        arguments[i] = postfixPatch;
                    else
                        arguments[i] = null;
                }

                patch.Method.Invoke(_harmony, arguments);
                _installed = true;
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.GetBaseException().Message;
                SafeUnpatch();
                return false;
            }
        }

        public void Dispose()
        {
            SafeUnpatch();
            _installed = false;
            _harmony = null;
            _harmonyType = null;
        }

        private static string GetProfilerAssemblyDirectory()
        {
            try
            {
                var location = ProfilerAssembly.Location;
                return string.IsNullOrWhiteSpace(location) ? null : Path.GetDirectoryName(location);
            }
            catch
            {
                return null;
            }
        }

        private void SafeUnpatch()
        {
            if (_harmony == null || _harmonyType == null)
                return;

            try
            {
                var unpatchSelf = _harmonyType.GetMethod(
                    "UnpatchSelf",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                if (unpatchSelf != null)
                {
                    unpatchSelf.Invoke(_harmony, null);
                    return;
                }

                var unpatchAll = _harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method => string.Equals(method.Name, "UnpatchAll", StringComparison.Ordinal)
                        && method.GetParameters().Length == 1
                        && method.GetParameters()[0].ParameterType == typeof(string));
                unpatchAll?.Invoke(_harmony, new object[] { HarmonyId });
            }
            catch
            {
                // Never disturb shutdown because optional instrumentation could not be removed cleanly.
            }
        }

        // Managed system updates run on the main thread; the stack pairs each prefix with its postfix.
        [ThreadStatic]
        private static NestedTimingStack _calls;

        internal static void ResetCallStack() => _calls?.Reset();

        // __state carries the stack token (0 = not tracked). Recording exclusive time keeps a system that updates
        // other systems (for example a simulation phase runner) from being charged its children's time again.
        private static void Prefix(SystemBase __instance, out long __state)
        {
            __state = 0L;
            if (!ManagedSystemTimingBridge.IsActive || __instance == null)
                return;

            var type = __instance.GetType();
            if (type.Assembly == ProfilerAssembly)
                return;

            __state = (_calls ?? (_calls = new NestedTimingStack())).Push(Stopwatch.GetTimestamp());
        }

        private static void Postfix(SystemBase __instance, long __state)
        {
            // Pop even after the capture ended so a later capture never starts on a stale stack.
            if (__state <= 0L || _calls == null
                || !_calls.TryPop((int)__state, Stopwatch.GetTimestamp(), out _, out var exclusiveTicks))
                return;
            if (__instance == null || !ManagedSystemTimingBridge.IsActive)
                return;

            var systemId = __instance.GetType().FullName;
            if (string.IsNullOrWhiteSpace(systemId))
                return;

            var milliseconds = exclusiveTicks * 1000d / Stopwatch.Frequency;
            ManagedSystemTimingBridge.Record(systemId, milliseconds);
        }
    }
}
