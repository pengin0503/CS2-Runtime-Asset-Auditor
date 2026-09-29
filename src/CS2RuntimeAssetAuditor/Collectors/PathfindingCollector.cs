using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CS2RuntimeAssetAuditor.Core;

namespace CS2RuntimeAssetAuditor.Collectors
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
            ReadPathfindActions(out var pending, out var inFlight);
            metrics.Add(pending);
            metrics.Add(inFlight);
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

        // PathfindQueueSystem.ActionList (Game 1.6.2f1): m_Items is a List. Enqueue appends; dispatching an item to a
        // worker advances m_NextIndex; PathfindResultSystem removes finished items and moves m_NextIndex back. Items
        // before m_NextIndex are therefore in flight and items from m_NextIndex on are still waiting.
        private void ReadPathfindActions(out NamedMetricValue pending, out NamedMetricValue inFlight)
        {
            const string pendingId = "pendingPathfindActions";
            const string inFlightId = "inFlightPathfindActions";
            string reason;
            if (_queueSystem == null || _pathfindActions == null)
            {
                reason = "m_PathfindActions is not available in this runtime build.";
                pending = NamedMetricValue.Unavailable(pendingId, reason);
                inFlight = NamedMetricValue.Unavailable(inFlightId, reason);
                return;
            }

            try
            {
                var actionList = _pathfindActions.GetValue(_queueSystem);
                var type = actionList?.GetType();
                var items = type?.GetField("m_Items", Flags)?.GetValue(actionList) as ICollection;
                var nextIndexField = type?.GetField("m_NextIndex", Flags);
                if (items == null || nextIndexField == null || nextIndexField.FieldType != typeof(int))
                {
                    reason = "ActionList layout (List m_Items, int m_NextIndex) is not present in this runtime build.";
                }
                else
                {
                    var count = items.Count;
                    var nextIndex = (int)nextIndexField.GetValue(actionList);
                    if (nextIndex >= 0 && nextIndex <= count)
                    {
                        pending = NamedMetricValue.Available(pendingId, count - nextIndex, MetricConfidence.Indirect);
                        inFlight = NamedMetricValue.Available(inFlightId, nextIndex, MetricConfidence.Indirect);
                        return;
                    }
                    reason = "ActionList m_NextIndex is outside its item list.";
                }
            }
            catch (Exception ex)
            {
                reason = $"Reading pathfind action queue failed: {RootMessage(ex)}";
            }

            pending = NamedMetricValue.Unavailable(pendingId, reason);
            inFlight = NamedMetricValue.Unavailable(inFlightId, reason);
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
