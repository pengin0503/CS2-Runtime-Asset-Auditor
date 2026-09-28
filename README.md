# CS2 Runtime Profiler

CS2 Runtime Profiler は、Cities: Skylines II 向けの**読み取り主体の診断用MOD**です。ゲームプレイシステムを自動的に変更したり、MODを無効化したり、十分な根拠のない因果関係を断定したりすることなく、実行時のパフォーマンスに関する根拠を可視化することを目的としています。

プロジェクト概要: [GitHub Pages](https://pengin0503.github.io/CS2-Runtime-Profiler/)

## Performance Advisor（検証中）

既存の画面に追加した「Performance Advisor」タブから、完了したキャプチャを選んで手動で診断できます。診断結果の測定根拠と信頼度を確認し、適用可能な推奨を個別に **Apply** した後、再診断して前後の測定差を比較できます。変更直前の値を記録し、個別 **Undo** とセッション全体の Undo を提供します。通常の Options 画面などで値が変わった場合は競合として扱い、自動では上書きしません。

Advisor は自動で設定を変更しません。変更できるのは標準 Options のユーザー向け設定のうち、適用経路を検証できたものだけです。設定が表示されても安全な書き込み方法を確認できない場合は読み取り専用になります。現時点で GPU の直接計測から対象を特定した場合、Depth of Field の効果を抑える提案は実測の改善を保証しません。Headroom は「上げる余地あり」であり、変更後は再診断してください。複数の変更後に得た差分から個別設定の因果関係は断定しません。

JSON Export は診断、根拠、推奨、設定の利用可能状態、Advisor による変更、Undo の競合、比較結果を含みます。非公開のゲーム設定は出力しません。実ゲームでの設定反映・表示・Undo と Release ビルドは環境を整えて追加検証する必要があります。

## 計測内容

このプロファイラーは、低オーバーヘッドの常時計測と、時間を限定した **Deep Capture（詳細計測）** を組み合わせています。

- **Normal Monitoring（通常監視）**  
  監視が有効になっている間、ゲーム全体および各ドメイン単位のメトリクスを継続的に取得します。

- **Deep Capture（詳細計測）**  
  安全に利用できる、より広範なプロファイラーマーカーを一時的に有効化します。マーカーのカバレッジを記録するとともに、詳細計測の前・計測中・計測後の状況を保持します。

- **Systems（システム）**

  取得されたECSプロファイラーマーカーを、システムの完全修飾型名と一意に対応付けられる場合に、システムごとの処理時間を表示します。

  既定の並び順は **フレーム平均**（キャプチャ中の合計時間 ÷ 描画フレーム数）です。オートセーブ時の `SerializerSystem` のように1回だけ長く実行されたシステムは、平均/回・最大が数百msになってもフレーム平均は小さくなります。1回あたりの値（平均/回・P95・P99・最大）は加工せずそのまま表示します。

- **Mods（MOD）**

  各システムを直接所有しているアセンブリ／MODのメタデータに基づいてグループ化します。直接システム時間は各システムのフレーム平均の合計です（フレーム数が取得できないキャプチャではサンプル平均で代用し、その旨を表示します）。

  パッチ所有者は Harmony ID ではなく、パッチメソッドを定義しているアセンブリ名で表示・集計します。

  MODによってパッチされたバニラシステムについては、その処理コストをパッチしたMOD側へ移すことはせず、引き続きバニラシステムとして扱います。パッチを適用したMODは、所有者ではなくメタデータとして表示されます。

- **Timeline / Captures / Diagnostics（タイムライン／キャプチャ／診断）**  
  キャプチャ期間、選択されたメトリクス、計測カバレッジ、警告、バッチ処理、およびプロファイラー自身のオーバーヘッドに関する情報を表示します。タイムラインには2点以上の履歴がある系列だけを描画し、値は単位付き（ms、MiB など）で表示します。

### パネル操作

- 左上のアイコンで開閉します。開いている間は **Esc キー（ゲームパッドの B）** でも閉じられます。Esc は DOM のキー入力ではなくゲームの「戻る（Back）」入力アクションとして届くため、バニラのパネルと同じく `InputActionConsumer` で消費します（ポーズメニューが開かないことは実機検証マトリクス #14 で確認します）。
- ヘッダーのタイトル部分をドラッグすると移動、右下の角をドラッグするとサイズを変更できます。位置とサイズは保存され、ヘッダーの「位置をリセット」で初期状態に戻せます。
- 本文は縦スクロールバー付きです。システム一覧は100件ずつ表示します。

完了したキャプチャでは、取得された `TimeNanoseconds` のECSマーカーをミリ秒へ変換します。また、データが取得できる場合には、以下の統計情報を保持します。

- 現在値
- 平均値
- 中央値
- P95（95パーセンタイル）
- P99（99パーセンタイル）
- 最大値
- 合計値
- 呼び出し回数

## 信頼度と欠損データ

各タイミング行には、計測結果の**信頼度ラベル**が付与されます。

特に重要なのは、**「Unavailable（取得不可）」は「0」を意味しない**という点です。

- **Full（完全）**  
  対応するプロファイラーマーカーから直接計測された値です。

- **Managed（マネージド）**  
  実装されている場合に、安全なマネージドコードのみのフォールバック方式で計測された値です。BurstやJobによる処理が含まれない可能性があります。

- **Indirect（間接）**  
  より間接的な観測可能データから推定された値です。その点を考慮して解釈する必要があります。

- **Unavailable（取得不可）**  
  利用可能な根拠からは、その項目を適切に計測できない状態です。

このプロファイラーは、タイミングデータを無理に取得するためにJobの完了を強制することはありません。

安全かつ直接的に処理元へ帰属させることのできないBurst／Jobの処理については、特定のシステムへ帰属させず、または取得不可として扱います。

## キャプチャトリガーのデフォルト設定

現在の自動キャプチャトリガーでは、**シミュレーション効率（実際の速度 ÷ 選択された速度）**を使用します。

デフォルト値は以下のとおりです。

- 効率しきい値：**0.80**
- しきい値を下回ってからキャプチャを開始するまでの継続時間：**2秒**
- Deep Capture時間：**10秒**
- キャプチャ後のバッファ時間：**5秒**
- クールダウン：**30秒**

Monitoring（監視中）またはCooldown（クールダウン中）の状態では、手動でキャプチャを要求することもできます。

これらはあくまで**実装上のデフォルト値**であり、パフォーマンスを保証するものではありません。

## エクスポートとプライバシー

JSONレポートは、Cities: Skylines II のユーザーデータディレクトリ内にある以下の場所へ保存されます。

`ModsData/CS2RuntimeProfiler/CS2Profiler-report-YYYY-MM-DD_HHmmss_fff.json`

ファイル名にはミリ秒まで含まれます。同一ファイル名がすでに存在する場合は上書きせず、`-1`、`-2` のような連番サフィックスを付けて別ファイルとして保存します。

エクスポーターにはプライバシー保護用のサニタイザーが実装されており、検出された場合には以下の情報を置換するよう設計されています。

- Windowsのユーザーホームパス
- macOSのユーザーホームパス
- Linuxのユーザーホームパス
- 現在のアカウント名

ただし、これは**多層防御（Defense in Depth）の一環**として扱ってください。

エクスポートしたレポートを他者と共有する前に内容を確認してください。また、各リリース候補版（RC）では、ランタイム検証マトリクスに含まれる**エクスポート／プライバシーの検証シナリオ**を完了してください。

## 対象外の機能と制限事項

- このプロジェクトは**診断用プロファイラー**であり、自動最適化ツールやMOD自動無効化ツールではありません。

- MODを「良い」「悪い」と評価・順位付けすることはありません。また、単なる相関関係を因果関係として扱うこともありません。

- システムごとの処理時間は、取得したマーカーの根拠とシステムを保守的な方法で対応付けられる場合にのみ出力されます。対応関係が曖昧な場合に推測することはありません。

- マネージドシステムのタイミングが取得できたとしても、Burst／Jobによる処理まで完全に計測・帰属できていることを意味するものではありません。

- 一部のランタイム情報の検出処理は、現在のゲームおよびランタイムのバージョンに依存しています。そのため、ゲーム側のアップデートによって利用できなくなる可能性があります。

- 設計上定められているプロファイラーのオーバーヘッド上限は**検証目標値**であり、実際のゲームセッションで測定・確認されるまでは、達成済みの性能値として扱うことはできません。

## Windows / PowerShell でゲームへ導入する

以下は、リポジトリの `main` ブランチを取得し、UI と C# をビルドして Cities: Skylines II のローカル Mod フォルダへ導入するまでの手順です。

前提:

- Windows 11
- Cities: Skylines II がインストール済み
- Git が使用可能
- Node.js 18 以上 / npm が使用可能
- .NET SDK が使用可能
- **Cities: Skylines II の公式 Modding Toolchain をゲーム側で一度セットアップ済み**

公式 `Mod.props` / `Mod.targets` は複数の **User スコープ環境変数**を参照します。`CSII_MANAGEDPATH`、`CSII_USERDATAPATH`、`CSII_LOCALMODSPATH` だけでなく、Unity Mod Project、Post Processor、Entities Source Generator、mscorlib 等のパスも必要です。通常はこれらを手作業で構成せず、ゲーム内の公式 Modding Toolchain セットアップに生成・更新させてください。

### 1. PowerShell で公式 Toolchain の環境を確認する

新しい PowerShell を開き、まず公式Toolchainが設定した **User環境変数**を確認します。

```powershell
$toolVars = @(
    "CSII_TOOLPATH",
    "CSII_MANAGEDPATH",
    "CSII_USERDATAPATH",
    "CSII_LOCALMODSPATH",
    "CSII_UNITYMODPROJECTPATH",
    "CSII_MODPOSTPROCESSORPATH",
    "CSII_ENTITIESVERSION",
    "CSII_MSCORLIBPATH"
)

$toolVars | ForEach-Object {
    [PSCustomObject]@{
        Name  = $_
        Value = [Environment]::GetEnvironmentVariable($_, "User")
    }
} | Format-Table -AutoSize
```

特に次を確認してください。

```powershell
$toolPath   = [Environment]::GetEnvironmentVariable("CSII_TOOLPATH", "User")
$managed    = [Environment]::GetEnvironmentVariable("CSII_MANAGEDPATH", "User")
$userData   = [Environment]::GetEnvironmentVariable("CSII_USERDATAPATH", "User")
$localMods  = [Environment]::GetEnvironmentVariable("CSII_LOCALMODSPATH", "User")
$postProc   = [Environment]::GetEnvironmentVariable("CSII_MODPOSTPROCESSORPATH", "User")
$unityProj  = [Environment]::GetEnvironmentVariable("CSII_UNITYMODPROJECTPATH", "User")
$mscorlib   = [Environment]::GetEnvironmentVariable("CSII_MSCORLIBPATH", "User")
$entities   = [Environment]::GetEnvironmentVariable("CSII_ENTITIESVERSION", "User")

Test-Path (Join-Path $toolPath "Mod.props")
Test-Path (Join-Path $toolPath "Mod.targets")
Test-Path (Join-Path $managed "Game.dll")
Test-Path $userData
Test-Path $localMods
Test-Path $postProc
Test-Path $unityProj
Test-Path $mscorlib
$entities
```

`Mod.props` が読む変数名は **`CSII_MANAGEDPATH`** です。`CSII_MANAGED_PATH` ではありません。

上記の主要パスが空、または `False` の場合は、先に Cities: Skylines II 側の公式 Modding Toolchain セットアップを再実行してください。単に現在のPowerShellで `$env:CSII_...` を数個設定するだけでは不十分です。提供されている `Mod.props` は多くの値を `EnvironmentVariableTarget.User` から取得します。

### 2. リポジトリを取得する

初回のみ:

```powershell
cd "$HOME\Downloads"
git clone https://github.com/pengin0503/CS2-Runtime-Profiler.git
cd .\CS2-Runtime-Profiler
git switch main
```

すでに取得済みの場合:

```powershell
cd "$HOME\Downloads\CS2-Runtime-Profiler"
git switch main
git pull --ff-only origin main
```

現在のブランチ確認:

```powershell
git branch --show-current
```

`main` と表示されれば正しい状態です。

### 3. UI の依存関係を導入してビルドする

```powershell
cd .\UI
npm.cmd install
npm.cmd test
npm.cmd run build
cd ..
```

### 4. Pure Core Tests を実行する

```powershell
dotnet test .\tests\CS2RuntimeProfiler.Tests\CS2RuntimeProfiler.Tests.csproj -v minimal
```

テスト失敗がある場合は、ゲームへ導入する前にログを確認してください。

### 5. Release ビルドする

`CS2RuntimeProfiler.csproj` は `$(CSII_TOOLPATH)\Mod.props` と `Mod.targets` をImportします。公式Toolchain環境が正しく設定された状態で実行してください。

```powershell
dotnet build .\CS2RuntimeProfiler.sln -c Release
```

提供されている `Mod.targets` の `DeployWIP` は、成果物を **`CSII_LOCALMODSPATH\$(TargetName)`** へ配置します。このプロジェクトの `TargetName` は通常 `CS2RuntimeProfiler` です。

```powershell
$localMods = [Environment]::GetEnvironmentVariable("CSII_LOCALMODSPATH", "User")
$modDir = Join-Path $localMods "CS2RuntimeProfiler"
Get-ChildItem $modDir -Recurse
```

最低限、`CS2RuntimeProfiler.dll` と UI の `.mjs` / `.css` 成果物が存在することを確認してください。

```powershell
Get-ChildItem $modDir -Recurse -File |
    Where-Object { $_.Extension -in ".dll", ".mjs", ".css" } |
    Select-Object FullName, Length, LastWriteTime
```

フォルダをエクスプローラーで開く場合:

```powershell
explorer $modDir
```

### 6. Cities: Skylines II 側で有効化する

1. Cities: Skylines II を起動します。
2. Paradox Mods / Playsets でローカル Mod の **CS2 Runtime Profiler** を有効にします。
3. 対象の Playset を選択します。
4. ゲームを再起動して Mod を読み込みます。
5. ゲーム左上に Runtime Profiler のアイコンが表示されることを確認します。

### 更新後に再導入する場合

2回目以降は、公式Toolchain環境が既に有効なら次の流れで更新できます。

```powershell
cd "$HOME\Downloads\CS2-Runtime-Profiler"

git switch main
git pull --ff-only origin main

$toolPath  = [Environment]::GetEnvironmentVariable("CSII_TOOLPATH", "User")
$managed   = [Environment]::GetEnvironmentVariable("CSII_MANAGEDPATH", "User")
$localMods = [Environment]::GetEnvironmentVariable("CSII_LOCALMODSPATH", "User")

Test-Path (Join-Path $toolPath "Mod.props")
Test-Path (Join-Path $toolPath "Mod.targets")
Test-Path (Join-Path $managed "Game.dll")
Test-Path $localMods

cd .\UI
npm.cmd install
npm.cmd test
npm.cmd run build
cd ..

dotnet test .\tests\CS2RuntimeProfiler.Tests\CS2RuntimeProfiler.Tests.csproj -v minimal
dotnet build .\CS2RuntimeProfiler.sln -c Release

$modDir = Join-Path $localMods "CS2RuntimeProfiler"
Get-ChildItem $modDir -Recurse -File |
    Where-Object { $_.Extension -in ".dll", ".mjs", ".css" } |
    Select-Object FullName, Length, LastWriteTime
```

ビルド後にゲームが起動中だった場合は、一度 Cities: Skylines II を終了してから再起動してください。

## Build and test

Game/Unity/Colossal DLLs are not redistributed in this repository. A full mod build requires the user's local Cities: Skylines II managed assemblies and official modding toolchain/environment.

Pure core tests:

```powershell
dotnet test .\tests\CS2RuntimeProfiler.Tests\CS2RuntimeProfiler.Tests.csproj -v minimal
```

UI tests/build:

```powershell
cd UI
npm ci
npm test
npm run build
```

Full mod build on a correctly configured Windows CS2 modding environment:

```powershell
dotnet build .\src\CS2RuntimeProfiler\CS2RuntimeProfiler.csproj -c Release
```

## Validation status

Automated tests and static/runtime-independent checks are not substitutes for in-game validation. The release validation matrix and its current evidence are maintained in [docs/validation/runtime-validation.md](docs/validation/runtime-validation.md). Any scenario not actually run must remain marked **NOT RUN / UNVERIFIED**.
