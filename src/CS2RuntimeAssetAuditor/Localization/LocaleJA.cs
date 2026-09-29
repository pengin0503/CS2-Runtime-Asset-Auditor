using System.Collections.Generic;
using Colossal;

namespace CS2RuntimeAssetAuditor.Localization
{
    public sealed class LocaleJA : IDictionarySource
    {
        private readonly Setting _setting;

        public LocaleJA(Setting setting)
        {
            _setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { _setting.GetSettingsLocaleID(), "CS2 Runtime Asset Auditor" },
                { _setting.GetOptionTabLocaleID(Setting.MainTab), "全般" },
                { _setting.GetOptionGroupLocaleID(Setting.MonitoringGroup), "監視" },
                { _setting.GetOptionGroupLocaleID(Setting.DisplayGroup), "表示" },
                { _setting.GetOptionGroupLocaleID(Setting.CaptureGroup), "キャプチャ" },
                { _setting.GetOptionGroupLocaleID(Setting.AdvancedGroup), "高度な設定" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnableMonitoring)), "監視を有効化" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnableMonitoring)), "低負荷の常時監視と詳細キャプチャを有効にします。無効にするとプロファイラーの収集処理を停止します。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnableAutomaticCapture)), "自動キャプチャを有効化" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnableAutomaticCapture)), "シミュレーション効率が設定した閾値を一定時間下回ったときに詳細キャプチャを自動開始します。手動キャプチャには影響しません。" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.UiScalePercent)), "UI倍率" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.UiScalePercent)), "診断パネル全体の表示倍率を75～150%で調整します。" },

                { _setting.GetOptionGroupLocaleID(Setting.KeyBindingGroup), "キー割り当て" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.TogglePanelBinding)), "診断パネルの開閉" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.TogglePanelBinding)), "プレイ中に診断パネルを開閉するキーです。初期状態ではキーを割り当てていません。左上のランチャーアイコンはキーの設定に関係なく使えます。" },
                { _setting.GetBindingKeyLocaleID(Setting.TogglePanelActionName), "診断パネルの開閉" },
                { _setting.GetBindingMapLocaleID(), "CS2 Runtime Asset Auditor" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.UiRefreshMilliseconds)), "UI更新間隔（ミリ秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.UiRefreshMilliseconds)), "プロファイラーUIの更新頻度を調整します。大きい値ほどUI更新の負荷が下がります。" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.EfficiencyThresholdPercent)), "自動キャプチャ閾値（%）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EfficiencyThresholdPercent)), "実効速度 ÷ 指定速度がこの割合を下回ると、自動キャプチャ判定を開始します。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.LowEfficiencySustainSeconds)), "低効率の継続時間（秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.LowEfficiencySustainSeconds)), "自動キャプチャを開始するまで低効率状態が継続する必要がある時間です。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.PrebufferSeconds)), "事前バッファ時間（秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.PrebufferSeconds)), "詳細キャプチャ開始前の通常監視履歴を何秒分含めるか指定します。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.DeepCaptureSeconds)), "詳細キャプチャ時間（秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.DeepCaptureSeconds)), "広い範囲のプロファイラーマーカーを収集する詳細キャプチャの継続時間です。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.PostbufferSeconds)), "事後バッファ時間（秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.PostbufferSeconds)), "詳細キャプチャ終了後に通常指標を追跡し続ける時間です。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.CooldownSeconds)), "再キャプチャ待機時間（秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CooldownSeconds)), "自動キャプチャ完了後、次の自動キャプチャ判定へ戻るまでの待機時間です。手動キャプチャは待機中でも開始できます。" },

                { _setting.GetOptionLabelLocaleID(nameof(Setting.SamplingIntervalMilliseconds)), "通常監視のサンプリング間隔（ミリ秒）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.SamplingIntervalMilliseconds)), "通常監視の収集間隔です。短くすると時間分解能が上がりますが、プロファイラー自身の負荷も増えます。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.MaxConcurrentMarkers)), "同時プロファイラーマーカー数" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MaxConcurrentMarkers)), "詳細キャプチャで同時に有効化するマーカー数の上限です。高いほど短時間で広く測れますが負荷が増えます。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ProfilerOverheadLimitPercent)), "許容プロファイラー負荷（%）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ProfilerOverheadLimitPercent)), "詳細キャプチャ中にこの割合を繰り返し超えた場合、同時マーカー数やサンプリング頻度を自動的に下げます。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.MaxCompletedCaptures)), "保持するキャプチャ数" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MaxCompletedCaptures)), "ゲーム内で保持する完了済みキャプチャ履歴の最大数です。上限を下げると古い履歴から削除されます。" },
                { _setting.GetOptionGroupLocaleID(Setting.ScanningGroup), "スキャン" },
                { _setting.GetOptionGroupLocaleID(Setting.AnalysisGroup), "分析" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.CollectSubordinateObjects)), "従属オブジェクトを収集" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CollectSubordinateObjects)), "対応する従属オブジェクトを次回の Census に含めます。無効時は 0 ではなく未スキャンとして扱います。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.CollectNetworkEdges)), "ネットワークエッジを収集" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.CollectNetworkEdges)), "対応するネットワークエッジの証拠を次回の Census に含めます。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.FrameBudgetMsOption)), "フレーム予算（ms）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.FrameBudgetMsOption)), "監査処理を分割実行するときに 1 フレームで使用する管理処理時間の上限です。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ProgressUpdateMs)), "進捗更新間隔（ms）" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ProgressUpdateMs)), "スキャン中に進捗だけが変化した場合の UI 更新間隔です。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.RefreshCatalogAtScanStart)), "スキャン開始時にカタログを更新" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.RefreshCatalogAtScanStart)), "次回の Asset Audit 開始前に Prefab カタログを更新します。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnableHeuristicFindings)), "ヒューリスティック検出" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnableHeuristicFindings)), "バージョン管理された証拠ベースのヒューリスティック検出を有効にします。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.EnablePeerOutliers)), "同種アセット外れ値分析" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.EnablePeerOutliers)), "同じ Prefab 種別の比較対象と比べて、LOD0 頂点数または推定テクスチャ容量が極端に大きい Prefab を所見として示します（比較対象5件以上、P95 超かつ中央値の2倍以上）。次回の Asset Audit から反映されます。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ComparisonPopulationOption)), "比較対象" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ComparisonPopulationOption)), "外れ値分析で比較するアセットの範囲です。比較は常に同じ Prefab 種別の中で行います。次回の Asset Audit から反映されます。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.ShowNoticeFindings)), "Notice を表示" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.ShowNoticeFindings)), "Warnings 画面に情報レベルの Notice を表示します。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.PageSize)), "アセットページサイズ" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.PageSize)), "UI が 1 回に要求するアセット件数です。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.MetadataCacheLimit)), "メタデータキャッシュ上限" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.MetadataCacheLimit)), "Asset Audit 中にメモリへ保持するサーフェスメタデータ（参照するテクスチャアセットを含む）の件数上限です。小さくするとメモリ使用量は減りますが、共有サーフェスを再読み込みする場合があります。" },
                { _setting.GetOptionLabelLocaleID(nameof(Setting.DeepInspectionLimit)), "Deep Inspection 上限" },
                { _setting.GetOptionDescLocaleID(nameof(Setting.DeepInspectionLimit)), "Deep Inspection の結果を同時に保持するレンダーアセット数の上限です。上限を超えると古い結果から破棄します。" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.SameCategory), "同一カテゴリ" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.BuiltinDlc), "バニラ / DLC" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.Custom), "カスタムアセット" },
                { _setting.GetEnumValueLocaleID(Setting.ComparisonPopulationChoice.SameSourcePack), "同一ソースパック" }
            };
        }

        public void Unload()
        {
        }
    }
}
