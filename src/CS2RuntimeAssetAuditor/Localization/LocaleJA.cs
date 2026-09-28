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
                { _setting.GetOptionDescLocaleID(nameof(Setting.UiScalePercent)), "プロファイラーパネルの表示倍率を75～150%で調整します。" },
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
                { _setting.GetOptionDescLocaleID(nameof(Setting.MaxCompletedCaptures)), "ゲーム内で保持する完了済みキャプチャ履歴の最大数です。上限を下げると古い履歴から削除されます。" }
            };
        }

        public void Unload()
        {
        }
    }
}
