# CS2 Runtime Asset Auditor

Cities: Skylines II の実行時パフォーマンスとアセットの静的な構造・都市内での配置状況を、ひとつの Code Mod で調査します。ゲーム画面左上のランチャーからパネルを開きます。概要、ランタイム、アセット、改善提案、診断を同じ画面で切り替えられます。

## 使い方

- **ランタイム**: 通常監視の指標を確認し、必要に応じて手動または自動トリガーの Deep Capture を実行します。システム、MOD、経路探索、タイムライン、完了したキャプチャを表示します。キャプチャを選んで「アセットを調査」へ移動できます。
- **アセット**: 手動で Census または Asset Audit を実行します。Prefab 一覧の検索・絞り込み・ページ分割、Census の配置数、形状・LOD・マテリアル・テクスチャの所見、比較、設定を利用できます。アセット詳細からランタイムキャプチャに戻れます。
- **改善提案**: キャプチャを選んで診断し、根拠と信頼度を確認します。適用可能な標準設定は個別に Apply/Undo できます。設定が別の場所で変わった場合は競合を表示します。自動適用は行いません。
- **診断**: 計測の利用可能性、カバレッジ、警告を確認します。

パネルはドラッグで移動、右下の角でサイズを変更できます。Esc またはゲームパッドの B で閉じます。位置・サイズは保存され、ヘッダーからリセットできます。

## 根拠の読み方

Runtime の `Full`、`Managed`、`Indirect`、`Unavailable` は計測の信頼度です。`Managed` は Job/Burst の実行時間を含まない場合があります。Asset の `Available`、`NotScanned`、`Unsupported`、`Failed` などは収集状態です。`NotScanned` や `Unavailable` はゼロを意味しません。

Asset の頂点数、テクスチャ情報、配置数は調査の手がかりであり、アセットごとのフレーム時間や GPU 負荷の実測値ではありません。同じ都市セッション内で開始・終了時刻が揃った Runtime キャプチャと Asset スナップショットだけを、前後・重複の時系列として関連付けます。関連付けは因果関係を示しません。

Deep Capture は重い Asset スキャンより優先されます。キャプチャ中に要求されたスキャンは待機し、進行中にキャプチャが始まれば安全な境界で中断します。中断した途中結果は完了スナップショットとして公開されません。再実行は手動で行ってください。Deep Capture の開始によって全 Asset Census を自動実行することはありません。

## レポート

パネルの JSON エクスポートは Runtime、Advisor、Asset、能力状態、診断、同一セッションの時系列リンクを含む単一のレポートを保存します。Asset 側からは絞り込み対象の Asset CSV も保存できます。ユーザーデータディレクトリ以下の保存先は次のとおりです。

- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-report-YYYY-MM-DD_HHmmss_fff.json`
- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-assets-YYYY-MM-DD_HHmmss_fff.csv`

同名ファイルがあれば上書きせず `-1`、`-2` の連番を付けます。ホームパスやアカウント識別子は出力時に置換しますが、共有前には内容を確認してください。CSV のテキストセルは表計算ソフトで数式として解釈されないよう保護します。

## 導入・ビルド

Windows で Cities: Skylines II と公式 Modding Toolchain をセットアップし、`.NET SDK`、Node.js 20/npm を用意してください。公式ツールチェーンが設定する `CSII_TOOLPATH`、`CSII_MANAGEDPATH`、`CSII_USERDATAPATH`、`CSII_LOCALMODSPATH` などが必要です。対応基準のゲームバージョンは Asset コレクター側の `1.6.2f1` です。実ゲームでの最新互換性と Release ビルドは [検証状況](docs/validation/2026-09-28-integration-validation.md)を参照してください。

```powershell
cd UI
npm ci
npm test
npm run build
cd ..
dotnet test .\tests\CS2RuntimeAssetAuditor.Tests\CS2RuntimeAssetAuditor.Tests.csproj -c Release
dotnet test .\tests\CS2RuntimeAssetAuditor.AdapterTests\CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
dotnet build .\src\CS2RuntimeAssetAuditor\CS2RuntimeAssetAuditor.csproj -c Release
```

ビルド成果物は `CS2RuntimeAssetAuditor.dll`、必要な `0Harmony.dll`、ひとつの UI バンドルです。公式 `Mod.props` / `Mod.targets` がローカル Mod 配置を管理します。ビルド後、ゲームのプレイセットで **CS2 Runtime Asset Auditor** を有効にしてください。

設定の保存 ID は `CS2RuntimeAssetAuditor` です。旧 Runtime Profiler / Asset Performance Auditor の保存設定は新しい ID には自動移行されないため、必要な値を設定画面で再指定してください。

## 開発と検証

GitHub Actions はゲームなしで動く .NET 純粋テストと UI テスト・本番ビルドを実行します。Adapter テスト、Mod の Release ビルド、ゲーム内での動作確認にはゲームの管理 DLL と公式ツールチェーンが必要です。結果と未実施項目は [検証状況](docs/validation/2026-09-28-integration-validation.md)に記録しています。

MIT License。
