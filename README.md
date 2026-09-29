# CS2 Runtime Asset Auditor

Cities: Skylines II の実行時パフォーマンスとアセットの静的な構造・都市内での配置状況を、ひとつの Code Mod で調査します。ゲーム画面左上のランチャーからパネルを開きます。概要、ランタイム、アセット、改善提案、診断を同じ画面で切り替えられます。

## 使い方

- **ランタイム**: 通常監視の指標を確認し、必要に応じて手動または自動トリガーの Deep Capture を実行します。システム、MOD、経路探索、タイムライン、完了したキャプチャを表示します。キャプチャを選んで「アセットを調査」へ移動できます。
- **アセット**: 手動で Census または Asset Audit を実行します。Prefab 一覧の検索・絞り込み・ページ分割、Census の配置数、形状・LOD・マテリアル・テクスチャの所見、比較、設定を利用できます。アセット詳細からランタイムキャプチャに戻れます。
- **改善提案**: キャプチャを選んで診断し、根拠と信頼度を確認します。適用可能な標準設定は個別に Apply/Undo できます。GPU 負荷が高いときの提案対象は、全体の画質プリセット（1段階ずつ）と被写界深度です。画質プリセットの変更は詳細なグラフィック設定をすべて上書きするため確認が必要で、現在の画質が「カスタム」の場合は提案しません。操作の結果（拒否された理由を含む）は画面に表示します。「セッションの変更を元に戻す」は確認後に、確認が必要な設定も含めて元に戻します。設定が別の場所で変わった場合は競合を表示します。自動適用は行いません。
- **診断**: 計測の利用可能性、カバレッジ、警告を確認します。

パネルはドラッグで移動、右下の角でサイズを変更できます。Esc またはゲームパッドの B で閉じます。位置・サイズは保存され、ヘッダーからリセットできます。

改善提案の「変更を試す」では、診断済みのキャプチャを基準に一度に一つの設定を調べます。提案を選び、必要な確認を含めて変更を明示的に適用し、少なくとも5秒待ってからフォローアップのキャプチャを自分で開始してください。キャプチャ後に指標の前後差を確認し、「変更を維持」または「試した変更を元に戻す」を選びます。追跡キャプチャが計測の安全上限や監視の無効化で途中終了した場合も比較は表示しますが、途中終了したことと理由を表示し、比較は終了までに計測できた範囲だけに基づきます（エクスポートでは `followUpInterruption`）。都市の切り替えで途中終了した追跡キャプチャは比較しません。変更後に改善・悪化が観測されても、都市のシミュレーションも変化し得るため、設定変更がその差を引き起こしたとは断定できません。実験をキャンセルした場合、適用済みの設定は自動で元に戻りません。

オプションの「キー割り当て」→「診断パネルの開閉」で、パネルを開閉するキーを設定できます。初期状態ではキーを割り当てていません（他の MOD やゲームのキーと衝突させないため）。キーはプレイ中の都市で有効で、テキスト入力欄にフォーカスがある間は反応しません。割り当ての解除・初期化はゲーム標準のキー設定欄から行えます。左上のランチャーアイコンはキー設定に関係なく使えます。

表示言語はゲームの言語設定に従います。日本語（`ja-*`）では日本語、それ以外の言語では英語で表示します。エクスポートするレポートの文言は英語で統一しています。

## 根拠の読み方

Runtime の `Full`、`Managed`、`Indirect`、`Unavailable` は計測の信頼度です。`Managed` は管理コードの同期実行の自己時間で、入れ子で更新される別の管理システムの時間と Job/Burst の実行時間を含みません。Asset の `Available`、`NotScanned`、`Unsupported`、`Failed` などは収集状態です。`NotScanned` や `Unavailable` はゼロを意味しません。

改善提案のボトルネック判定は、MOD 自身が計測したフレーム時間（`Time.unscaledDeltaTime`。製品版のゲームには `Frame Time` マーカーがないため）と `FrameTimingManager` の値を使います。フレーム時間が長いとき、メインスレッドが Present（GPU の表示完了）を待っていれば「描画/GPU」、待たずにほぼフレーム全体で処理していれば「CPU/メインスレッド」と判定します。D3D11 の GPU 時間は CPU を待つ時間も含むため、GPU 時間だけでは描画が原因とは判定しません（この場合は確信度を下げます）。シミュレーション効率の低下は次の順に原因を区別します。ゲームは描画 1 フレームで最大 `max(1, min(8, round(速度×2)))` ステップしか進めないため、30 fps を下回ると 1 倍・2 倍・4 倍速のどれでも速度が落ちます。効率がこの上限に近い、または大半のフレームが上限までステップしていれば「フレームレートによるシミュレーション制限」です（フレーム時間を下げる必要があり、シミュレーション負荷の問題ではありません）。次に、経路探索の先行分が 48 フレームを下回ってゲームが減速したフレームが多ければ「経路探索」、それ以外を「シミュレーション/CPU」とします。一般設定の性能優先が SimulationSpeed 以外の場合は、その設定もステップを制限するため確信度を下げます。

Asset の種別は、建物・サービス建物・建物の拡張（サービス建物のアップグレードを含む）・小物・樹木・植物（樹木以外の植栽）・車両・ネットワークです。Deep Inspection のマテリアル情報は、マテリアル枠ごとのテンプレートシェーダーとアセットのキーワードで、ゲームが描画に使うマテリアルそのものではありません。Deep Inspection はマテリアルやテクスチャを新たに読み込みません。

Asset の頂点数、テクスチャ情報、配置数は調査の手がかりであり、アセットごとのフレーム時間や GPU 負荷の実測値ではありません。同種アセット外れ値分析は、同じ Prefab 種別の比較対象（5件以上）と比べて LOD0 頂点数や推定テクスチャ容量が極端に大きいものを注記として示します。

都市セッションは、ゲームプレイの都市の読み込み完了から次の読み込み開始までです。別の都市を読み込むと、それまでのキャプチャ・Census・分析結果は破棄され、メインメニューやエディターではキャプチャやスキャンを行いません。同じ都市セッション内で開始・終了時刻が揃った Runtime キャプチャと Asset スナップショットだけを、前後・重複の時系列として関連付けます。Asset スナップショットの区間は Asset Audit の開始から公開までで、Deep Inspection による追加は区間を変えません。関連付けは因果関係を示しません。

Deep Capture は重い Asset スキャンより優先されます。キャプチャ中（後処理の区間を含む）に要求されたスキャンは待機し、進行中にキャプチャが始まれば安全な境界で中断します。自動キャプチャは、シミュレーション効率がしきい値を下回った状態が続いたときに始まります。ただし、その低下がフレームレートで説明できる場合は始めません（下の「フレームレートによるシミュレーション制限」と同じ判定です）。このときのキャプチャは通常監視で分かるフレーム時間を測るだけで、計測の負荷を増やすためです。スキップした場合は MOD ログに `Automatic capture skipped` の行を残します。手動キャプチャはこの判定に関係なく実行できます。プロファイラーのメモリ増加による負荷の削減と停止は、連続する 2 回のサンプルで増加が続いた場合だけ行います。ゲームの自動保存は 1 フレームだけプロファイラーのメモリを約 800 MiB 増やすため、これをキャプチャのメモリ増加と誤って扱わないようにしています。計測負荷やメモリの安全上限でキャプチャを停止した場合は、クールダウンに入り、停止が続くほど自動キャプチャの再開を遅らせます（最大10分）。中断した途中結果は完了スナップショットとして公開されません。再実行は手動で行ってください。Deep Capture の開始によって全 Asset Census を自動実行することはありません。

## ロード記録

都市の読み込み時には Deep Capture と Asset Audit を起動せず、別の軽量な記録を残します。MOD の OnLoad、ロード開始、セーブ読込後のコールバック、ゲームのロード完了コールバックを UTC 時刻で JSON の `loading` に保存します。新規都市ではセーブ復元の代わりに `gameLoaded` と記録します。ロード完了コールバックは操作可能時点の近似で、全ての UI・シミュレーション処理の完了を保証しません。

ロード中は Unity のフレーム更新が動く範囲で 2 秒ごとに Unity 割当メモリ、プロセスの RAM ワーキングセット、グラフィックスドライバ割当量、AssetDatabase 登録数、いずれかのデータベースがキャッシュ済みかを観測します。ロード開始時の最初の観測を基準とし、登録数またはキャッシュ状態が初めて変わった観測の時刻を `assetRegistrationChanged` / `assetCacheStateChanged` として記録します（観測間隔の分だけ遅れ、変化がなければ記録しません）。時系列は最大 512 点に間引いて保持し、ピーク（指標ごとの値と、その値に最初に達した観測の時刻）と終了時の値は間引き前の観測も含めます。

グラフィックスドライバ割当量は Unity が Development ビルドとエディターでのみ返す値で、製品版のゲームでは 0 が返るため、0 以下は取得不可として出力しません。実 VRAM 使用量、実際に読み込んだバイト数、キャッシュ再構築・失敗は取得できないため値を推定せず、`limitations` に記します。ドライバ割当量を VRAM 使用量として扱わないでください。ロード中にフレーム更新が停止する区間と 2 秒未満のメモリ急増は捕捉できません。

`outcome` はロードの結末を `completed`（ゲームがロード完了を通知）、`interrupted`（完了の通知前に次のロードが始まった、またはゲームプレイ以外としてロードが完了した。`interruptionReason` に次のロードの種類を記録）、`inProgress` のいずれかで示します。中断したロードの記録は破棄しません。エクスポートはゲームプレイ中にしかできないため、中断したロードの記録は直後のゲームプレイのロードの記録に `previousInterruptedLoad` として含めます（保持するのは直前の 1 件だけです）。

記録はメモリ上にあり、JSON エクスポートでのみ保存されます。ロード中にゲームが終了しても追えるように、ロード開始とセーブ読込後の時点で経過時間とその時点までのピークを、完了・中断時に所要時間とピークの要約を、それぞれ MOD ログへ 1 行ずつ出力します。最後の行より後のピークはログに残りません。

## レポート

パネルの JSON エクスポートは Runtime、Advisor、Asset、能力状態、診断、同一セッションの時系列リンクを含む単一のレポートを保存します。Asset 側からは絞り込み対象の Asset CSV も保存できます。ユーザーデータディレクトリ以下の保存先は次のとおりです。

- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-report-YYYY-MM-DD_HHmmss_fff.json`
- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-assets-YYYY-MM-DD_HHmmss_fff.csv`

同名ファイルがあれば上書きせず `-1`、`-2` の連番を付けます。絶対パスは `[redacted-path]`、アカウント名とマシン名は独立した単語として現れる場合だけ `[redacted]` に置換します（3文字未満の名前は置換しません）。共有前には内容を確認してください。CSV のテキストセルは表計算ソフトで数式として解釈されないよう保護します。

## 診断ログ

実機のプレイを後から分析するための連続記録です。JSON レポート（操作したときのスナップショット。時系列はキャプチャ 1 回分）や MOD ログ（人が読むイベントの行）とは別に、オプションの「監視」→「診断ログを記録」を有効にしている間、都市を開いているあいだ 1 秒ごとに 1 行を CSV に書きます。初期状態は無効です。

- `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-diagnostic-YYYY-MM-DD_HHmmss_fff.csv`: 時系列。都市セッションごとに 1 ファイルで、最大 32 MiB（1 行は数百バイトなので長時間でも上限に達しにくい量です）。上限に達するとその都市では以降の行を書きません。
- `…-markers.csv`: 記録開始時にこのゲームで利用できるプロファイラーマーカーの一覧（カテゴリ、名前、単位、データ型、通常監視の対象か）。

各行は 1 秒間の集計です。数値は不変カルチャで書き、取得できなかった値は 0 ではなく空欄にします。

| 列 | 内容 |
| --- | --- |
| `frameMs*` | `Time.unscaledDeltaTime` による描画フレーム時間の中央値・P95・最大 |
| `cpuMainMs*` / `cpuRenderMs*` / `gpuMs*` / `presentWaitMs*` | `FrameTimingManager` のメインスレッド・レンダースレッド・GPU・Present 待ち時間。数フレーム遅れた値で、機能が無効なら空欄（開始時の MOD ログに `frameTimingFeature` を出力） |
| `selectedSpeed` / `actualSpeedMean` / `efficiencyMean` | 選択速度（区間の最後）、`smoothSpeed` と効率の区間平均（一時停止中のフレームは除外、`pausedFrames` に数） |
| `simSteps` / `simStepsPerSecond` / `simMaxStepsPerFrame` | `frameIndex` の増分から数えた実際のシミュレーションステップ数（60/秒が 1 倍速） |
| `simFramesAtRenderCap` | 描画 1 フレームあたりの上限（`max(1, min(8, round(選択速度×2)))`）までステップしたフレーム数。多いほど描画フレーム数が速度を制限している |
| `simFramesWithoutStep` | 実行中なのに 1 ステップも進まなかったフレーム数 |
| `simStepMs*` | `SimulationSystem.frameDuration`（ジョブ完了までを含む 1 ステップの実時間） |
| `performancePreference` | 一般設定の性能優先（FrameRate / Balanced / SimulationSpeed） |
| `pathfindLeadFramesMin` / `pathfindLowLeadFrames` / `pathfindPendingRequestsMax` | 経路探索結果の期限までの残りフレーム（このフレームのステップ後）の最小値、48 未満だったフレーム数、結果待ちの要求数の最大値 |
| `gcCollections` / `managedHeapMiB` | 前の行からの GC 回数、管理ヒープ量 |
| `captureState` / `captureTrigger` / `captureId` | Deep Capture の状態。`captureId` で JSON レポートのキャプチャと対応付けられます |
| `frameRateEfficiencyCeiling` | 区間のフレームレートと選択速度で到達できる効率の上限（`min(1, 上限ステップ数×fps÷(60×速度))`）。1 未満ならフレームレートが速度を制限している |
| `modUpdateMsMedian` / `modUpdateMsMax` | この MOD のシステムが 1 フレームで使った更新時間の合計の中央値・最大。長いフレームのうち MOD の処理が占めた分を見分けるための値です |
| `modUpdateSlowestSystem` / `modUpdateSlowestSystemMs` | MOD の更新時間が最も長かったフレームで、最も時間を使ったシステムとその時間 |
| `autoSaveStarts` | この区間に始まったゲームの自動保存の回数（`AutoSaveSystem` の内部状態から検出。読めなければ空欄）。自動保存のフレームは 1〜1.5 秒止まり、プロファイラーのメモリが一時的に増えます |
| `recorder:<名前> [単位]` | 通常監視で有効にしているプロファイラーレコーダーの区間内の最後の値（単位はマーカーの単位のまま） |

記録の開始・停止（理由と行数）、失敗、Deep Capture の開始（トリガーと効率）、ゲームの自動保存の開始は MOD ログに出力します。MOD ログの行は、ゲームのログ（`Logs/CS2RuntimeAssetAuditor.Mod.log`）に加えて `ModsData/CS2RuntimeAssetAuditor/CS2RuntimeAssetAuditor-events.log` にも書きます。実機の環境で、ゲームのログに OnLoad 以外の行が残らないことがあったためです。このファイルは 4 MiB に達すると `.old` に移し、2 ファイルまで保持します。ゲームのロガーの設定レベルも起動・都市の読み込み・終了の行に記録します。停止（ヒッチ）の原因を追うため、診断ログの有効・無効にかかわらず、次の行も MOD ログに出力します。

- `Slow mod update`: MOD のシステムの 1 回の更新が 100 ms 以上かかったとき（同じシステムは 10 秒に 1 行まで。省略した回数を `suppressedSinceLastReport` に記録）
- `Capture completion timing`: キャプチャ完了処理の段階ごとの所要時間
- `Report export timing` / `Asset CSV export timing`: エクスポートの作成・書き出しの所要時間。エクスポートはメインスレッドで実行するため、この時間だけゲームが止まります

ログにはファイル名だけを書き、絶対パスは書きません。

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

GitHub Actions はゲームなしで動く .NET 純粋テストと UI テスト・本番ビルドを実行します。Adapter テスト、Mod の Release ビルド、ゲーム内での動作確認にはゲームの管理 DLL と公式ツールチェーンが必要です。結果と未実施項目は [検証状況](docs/validation/2026-09-28-integration-validation.md)と [レビュー指摘の修正の検証状況](docs/validation/2026-09-29-review-fixes-validation.md)、[ランチャー非表示の修正とパネル開閉キーの検証状況](docs/validation/2026-09-29-launcher-and-keybinding-validation.md)、[ゲーム連携レビュー指摘の修正の検証状況](docs/validation/2026-09-29-game-integration-review-fixes-validation.md)、[ロード記録と調査実験のレビュー指摘の修正の検証状況](docs/validation/2026-09-29-loading-trace-review-fixes-validation.md)に記録しています。

MIT License。
