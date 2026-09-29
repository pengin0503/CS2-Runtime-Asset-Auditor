# CS2 Runtime Asset Auditor

Cities: Skylines II の実行時パフォーマンスとアセットの静的な構造・都市内での配置状況を、ひとつの Code Mod で調査します。ゲーム画面左上のランチャーからパネルを開きます。概要、ランタイム、アセット、改善提案、診断を同じ画面で切り替えられます。

## 使い方

- **ランタイム**: 通常監視の指標を確認し、必要に応じて手動または自動トリガーの Deep Capture を実行します。システム、MOD、経路探索、タイムライン、完了したキャプチャを表示します。キャプチャを選んで「アセットを調査」へ移動できます。
- **アセット**: 手動で Census または Asset Audit を実行します。Prefab 一覧の検索・絞り込み・ページ分割、Census の配置数、形状・LOD・マテリアル・テクスチャの所見、比較、設定を利用できます。アセット詳細からランタイムキャプチャに戻れます。
- **改善提案**: キャプチャを選んで診断し、根拠と信頼度を確認します。適用可能な標準設定は個別に Apply/Undo できます。操作の結果（拒否された理由を含む）は画面に表示します。「セッションの変更を元に戻す」は確認後に、確認が必要な設定も含めて元に戻します。設定が別の場所で変わった場合は競合を表示します。自動適用は行いません。
- **診断**: 計測の利用可能性、カバレッジ、警告を確認します。

パネルはドラッグで移動、右下の角でサイズを変更できます。Esc またはゲームパッドの B で閉じます。位置・サイズは保存され、ヘッダーからリセットできます。

オプションの「キー割り当て」→「診断パネルの開閉」で、パネルを開閉するキーを設定できます。初期状態ではキーを割り当てていません（他の MOD やゲームのキーと衝突させないため）。キーはプレイ中の都市で有効で、テキスト入力欄にフォーカスがある間は反応しません。割り当ての解除・初期化はゲーム標準のキー設定欄から行えます。左上のランチャーアイコンはキー設定に関係なく使えます。

表示言語はゲームの言語設定に従います。日本語（`ja-*`）では日本語、それ以外の言語では英語で表示します。エクスポートするレポートの文言は英語で統一しています。

## 根拠の読み方

Runtime の `Full`、`Managed`、`Indirect`、`Unavailable` は計測の信頼度です。`Managed` は管理コードの同期実行の自己時間で、入れ子で更新される別の管理システムの時間と Job/Burst の実行時間を含みません。Asset の `Available`、`NotScanned`、`Unsupported`、`Failed` などは収集状態です。`NotScanned` や `Unavailable` はゼロを意味しません。

Asset の頂点数、テクスチャ情報、配置数は調査の手がかりであり、アセットごとのフレーム時間や GPU 負荷の実測値ではありません。同種アセット外れ値分析は、同じ Prefab 種別の比較対象（5件以上）と比べて LOD0 頂点数や推定テクスチャ容量が極端に大きいものを注記として示します。

都市セッションは、ゲームプレイの都市の読み込み完了から次の読み込み開始までです。別の都市を読み込むと、それまでのキャプチャ・Census・分析結果は破棄され、メインメニューやエディターではキャプチャやスキャンを行いません。同じ都市セッション内で開始・終了時刻が揃った Runtime キャプチャと Asset スナップショットだけを、前後・重複の時系列として関連付けます。Asset スナップショットの区間は Asset Audit の開始から公開までで、Deep Inspection による追加は区間を変えません。関連付けは因果関係を示しません。

Deep Capture は重い Asset スキャンより優先されます。キャプチャ中（後処理の区間を含む）に要求されたスキャンは待機し、進行中にキャプチャが始まれば安全な境界で中断します。計測負荷やメモリの安全上限でキャプチャを停止した場合は、クールダウンに入り、停止が続くほど自動キャプチャの再開を遅らせます（最大10分）。中断した途中結果は完了スナップショットとして公開されません。再実行は手動で行ってください。Deep Capture の開始によって全 Asset Census を自動実行することはありません。

## レポート

パネルの JSON エクスポートは Runtime、Advisor、Asset、能力状態、診断、同一セッションの時系列リンクを含む単一のレポートを保存します。Asset 側からは絞り込み対象の Asset CSV も保存できます。ユーザーデータディレクトリ以下の保存先は次のとおりです。

- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-report-YYYY-MM-DD_HHmmss_fff.json`
- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-assets-YYYY-MM-DD_HHmmss_fff.csv`

同名ファイルがあれば上書きせず `-1`、`-2` の連番を付けます。絶対パスは `[redacted-path]`、アカウント名とマシン名は独立した単語として現れる場合だけ `[redacted]` に置換します（3文字未満の名前は置換しません）。共有前には内容を確認してください。CSV のテキストセルは表計算ソフトで数式として解釈されないよう保護します。

## 導入・ビルド

### 1. 前提条件

Windows 環境で、次を準備してください。

- Git
- Cities: Skylines II
- Cities: Skylines II 公式 Modding Toolchain
- .NET 8 SDK
- Node.js 20 / 22 / 24 系と npm

公式 Modding Toolchain が使用する `CSII_TOOLPATH`、`CSII_MANAGEDPATH`、`CSII_USERDATAPATH`、`CSII_LOCALMODSPATH` などの環境変数が設定されている必要があります。Mod の Release ビルドと Adapter テストは、対応するゲーム管理 DLL と公式 Toolchain が利用できる環境で実行してください。

対応基準のゲームバージョンは Asset コレクター側の `1.6.2f1` です。実ゲームでの最新互換性と Release ビルドの検証状況は [検証状況](docs/validation/2026-09-28-integration-validation.md)を参照してください。

### 2. リポジトリを取得

PowerShell を開き、作業したいディレクトリでリポジトリを clone します。

```powershell
git clone https://github.com/pengin0503/CS2-Runtime-Asset-Auditor.git
cd CS2-Runtime-Asset-Auditor
```

必要に応じてツールが利用できることを確認できます。

```powershell
git --version
dotnet --version
node --version
npm --version
```

### 3. UI の依存関係をインストール

```powershell
cd UI
npm.cmd ci
cd ..
```

`npm ci` は `UI/package-lock.json` に固定された依存関係をインストールします。初回 clone 後や lockfile が更新された場合は、Mod をビルドする前に実行してください。

### 4. テストと UI ビルド

UI のテストと本番バンドルを確認します。

```powershell
cd UI
npm.cmd test
npm.cmd run build
cd ..
```

ゲームに依存しない .NET テストは次で実行できます。

```powershell
dotnet test .\tests\CS2RuntimeAssetAuditor.Tests\CS2RuntimeAssetAuditor.Tests.csproj -c Release
```

対応する CS2 管理 DLL と公式 Modding Toolchain が揃っている環境では、Adapter テストも実行します。

```powershell
dotnet test .\tests\CS2RuntimeAssetAuditor.AdapterTests\CS2RuntimeAssetAuditor.AdapterTests.csproj -c Release
```

### 5. Mod を Release ビルド

リポジトリのルートで次を実行します。

```powershell
dotnet build .\src\CS2RuntimeAssetAuditor\CS2RuntimeAssetAuditor.csproj -c Release
```

`CS2RuntimeAssetAuditor.csproj` はビルド時に `UI` の本番ビルドも実行し、UI バンドルを DLL と同じ出力フォルダーに作成します。ソリューション経由でもプロジェクト単体のビルドでも同じです。事前に `npm ci` で UI の依存関係をインストールしておく必要があり、未インストールの場合や UI ファイルが生成されなかった場合はビルドがエラーで止まります。

ビルド成果物には `CS2RuntimeAssetAuditor.dll`、必要な `0Harmony.dll`、UI バンドル（`CS2RuntimeAssetAuditor.mjs`、`CS2RuntimeAssetAuditor.css`、`cs2-runtime-asset-auditor-images/profiler-icon.svg`）が含まれます。公式 Toolchain の `Mod.props` / `Mod.targets` がローカル Mod への配置を管理します。

### 6. ゲームで有効化

ビルド後に Cities: Skylines II を起動し、使用するプレイセットで **CS2 Runtime Asset Auditor** を有効にしてください。ゲーム画面左上のランチャーアイコンからパネルを開ければ導入完了です。

アイコンが表示されない場合は、ローカル Mod フォルダー（`%USERPROFILE%\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CS2RuntimeAssetAuditor`）に `CS2RuntimeAssetAuditor.mjs` があるか確認してください。DLL だけがある場合は UI バンドルが配置されていないため、`npm ci` の後に Mod を再ビルドしてください。

設定の保存 ID は `CS2RuntimeAssetAuditor` です。旧 Runtime Profiler / Asset Performance Auditor の保存設定は新しい ID には自動移行されないため、必要な値を設定画面で再指定してください。

## 開発と検証

GitHub Actions はゲームなしで動く .NET 純粋テストと UI テスト・本番ビルドを実行します。Adapter テスト、Mod の Release ビルド、ゲーム内での動作確認にはゲームの管理 DLL と公式ツールチェーンが必要です。結果と未実施項目は [検証状況](docs/validation/2026-09-28-integration-validation.md)と [レビュー指摘の修正の検証状況](docs/validation/2026-09-29-review-fixes-validation.md)、[ランチャー非表示の修正とパネル開閉キーの検証状況](docs/validation/2026-09-29-launcher-and-keybinding-validation.md)に記録しています。

MIT License。
