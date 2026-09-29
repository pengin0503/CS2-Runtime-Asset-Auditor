# Guided Investigation Experiment 検証記録

実施日: 2026-09-29。対象: `docs/superpowers/specs/2026-09-29-investigation-experiment-design.md`。`PASS` は実際に実行した検証だけを示す。実ゲームの結果は推測しない。

## 自動検証

| 対象 | 結果 | 実行内容・範囲 |
| --- | --- | --- |
| Pure .NET tests（Investigation focused / Advisor・Runtime regression を含む） | PASS | .NET 8 SDK の C# コンパイラで純粋テストプロジェクトの271ソースをコンパイルし、NUnitLite で451/451件実行。通常の `dotnet test` / MSBuild はこの Work 環境の `/proc` とプロセス識別の不整合で `Process.GetStat` / `DebugUtils` に失敗するため、標準 VSTest 経路は NOT RUN。 |
| Adapter のゲーム非依存のソース契約テスト | PASS | net8 NUnitLite にリンクして3/3件。正確な手動 Capture の返却、Advisor の既存操作の利用、都市変更時の取り扱いを確認。 |
| 完全な Adapter tests | NOT RUN | CS2 managed DLL と .NET Framework の実行ホストがこの環境にない。ソース契約テストは代替の全 API 検証ではない。 |
| UI TypeScript type check | PASS | `npx tsc --noEmit -p .`、終了コード0。 |
| UI tests | PASS | `npm test`、30ファイル・112/112件。 |
| UI production build | PASS | `CS2_MOD_UI_OUTPUT_DIR` を一時出力先に設定した `npm run build`、webpack compiled successfully。ゲームへの配置は未検証。 |
| Mod Release build | NOT RUN | `CSII_TOOLPATH` / `CSII_MANAGEDPATH` / `CSII_USERDATAPATH` / `CSII_LOCALMODSPATH` が未設定で、公式 CS2 Modding Toolchain と managed DLL がこの環境にない。ゲーム側の型結合は未検証。 |

## 実ゲーム検証（仕様 §22）

この環境には Cities: Skylines II と公式 Modding Toolchain がない。以下はすべて `NOT RUN`。ゲームで実行した際に実測値、Capture ID、ログ、画面と結果を追記する。

| # | シナリオ | 結果 |
| --- | --- | --- |
| 1 | 実際の完了 Capture から実験を開始する | NOT RUN |
| 2 | 確認不要の設定を試す（利用可能な場合） | NOT RUN |
| 3 | 画質プリセットなど確認が必要な設定を試す | NOT RUN |
| 4 | 待機して明示的に Follow-up Capture を開始する | NOT RUN |
| 5 | 無関係の Automatic Capture を Follow-up に採用しない | NOT RUN |
| 6 | 結果値が意図した2件の Capture と一致する | NOT RUN |
| 7 | Keep で試した設定値が維持される | NOT RUN |
| 8 | 安全な場合に Undo で元の値に戻る | NOT RUN |
| 9 | Options で対象設定を変更すると無効化・競合が表示され、勝手に上書きしない | NOT RUN |
| 10 | 実験中の別提案の操作が単一設定の有効な結果を偽装しない | NOT RUN |
| 11 | 別の都市を読み込むと無効化され、都市をまたぐ比較や自動 Undo が起きない | NOT RUN |
| 12 | Export に実験結果が含まれ、予期しない個人情報がない | NOT RUN |
| 13 | 日本語・英語の UI が通常の対応 UI スケールで読め、切れない | NOT RUN |
| 14 | 実験なしで通常の Advisor 診断、Apply、Undo、比較が動く | NOT RUN |
