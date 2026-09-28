# 統合版の検証状況 — 2026-09-28

## 自動検証

| 対象 | 結果 | 根拠・制約 |
| --- | --- | --- |
| .NET 純粋テスト | PASS | GitHub Actions `Pure Core Tests`。ローカルには `dotnet` SDK がなく、GitHub Actions で実行。 |
| UI テスト | PASS | `npm test`。統合ナビゲーション、スキャン状態、一覧、関連付け等を含む。 |
| UI 本番ビルド | PASS | `npm run build`、webpack compiled successfully。 |
| Adapter テスト | NOT RUN | CS2 管理 DLL と公式 Modding Toolchain がこの実行環境にない。 |
| Mod Release ビルド | NOT RUN | `dotnet` SDK、CS2 管理 DLL、公式 `Mod.props` / `Mod.targets` がこの実行環境にない。ゲーム API との適合は未確認。 |

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
