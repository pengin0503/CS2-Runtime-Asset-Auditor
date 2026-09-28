using System.Runtime.Serialization;

namespace CS2RuntimeAssetAuditor.Assets.UI
{
    [DataContract]
    public sealed class UiExportRequest
    {
        [DataMember(Name = "format", Order = 1)] public string Format { get; set; } = "Json";
        [DataMember(Name = "scope", Order = 2)] public string Scope { get; set; } = "Full";
        [DataMember(Name = "selectedKeys", Order = 3)] public UiPrefabKey[] SelectedKeys { get; set; } = new UiPrefabKey[0];
    }

    [DataContract]
    public sealed class UiPrefabKey
    {
        [DataMember(Name = "prefabId", Order = 1)] public string PrefabId { get; set; } = string.Empty;
        [DataMember(Name = "prefabType", Order = 2)] public string PrefabType { get; set; } = string.Empty;
    }
}
