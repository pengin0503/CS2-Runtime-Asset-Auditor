using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CS2RuntimeProfiler.Core;

namespace CS2RuntimeProfiler.Collectors
{
    public sealed class PathfindingCollector : IMetricCollector
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object _queueSystem;
        private readonly FieldInfo _pathfindActions;
        private readonly FieldInfo _actionTypes;
        private readonly FieldInfo _workerActions;
        private readonly MethodInfo _getGraphSize;
        private readonly MethodInfo _getGraphMemory;
        private readonly MethodInfo _getQueryMemory;
        private int? _previousPending;
        private double? _previousTimestamp;

        public PathfindingCollector(object queueSystem)
        {
            _queueSystem = queueSystem;
            var type = queueSystem?.GetType();
            if (type != null)
            {
                _pathfindActions = type.GetField("m_PathfindActions", Flags);
                _actionTypes = type.GetField("m_ActionTypes", Flags);
                _workerActions = type.GetField("m_WorkerActions", Flags);
                _getGraphSize = type.GetMethod("GetGraphSize", Flags, null, Type.EmptyTypes, null);
                _getGraphMemory = FindMemoryMethod(type, "GetGraphMemory");
                _getQueryMemory = FindMemoryMethod(type, "GetQueryMemory");
            }

            Latest = new NamedMetricSnapshot(0, Array.Empty<NamedMetricValue>());
        }

        public string Name => "Pathfinding";
        public NamedMetricSnapshot Latest { get; private set; }

        public void Sample(double timestampSeconds)
        {
            var metrics = new List<NamedMetricValue>();
            var pending = TryReadPendingPathfindActions();
            metrics.Add(pending);
            metrics.Add(TryReadCollectionCount("actionTypeQueue", _actionTypes));
            metrics.Add(TryReadCollectionCount("workerActionQueue", _workerActions));
            metrics.Add(TryInvokeScalar("graphSize", _getGraphSize));
            AddMemoryMetrics(metrics, "graphMemory", _getGraphMemory);
            AddMemoryMetrics(metrics, "queryMemory", _getQueryMemory);

            if (pending.Availability == MetricAvailability.Available && pending.Value.HasValue &&
                _previousPending.HasValue && _previousTimestamp.HasValue && timestampSeconds > _previousTimestamp.Value)
            {
                metrics.Add(NamedMetricValue.Available(
                    "queueDeltaPerSecond",
                    (pending.Value.Value - _previousPending.Value) / (timestampSeconds - _previousTimestamp.Value),
                    MetricConfidence.Indirect));
            }
            else
            {
                metrics.Add(NamedMetricValue.Unavailable("queueDeltaPerSecond", "A prior verified pending sample is required."));
            }

            metrics.Add(NamedMetricValue.Unavailable(
                "requestsPerSecond",
                "No verified runtime request counter is available for this game build."));
            metrics.Add(NamedMetricValue.Unavailable(
                "resultsPerSecond",
                "No verified runtime result counter is available for this game build."));

            if (pending.Availability == MetricAvailability.Available && pending.Value.HasValue)
            {
                _previousPending = (int)pending.Value.Value;
                _previousTimestamp = timestampSeconds;
            }

            Latest = new NamedMetricSnapshot(timestampSeconds, metrics);
        }

        private NamedMetricValue TryReadPendingPathfindActions()
        {
            const string id = "pendingPathfindActions";
            if (_queueSystem == null || _pathfindActions == null)
                return NamedMetricValue.Unavailable(id, "m_PathfindActions is not available in this runtime build.");

            try
            {
                var actionList = _pathfindActions.GetValue(_queueSystem);
                if (actionList == null)
                    return NamedMetricValue.Unavailable(id, "m_PathfindActions returned null.");

                var type = actionList.GetType();
                var itemsField = type.GetField("m_Items", Flags);
                var nextIndexField = type.GetField("m_NextIndex", Flags);
                if (itemsField == null || nextIndexField == null)
                    return NamedMetricValue.Unavailable(id, "ActionList layout is not verified in this runtime build.");

                var items = itemsField.GetValue(actionList);
                if (items == null || !TryGetCollectionLength(items, out var capacity))
                    return NamedMetricValue.Unavailable(id, "ActionList m_Items has no verified Count or Length property.");

                var nextIndex = Convert.ToInt32(nextIndexField.GetValue(actionList));
                // Game.dll's Enqueue increments m_NextIndex as it writes each queued item;
                // Clear resets it. m_Items is fixed-capacity NativeArray storage in the
                // supplied game build, so its Length is capacity, not the pending count.
                if (nextIndex < 0 || nextIndex > capacity)
                    return NamedMetricValue.Unavailable(id, "ActionList m_NextIndex is outside the verified m_Items bounds.");

                return NamedMetricValue.Available(id, nextIndex, MetricConfidence.Indirect);
            }
            catch (Exception ex)
            {
                return NamedMetricValue.Unavailable(id, $"Reading pathfind action queue failed: {RootMessage(ex)}");
            }
        }

        private static bool TryGetCollectionLength(object value, out int length)
        {
            if (value is ICollection collection)
            {
                length = collection.Count;
                return length >= 0;
            }

            var property = value.GetType().GetProperty("Length", Flags);
            if (property == null || property.PropertyType != typeof(int) || property.GetIndexParameters().Length != 0)
            {
                length = 0;
                return false;
            }

            var rawLength = property.GetValue(value, null);
            if (!(rawLength is int nativeLength))
            {
                length = 0;
                return false;
            }

            length = nativeLength;
            return length >= 0;
        }

        private NamedMetricValue TryReadCollectionCount(string id, FieldInfo field)
        {
            if (_queueSystem == null || field == null)
                return NamedMetricValue.Unavailable(id, $"Runtime field for '{id}' is not available.");

            try
            {
                var value = field.GetValue(_queueSystem);
                if (value != null && TryGetCollectionLength(value, out var count))
                    return NamedMetricValue.Available(id, count, MetricConfidence.Indirect);
                return NamedMetricValue.Unavailable(id, $"Runtime field for '{id}' has no verified Count or Length property.");
            }
            catch (Exception ex)
            {
                return NamedMetricValue.Unavailable(id, $"Reading '{id}' failed: {RootMessage(ex)}");
            }
        }

        private NamedMetricValue TryInvokeScalar(string id, MethodInfo method)
        {
            if (_queueSystem == null || method == null)
                return NamedMetricValue.Unavailable(id, $"Runtime method for '{id}' is not available.");

            try
            {
                var value = method.Invoke(_queueSystem, null);
                return NamedMetricValue.Available(id, Convert.ToDouble(value), MetricConfidence.Full);
            }
            catch (Exception ex)
            {
                return NamedMetricValue.Unavailable(id, $"Reading '{id}' failed: {RootMessage(ex)}");
            }
        }

        private void AddMemoryMetrics(List<NamedMetricValue> metrics, string prefix, MethodInfo method)
        {
            var usedId = prefix + "Used";
            var allocatedId = prefix + "Allocated";
            if (_queueSystem == null || method == null)
            {
                metrics.Add(NamedMetricValue.Unavailable(usedId, $"Runtime method for '{prefix}' is not available."));
                metrics.Add(NamedMetricValue.Unavailable(allocatedId, $"Runtime method for '{prefix}' is not available."));
                return;
            }

            try
            {
                var args = new object[] { 0u, 0u };
                method.Invoke(_queueSystem, args);
                metrics.Add(NamedMetricValue.Available(usedId, Convert.ToDouble(args[0]), MetricConfidence.Full, MetricUnits.Bytes));
                metrics.Add(NamedMetricValue.Available(allocatedId, Convert.ToDouble(args[1]), MetricConfidence.Full, MetricUnits.Bytes));
            }
            catch (Exception ex)
            {
                var reason = $"Reading '{prefix}' failed: {RootMessage(ex)}";
                metrics.Add(NamedMetricValue.Unavailable(usedId, reason));
                metrics.Add(NamedMetricValue.Unavailable(allocatedId, reason));
            }
        }

        private static MethodInfo FindMemoryMethod(Type type, string name)
        {
            foreach (var method in type.GetMethods(Flags))
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType.IsByRef && parameters[1].ParameterType.IsByRef)
                    return method;
            }
            return null;
        }

        private static string RootMessage(Exception ex)
        {
            if (ex is TargetInvocationException tie && tie.InnerException != null)
                return tie.InnerException.Message;
            return ex.Message;
        }
    }
}
