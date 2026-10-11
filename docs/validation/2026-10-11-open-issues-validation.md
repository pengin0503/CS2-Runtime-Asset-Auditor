# オープン issue（#38, #46〜#59）対応の検証状況 — 2026-10-11

2026-10-11 時点のオープン issue をコードで対応した。ここには、実施した検証と、実機でしか確かめられない残りの項目を記録する。自動テストの成功は、実機での PASS として扱わない。

## コミット

| issue | コミット | 内容 |
| --- | --- | --- |
| #38 | ae4719d | 通常監視のサンプル・事前バッファ・差分の基準を、都市セッションの世代で分ける。メニュー・エディター・ロード中は採取しない |
| #51 | 3d2180a | Surface/Texture の段階を、1 回の読み取り単位で中断・再開する。予算超過のスライス数と、分割できない読み取りの最大時間をログに出す |
| #55 | 20a898f | レンダースレッド律速（`RenderThreadCpu`）とフレーム上限（`PresentLimited`）を、GPU 律速と分けて判定する |
| #53, #54 | c40e19f | 描画カウンターを通常監視・CSV・概要・JSON に出す。`Video Memory Bytes` は容量と一致したときだけ「容量」と表示し、使用量としては表示しない |
| #52 | a808010 | ObjectGeometry に壊れたメッシュ参照があるとき、Supported にしない |
| #50 | 83fad01 | Census のネットワーク取得時刻（`censusNetworkCapturedAt`）を記録し、UI に時刻差を表示する |
| #46 | 3584190 | ソース文字列の検査を、挙動を確かめるテストに置き換える |
| #56 | 4d885f3 | `TextureStreamingSystem` の VT 状態を読み取り、CSV・概要・JSON に出す |
| #57 | 02e9bfa | 影・詳細度・動的解像度・テクスチャの品質を、1 段階下げる試行候補として提案する |
| #58 | 5761023 | ゲーム内蔵ベンチマークの最新結果を `runtime.gameBenchmark` に入れる |
| #59 | f1b493b | 選択肢が 4 つ以上の選択を、cs2/ui の `Dropdown` にする |
| #47, #49 | 527e4c1 | 計測用の補助。解析を公開するときにキャッシュの件数をログに出す。CSV に `assetScan` / `assetUiPublishes` / `assetTelemetrySnapshots` の列を追加する |

## 自動検証（この作業環境で実行）

| 対象 | 結果 | 根拠・制約 |
| --- | --- | --- |
| C# テスト（`dotnet test -c Release`） | PASS（630件） | Unity と ECS に依存する部分は、純粋なクラス（`RecorderHandleTable`、`PanelVisibility`、`QualityGroupRules`、`ReportGameBenchmark` など）に切り出してテストした |
| UI テスト（vitest）/ 型チェック（tsc） | PASS（133件）/ PASS | cs2/ui と cs2/input のスタブは、呼び出しを記録する。Dropdown のスタブは開閉と選択を再現する |
| MOD 全体のコンパイル | 0 エラー | ゲームの 1.6.2f1 DLL（`CS2-All-Managed-1.6.2f1-fix`）を参照してコンパイルした。公式 Toolchain の後処理は実行していない |
| UI バンドル（webpack） | PASS | `Dropdown` / `DropdownItem` / `DropdownToggle` は、外部の `cs2/ui` として参照される |
| ゲームの実装との照合 | 実施 | ゲームの DLL と `index.js` をデコンパイルして照合した。対象は `TextureStreamingSystem`、`QualitySetting<T>`（`GetLevel` / `SetLevel` / `IsOptionFullyDisabled`）、`BenchmarkUISystem` / `BenchmarkSettings` / `BenchmarkRunData`、ドロップダウンの部品 |

## 実ゲームで追加確認が必要な項目

| # | issue | シナリオ | 確認点 | 結果 |
| --- | --- | --- | --- | --- |
| V1 | #38 | 都市 A → メニュー → 都市 B、同じ都市の再ロード、監視の ON/OFF | 都市 B に入ってから 5 秒以内の手動キャプチャと概要に、都市 A やロード中の値が出ない。ロード記録（`loading`）は残る | NOT RUN |
| V2 | #51 | 大規模なアセット環境で Asset Audit を予算 0.25 / 2 / 8 ms で実行し、MOD ログの `Asset scan slice timing` を見る | `overBudgetSlices` と `maxIndivisibleReadMs` が記録される。実行前後で、フレーム時間の P95/P99 と監査結果が変わらない | NOT RUN |
| V3 | #55 | GPU 律速の場面（高画質・高解像度）と描画命令の多い場面（引きのカメラ）、および VSync ON で、それぞれキャプチャ→診断 | それぞれ `RenderingGpu` / `RenderThreadCpu` / `PresentLimited` に分かれる。根拠に `cpu.render.ms` が出る | NOT RUN |
| V4 | #53 | 通常監視の診断ログを、描画カウンターの追加前後で比較する | `modUpdateMs*` と `gcCollections` の増分。カメラの向きや画質を変えると、カウンターの値が変わる | NOT RUN |
| V5 | #54 | 別の GPU やドライバーで Deep Capture を取る | `render.videoMemoryBytes.*` が、容量と一致するときだけ `capacity` になる | NOT RUN |
| V6 | #50 | 長い Census スキャン（ネットワークを含む）を実行する | Census タブと概要に、オブジェクトとネットワークの時刻、およびその差が出る。JSON の `censusNetworkCapturedAt` と一致する | NOT RUN |
| V7 | #46 | パネルを開いて Esc（ゲームパッドなら B）を押す | パネルが閉じる。閉じた後の Esc が、ゲーム本来の操作（ポーズメニューなど）を妨げない | NOT RUN |
| V8 | #56 | ロード直後、カメラを素早く動かした直後、静止中、テクスチャ品質の変更直後 | `vtWorkingSetLodBias` が初回の読み戻しまで空欄で、その後 1 以上の値になる。要求数と読み込み待ちのマテリアル数が変化する。MOD の負荷が増えない（V4 と同じ比較） | NOT RUN |
| V9 | #57 | GPU 律速の都市で診断し、影の品質の提案を「適用」→再計測→「元に戻す」 | 提案は 1 段階下で、信頼度は最大でも「中」。適用後、オプション画面の影の品質が変わり、全体の画質プリセットは「カスタム」になる。元に戻すと、元の段階に戻る。DLSS/FSR2 が有効なときは、動的解像度の提案が出ず、適用も拒否される | NOT RUN |
| V10 | #57 | 同じ条件で前後比較（調査実験）を行う | 改善しなかった候補が「確実な最適化」として表示されない | NOT RUN |
| V11 | #58 | ゲーム内蔵ベンチマークを完了してから、都市でレポートを出力する | `runtime.gameBenchmark` の平均 FPS・日時・画質が、ベンチマーク画面の値と一致する。キャンセルした実行は保存されない。MOD の有無でベンチマーク結果を比較し、監視の影響を記録する | NOT RUN |
| V12 | #59 | アセットの種類・提供元・存在・並び順、所見の状態・分類、出力範囲、比較母集団のドロップダウン | マウスで開閉と選択ができ、外側をクリックすると閉じる。キーボードやゲームパッドでフォーカスを移動できる。開いている間の Esc はドロップダウンだけを閉じ、パネルは閉じない | NOT RUN |
| V13 | #47 | 大規模なアセット環境で Asset Audit を実行し、MOD ログの `Asset analysis caches at publish` を見る | geometry/texture の件数と、`renderAssets` との比。`MetadataCacheLimit` を変えると `surfaceCached` の上限と `surfaceEvictions` が変わる。Deep Inspection 上限は、保持する結果の数だけに効く | NOT RUN |
| V14 | #48 | 同じ都市で、外れ値の ON/OFF と比較母集団 4 種を切り替えて監査する | 既知の外れ値に所見が付き、サンプルが不足すると所見が出ない。UI・JSON・CSV で同じ所見が同じアセットに付く | NOT RUN |
| V15 | #49 | 診断ログを有効にし、パネルを開いたままスキャンする。アイドル時と閉じた状態も同様に記録する | スキャン中の `assetUiPublishes` と `assetTelemetrySnapshots` が、1 秒あたり「1000 / 進捗の更新間隔」程度。アイドル中と閉じている間は 0。完了・キャンセル・再表示の直後に表示が最新になる | NOT RUN |

## 単体テストで確認した #48 / #49 の関連テスト（2026-10-11 実行）

`PeerOutlier*`、`AssetStatusFingerprint*`、`UiPublishPolicy*`、`BoundedState*` の 16 件はすべて PASS だった。主なテストは次のとおり。

- 外れ値（#48）: `Vertex_outlier_is_reported_against_the_same_category`、`Populations_below_the_minimum_sample_size_produce_no_findings`、`Builtin_population_compares_custom_assets_against_vanilla_baseline`、`Same_source_pack_skips_assets_without_pack_or_source_evidence`、`Categories_are_never_mixed`
- 保持上限（#47）: `Lru_cache_evicts_the_least_recently_used_entry`、`Deep_inspection_retention_limit_drops_the_oldest_other_results`
- 更新頻度（#49）: `A_running_scan_telemetry_never_matches`、`Scan_progress_is_throttled_to_the_progress_interval`、`Idle_unchanged_state_is_not_rebuilt_however_long_it_has_been`
