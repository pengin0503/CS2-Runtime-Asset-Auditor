namespace CS2RuntimeAssetAuditor.Assets.Core.Rendering
{
    /// <summary>
    /// Coverage of one ObjectGeometry Prefab from what its mesh references resolved to. A Prefab is Supported only
    /// when every reference resolved, so totals built from the resolved part are never shown as the whole Prefab.
    /// </summary>
    public readonly struct MeshReferenceCoverage
    {
        public const string NoMeshRelations = "object_geometry_has_no_mesh_relations";
        public const string MissingReference = "object_geometry_mesh_reference_missing";
        public const string UnsupportedReference = "object_geometry_mesh_type_unsupported";

        private MeshReferenceCoverage(RenderCoverage coverage, string? diagnosticCode)
        {
            Coverage = coverage;
            DiagnosticCode = diagnosticCode;
        }

        public RenderCoverage Coverage { get; }
        public string? DiagnosticCode { get; }

        /// <param name="resolved">References that resolved to a RenderPrefab.</param>
        /// <param name="missing">Null references: a mesh or LOD slot whose asset is not loaded.</param>
        /// <param name="unsupported">References of another render type this mod does not read (for example CharacterGroup).</param>
        public static MeshReferenceCoverage Classify(int resolved, int missing, int unsupported)
        {
            if (missing > 0)
                return new MeshReferenceCoverage(RenderCoverage.Failed, MissingReference);
            if (unsupported > 0)
                return new MeshReferenceCoverage(RenderCoverage.Unknown, UnsupportedReference);
            if (resolved == 0)
                return new MeshReferenceCoverage(RenderCoverage.Unknown, NoMeshRelations);
            return new MeshReferenceCoverage(RenderCoverage.Supported, null);
        }
    }
}
