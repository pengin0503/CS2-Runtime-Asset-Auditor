# ロード記録と調査実験のレビュー指摘（#22〜#28）修正の検証状況 — 2026-09-29

main @ 8418dfd のレビューで登録した issue #22〜#28 の修正について、実施した検証と未実施の項目を記録する。自動テストの成功を実機での PASS とは扱わない。

## 修正の要点

| Issue | 修正 |
| --- | --- |
| #22 | グラフィックスドライバ割当量は 0 以下を取得不可（null）として扱い、サンプル・ピーク・終了値に出力しない。`limitations` に「Unity は Development ビルドとエディターでのみ返し、製品版では 0 を返す」旨を追記した。Unity 割当メモリとワーキングセットは従来どおり 0 以上を有効値とする。 |
| #23 | `assetDatabaseObserved` を削除した。ロード開始時の最初の観測を基準に、登録数・キャッシュ状態が初めて変わった観測の時刻を `assetRegistrationChanged` / `assetCacheStateChanged` として記録する。 |
| #24 | `LoadingTraceRecorder` に結末（`InProgress` / `Completed` / `Interrupted`）を持たせた。ロード中に次のロードが始まった場合、またはゲームプレイ以外としてロードが完了した場合は `Clear()` せず `Interrupt()` で終える。エクスポートはゲームプレイ中にしかできない（`ProfilerUISystem.gameMode` が `Game`）ため、中断した記録は直後のゲームプレイのロードの記録に `previousInterruptedLoad` として含める。ロード開始・セーブ読込後に経過時間とその時点までのピークを、完了・中断時に所要時間・ピーク・中断理由の要約を MOD ログへ 1 行ずつ出力する。使われていなかった `Complete(cityOperable: false)` の経路は削除した。 |
| #25 | 指標ごとにピーク値に最初に達した観測時刻を保持し、`memoryPeak` に `unityAllocatedAtUtc` などとして出力する。間引きで時系列から落ちた観測でも時刻は残る。 |
| #26 | 常に null だった `loadedBytes`、`cacheRebuilt`、`cacheFailed`、`vramUsedBytes` を削除した。取得できない旨は従来どおり `limitations` にある。 |
| #27 | 下記の単体テストと Adapter 契約テストを追加し、この検証記録を作成した。 |
| #28 | `CaptureSession` に中断理由（`CaptureInterruptionReason`）を持たせ、`DeepCaptureController.InterruptActiveCapture` で設定する。追跡キャプチャが安全上限・監視無効化で中断された場合は比較を出しつつ `FollowUpInterruption` として状態を保持し、UI に理由付きの注記を表示、エクスポートに `followUpInterruption` を出す。都市セッションの変更で中断された追跡キャプチャと、証拠を作れない中断キャプチャは `FollowUpCaptureInterrupted` で無効化する。Runtime キャプチャのエクスポートにも `interruptionReason` を追加した。 |

`loading` のスキーマは変わったが、この機能は未リリース（タグ・リリースなし）のため `schemaVersion` は 1 のままとした。

## 自動検証（この作業環境で実行）

参照 DLL はプロジェクトファイルの `CS2-managed-reference-1.6.2f1.zip`（Game、Colossal.*、Unity.*、UnityEngine.CoreModule、UnityEngine.PerformanceReportingModule、`Mod.props` / `Mod.targets`）を使った。csproj が参照する DLL は全て含まれており、不足はなかった。

| 対象 | 結果 | 根拠・制約 |
| --- | --- | --- |
| Mod の全ソースの実 API に対するコンパイル | PASS（エラー0件） | Linux の .NET 8 SDK で、`src/CS2RuntimeAssetAuditor` の全ソースを参照 DLL・Lib.Harmony 2.2.2・.NET Framework 4.8 参照アセンブリに対してコンパイルする一時プロジェクトを使用。公式 Toolchain の `Mod.props` / `Mod.targets`、ゲームの mscorlib、PostProcessor、Entities ソースジェネレーターは使っていない。変更前後で新たに出た警告は nullable 注釈（CS8618/CS8601）だけで、既存コードと同じ書き方による。 |
| Adapter テスト | PASS（40件） | net8.0 でビルドし参照 DLL に対して実行。ロード記録が使う API の契約テスト4件（`Profiler.GetTotalAllocatedMemoryLong` / `GetAllocatedMemoryForGraphicsDriver` が `Int64`、`AssetDatabase.global` の `count`（`Int32`）と `isCached`（`Boolean`）、`OnGameLoaded(Context)` と `Context.purpose` / `Purpose.LoadGame`、`GameMode` の値）を追加。期待値を故意に変えると失敗することも確認した。net48 のテストプロジェクトのビルドも成功。 |
| .NET 純粋テスト | PASS（474件） | ロード記録: 境界とマイルストーン、ドライバ割当量 0/負値の null 化、DB 変化マイルストーン、指標別ピーク時刻（同値は最初の時刻）、間引き後の件数（3000 観測で 375 点）と等間隔、完了後の `Mark`・`Observe`・`ShouldSample`・`Complete`・`Interrupt` の無視、中断の保持とエクスポート、進行中のエクスポート、直前の中断記録の引き継ぎ、`FromSnapshot(null)`、削除したフィールドが出力されないこと、ログ行の書式、ライフサイクルが中断を `Clear()` しないこと（ソース契約）。調査実験: キャプチャの中断理由の記録と UI 射影、中断した追跡キャプチャの比較と理由の保持、都市変更による中断の無効化、エクスポートの `followUpInterruption` / `interruptionReason`。 |
| UI テスト / 型チェック / 本番ビルド | PASS（113件）/ PASS / PASS | 中断した追跡キャプチャで比較と注記が両方表示され、中断なしでは注記が出ないことのテストと、注記文言の英日両方の存在確認を追加。本番ビルドは一時出力先で実行。 |
| Mod の Release ビルド（公式 Toolchain） | NOT RUN | PostProcessor・Unity Mod プロジェクト・ゲームの mscorlib がこの環境にない。 |

## 実ゲームで追加確認が必要な項目（1.6.2f1）

この環境ではゲームを起動できないため、次は全て未実施。各値が取得できるかとサンプル間隔の実測はここで確認する。

| # | シナリオ | 確認点 | 結果 |
| --- | --- | --- | --- |
| L1 | セーブを読み込み、読込完了後に JSON をエクスポートする | `outcome` が `completed`。`unityAllocatedBytes`・`processWorkingSetBytes`・`registeredAssetCount`・`anyDatabaseCached` が値を持つ。`graphicsDriverAllocatedBytes` が出力されない（製品版で 0 が返るという前提の確認。値が出る場合は前提が誤り） | NOT RUN |
| L2 | L1 の `memorySamples` の `atUtc` の差を集計する | 観測間隔がおおむね 2 秒で、ロード中にフレーム更新が止まる区間の長さが分かる | NOT RUN |
| L3 | L1 の `milestones` を見る | `assetRegistrationChanged` / `assetCacheStateChanged` がロード中に記録されるか（記録されない場合、その変化はロード中の観測では起きないことを示す） | NOT RUN |
| L4 | L1 の後に MOD ログ（ロガー名 `CS2RuntimeAssetAuditor.Mod`）を見る | `Loading trace: milestone=loadStarted`、`milestone=saveRestored`、`Loading trace finished: outcome=Completed` の 3 行があり、ピークと時刻がエクスポートと一致する | NOT RUN |
| L5 | 新規都市を開始する | `gameLoaded` が記録され `outcome` が `completed` | NOT RUN |
| L6 | 読み込みに失敗するセーブ（または読み込み中にタイトルへ戻れる状況）を用意し、続けて別の都市を読み込んでエクスポートする | `previousInterruptedLoad` に `outcome: interrupted` と `interruptionReason` があり、ログに `outcome=Interrupted` の行がある。失敗時にメニューへ戻る流れ自体が実機未確認 | NOT RUN |
| E1 | 調査実験の追跡キャプチャ中に監視を無効化する | 比較が表示され、「監視が無効化されたため…途中で終了しました」の注記が出る。エクスポートに `followUpInterruption: MonitoringDisabled` | NOT RUN |
| E2 | 調査実験の追跡キャプチャ中に都市を切り替える | 実験が無効（都市セッション変更）になり比較は出ない | NOT RUN |
