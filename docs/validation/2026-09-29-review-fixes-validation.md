# レビュー指摘（#1〜#13）修正の検証状況 — 2026-09-29

コードレビューで登録した issue #1〜#13 の修正について、実施した検証と未実施の項目を記録する。自動テストの成功を実機での PASS とは扱わない。

## 自動検証（この作業環境で実行）

| 対象 | 結果 | 根拠・制約 |
| --- | --- | --- |
| .NET 純粋テスト | PASS（402件） | `dotnet test tests/CS2RuntimeAssetAuditor.Tests -c Release`（.NET 8 SDK、Linux）。 |
| UI 型チェック | PASS | `npx tsc --noEmit -p UI`。 |
| UI テスト | PASS（98件） | `npm test`。英語・日本語の表示、Advisor の結果表示と確認付きのセッション Undo、所見のページ表示などを含む。 |
| UI 本番ビルド | PASS | `npm run build`、webpack compiled successfully。 |
| Adapter テストのビルド | PASS | `dotnet build tests/CS2RuntimeAssetAuditor.AdapterTests`。このプロジェクトは修正前、参照しているソースをコンパイル対象に含めておらずビルドできなかった。 |
| Adapter テストの実行 | NOT RUN | CS2 の管理 DLL がない。ゲームの読み込みフック（`OnGamePreload` / `OnGameLoadingComplete`）の契約テストを追加済みで、DLL のある環境で実行が必要。 |
| Mod のビルド（スタブによる型検査） | PASS（エラー0件） | ゲーム・Unity・Colossal の API を `dynamic` のスタブで置き換え、Mod の全ソースをコンパイルして自前コードの型・メンバー参照を検査した。修正前のソースでは `ProfilerUISystem` の `PrivacySanitizer` 曖昧参照（CS0104）を検出し、#3 の修正で解消した。スタブの形は実際の API と異なり得るため、Release ビルドの代わりにはならない。 |
| Mod Release ビルド | NOT RUN | 公式 Modding Toolchain と CS2 管理 DLL がない。 |

## 実ゲームで追加確認が必要な項目

既存の 15 シナリオ（[統合版の検証状況](2026-09-28-integration-validation.md)）に加え、今回の変更について次を確認する。

| # | シナリオ | 確認点 | 結果 |
| --- | --- | --- | --- |
| R1 | 都市 A で Census・Asset Audit・Deep Capture を行い、別の都市 B を読み込む | キャプチャ一覧・Census・分析結果が空になり、エクスポートの `session.sessionId` が A と B で異なる | NOT RUN |
| R2 | メインメニューで手動キャプチャ・スキャンを要求する | 実行されない | NOT RUN |
| R3 | Asset Audit を完了させてエクスポートする | `assets.startedAtUtc` と `completedAtUtc` が監査の開始と公開の時刻で、Deep Inspection 後は `enrichedAtUtc` だけが増える | NOT RUN |
| R4 | 計測負荷またはメモリの安全上限でキャプチャを停止させる | クールダウンに入り、連続した停止で停止時間が延びる警告が出る | NOT RUN |
| R5 | ゲームの言語を日本語と英語で切り替える | パネルの表示言語が追従し、エクスポートの文言は英語のまま | NOT RUN |
| R6 | 確認が必要な標準設定を Advisor で適用し、「セッションの変更を元に戻す」を実行する | 確認後に元の値へ戻り、結果が表示される | NOT RUN |
| R7 | 外れ値分析を有効にして Asset Audit を実行する | `APA-PEER-001` / `APA-PEER-002` の所見に比較対象の件数・中央値・P95 が記録される | NOT RUN |
| R8 | Deep Capture のシステム一覧で `Managed` 行を確認する | 他のシステムを入れ子で更新するシステム（シミュレーション系など）が、子の時間を二重に含まない | NOT RUN |
| R9 | パネルを閉じたまま Asset Audit を実行する | 完了後にパネルを開くと結果が表示され、閉じている間のフレーム時間に Asset UI の更新処理が現れない | NOT RUN |
