# 統合版の検証状況 — 2026-09-28

## 計画の進捗確認（実装照合基準: `6d7fbd6b4e66c4aa287596e5cfbc0c179732aa7d`）

実装計画の Task 1〜10 と Task 11 の CI・README・検証記録は、順番に対応するコミットを確認済み。計画全体の検証完了とは扱わない。残る Task 11 の条件付き実行は Adapter テスト、統合 Mod の Release ビルド、実ゲーム 15 シナリオである。

- 純粋テスト: [Pure Core Tests 実行結果](https://github.com/pengin0503/CS2-Runtime-Asset-Auditor/actions/runs/36497888177) — 上記コミットで success。
- UI テスト・本番ビルド: [UI Tests 実行結果](https://github.com/pengin0503/CS2-Runtime-Asset-Auditor/actions/runs/36497888262) — 上記コミットで success。移植元の操作・設定・エクスポート・Deep Inspection と、バインド済みスナップショットの参照安定性を統合 UI 上の回帰テストで確認。
- ゲーム依存の検証を実行するには、起動可能な .NET SDK、対応する CS2 管理 DLL と公式 Modding Toolchain 一式、実ゲーム環境が必要。現在の作業環境ではこの組み合わせが揃わない。参照 DLL の断片が見つかっても、完全なツールチェーンや実機検証の代わりにはしない。

## 自動検証

| 対象 | 結果 | 根拠・制約 |
| --- | --- | --- |
| .NET 純粋テスト | PASS | GitHub Actions `Pure Core Tests`。この作業環境の `dotnet` コマンドは利用不可。GitHub Actions で実行。 |
| UI テスト | PASS | `npm test`。統合ナビゲーション、スキャン状態、一覧、関連付け等を含む。 |
| UI 本番ビルド | PASS | `npm run build`、webpack compiled successfully。 |
| Adapter テスト | NOT RUN | 対応する CS2 管理 DLL と公式 Modding Toolchain 一式が揃わず、この環境からは実行できない。 |
| Mod Release ビルド | NOT RUN | 起動可能な `dotnet` SDK、対応する CS2 管理 DLL、公式 Modding Toolchain 一式が揃わない。ゲーム API との適合は未確認。 |

## 実ゲームでの確認

この環境では Cities: Skylines II を起動できないため、下記の実ゲーム項目はいずれも **NOT RUN**。自動テスト成功を実機での PASS と扱わない。

| # | シナリオ | 結果 |
| --- | --- | --- |
| 1 | 統合 Mod のみ有効にしてゲームを起動 | NOT RUN |
| 2 | ランチャーとパネルが一つずつ、Esc/B で閉じる | NOT RUN |
| 3 | 通常監視中のオーバーヘッド確認 | NOT RUN |
| 4 | 手動 Deep Capture | NOT RUN |
| 5 | 再現可能な自動キャプチャトリガー | NOT RUN |
| 6 | 手動 Asset カタログ・Census | NOT RUN |
| 7 | Deep Capture 中の Asset スキャン要求が待機 | NOT RUN |
| 8 | Asset スキャン中の Deep Capture が安全に中断 | NOT RUN |
| 9 | Runtime キャプチャから Asset へ移動・文脈を表示 | NOT RUN |
| 10 | Asset 詳細から Runtime へ戻り、架空の個別コストを表示しない | NOT RUN |
| 11 | Advisor 診断・Apply・Undo・競合 | NOT RUN |
| 12 | 統合 JSON と Asset CSV の保存・内容 | NOT RUN |
| 13 | エクスポートのパス・ユーザー名のプライバシー確認 | NOT RUN |
| 14 | 再起動後の新しい設定 ID での保持 | NOT RUN |
| 15 | Mod 無効化・アンインストール後の UI 重複なし | NOT RUN |
