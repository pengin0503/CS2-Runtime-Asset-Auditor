using System;
using System.Collections.Generic;
using System.Linq;
using CS2RuntimeAssetAuditor.Assets.Core.Observations;

namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    public sealed class MaterialBindingObservation
    {
        public MaterialBindingObservation(string materialName, string shaderName, IEnumerable<string> shaderKeywords, int renderQueue, int passCount, bool enableInstancing)
        {
            MaterialName = materialName ?? string.Empty;
            ShaderName = shaderName ?? string.Empty;
            ShaderKeywords = Array.AsReadOnly((shaderKeywords ?? throw new ArgumentNullException(nameof(shaderKeywords))).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            RenderQueue = renderQueue;
            PassCount = passCount;
            EnableInstancing = enableInstancing;
        }
        public string MaterialName { get; }
        public string ShaderName { get; }
        public IReadOnlyList<string> ShaderKeywords { get; }
        public int RenderQueue { get; }
        public int PassCount { get; }
        public bool EnableInstancing { get; }
    }

    public sealed class DeepInspectionObservation
    {
        private DeepInspectionObservation(Availability availability, IEnumerable<MaterialBindingObservation> materials, IEnumerable<string> surfaceAssetIds, DateTimeOffset capturedAt, string? diagnosticCode)
        {
            Availability = availability;
            Materials = Array.AsReadOnly((materials ?? throw new ArgumentNullException(nameof(materials))).ToArray());
            SurfaceAssetIds = Array.AsReadOnly((surfaceAssetIds ?? throw new ArgumentNullException(nameof(surfaceAssetIds))).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
            CapturedAt = capturedAt;
            DiagnosticCode = diagnosticCode;
        }
        public Availability Availability { get; }
        public IReadOnlyList<MaterialBindingObservation> Materials { get; }
        public IReadOnlyList<string> SurfaceAssetIds { get; }
        public DateTimeOffset CapturedAt { get; }
        public string? DiagnosticCode { get; }
        public static DeepInspectionObservation Available(IEnumerable<MaterialBindingObservation> materials, IEnumerable<string> surfaceAssetIds, DateTimeOffset capturedAt)
            => new DeepInspectionObservation(Availability.Available, materials, surfaceAssetIds, capturedAt, null);
        public static DeepInspectionObservation Unavailable(Availability availability, DateTimeOffset capturedAt, string? diagnosticCode = null)
        {
            if (availability == Availability.Available) throw new ArgumentException("Unavailable inspection cannot be Available.", nameof(availability));
            return new DeepInspectionObservation(availability, Array.Empty<MaterialBindingObservation>(), Array.Empty<string>(), capturedAt, diagnosticCode);
        }
    }

    public sealed class RenderAssetRecord
    {
        public RenderAssetRecord(RenderAssetKey key, string displayName, DeepInspectionObservation? deepInspection = null)
        {
            if (!key.IsValid) throw new ArgumentException("A stable render-asset key is required.", nameof(key));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A render-asset display name is required.", nameof(displayName));
            Key = key;
            DisplayName = displayName;
            DeepInspection = deepInspection;
        }
        public RenderAssetKey Key { get; }
        public string DisplayName { get; }
        public DeepInspectionObservation? DeepInspection { get; }
        public RenderAssetRecord WithDeepInspection(DeepInspectionObservation observation)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            return new RenderAssetRecord(Key, DisplayName, observation);
        }
        public RenderAssetRecord WithoutDeepInspection() => DeepInspection == null ? this : new RenderAssetRecord(Key, DisplayName);
    }
}
