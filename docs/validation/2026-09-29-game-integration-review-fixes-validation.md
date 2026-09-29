# ゲーム連携レビュー指摘（#14〜#21）修正の検証状況 — 2026-09-29

CS2 1.6.2f1 の管理 DLL を使ったレビューで登録した issue #14〜#21 の修正について、実施した検証と未実施の項目を記録する。自動テストの成功を実機での PASS とは扱わない。

## 修正の要点

| Issue | 修正 |
| --- | --- |
| #14 | Deep Inspection は `RenderPrefab.ObtainMaterials` を使わない。Surface のプロパティ（読み込みと解放を対で行う）と共有テンプレートマテリアルから読み、Material の生成とテクスチャの読み込みを行わない。レポートに `basis`（`surfaceTemplateAndAssetKeywords`）を追加し、UI に説明を表示する。 |
| #15 | Census は `ToComponentDataListAsync` の未追跡ジョブをやめ、`ToComponentDataArray` でメインスレッドで同期的にコピーしてから、コピーを複数フレームで集計する。ジョブ完了待ちと遅延後始末の状態（`_censusCleanupRequested` など）を削除し、取り消し・失敗はその場で完了する。 |
| #16 | `pendingPathfindActions` を `m_Items.Count - m_NextIndex`（待機数）に直し、`inFlightPathfindActions`（`m_NextIndex`）を追加。`queueDeltaPerSecond` は待機数から計算する。 |
| #17 | `BuildingExtensionPrefab` を種別 `BuildingExtension`、`TreeObject` を持たない `PlantObject` の静的オブジェクトを種別 `Plant` として分類する。UI の種別フィルターに追加。 |
| #18 | Mod が自分で Surface のプロパティを読み込んだ場合、`isCurrentlyUsingVirtualTexturing` を `NotApplicable` とする。Surface の読み込みは `SurfaceAssetReader` の1か所に統合した。 |
| #19 | Prefab カタログ取得と Census 集計も、設定「フレーム予算」の時間まで小さな単位を繰り返す。設定の説明を適用範囲に合わせた。 |
| #20 | 全体の画質プリセット（`QualitySetting.GetLevel/SetLevel`）の専用アダプターを追加し、`rendering.ordered-quality` ルールに接続した。対象はプリセット間の変更だけで、「カスタム」からは提案しない。適用には確認が必要。提案の元のカタログをゲートウェイ経由に一本化した。 |
| #21 | リフレクションによるポーズ判定を削除し、`SimulationSystem.selectedSpeed == 0` と `GameManager.isGameLoading` を型付きで読む。 |

未使用だった拡張メソッド（`AssetAuditSurfaceTextureExtensions` など3ファイル）を削除した。

## 自動検証（この作業環境で実行）

| 対象 | 結果 | 根拠・制約 |
| --- | --- | --- |
| Mod の全ソースの実 API に対するコンパイル | PASS（エラー0件） | 管理 DLL と公式 `Mod.props` / `Mod.targets` によるビルド。PostProcessor とソースジェネレーターは代用。 |
| Adapter テスト | PASS（32件） | net8.0 でビルドして管理 DLL に対して実行。今回の修正が依存する API（`SurfaceAsset` のプロパティとテンプレート、`EntityQuery.ToComponentDataArray`、`PathfindQueueSystem.ActionList`、`StaticObjectPrefab` の派生一覧、画質レベル API、`selectedSpeed`）の契約テスト6件を追加。net48 のテストプロジェクトのビルドも成功。 |
| .NET 純粋テスト | PASS（429件） | 修正の構造と挙動（分類、画質プリセットの提案、経路探索の待機数など）のテストを追加・更新。 |
| UI テスト / 型チェック | PASS（102件）/ PASS | 種別フィルターの全選択肢に英語・日本語のラベルがあることのテストを追加。 |

## 実ゲームで追加確認が必要な項目

| # | シナリオ | 確認点 | 結果 |
| --- | --- | --- | --- |
| F1 | 同じ描画アセットで Deep Inspection を繰り返す | VRAM・メモリが増え続けない。マテリアル枠ごとにシェーダー名とキーワードが表示される | NOT RUN |
| F2 | 大規模な都市で Census を実行し、実行中に建物の設置・削除を行う | 例外やクラッシュがなく、完了までの時間がフレーム予算に応じて変わる | NOT RUN |
| F3 | 交通量の多い都市で経路探索タブを見る | `pendingPathfindActions` が詰まったときに増える | NOT RUN |
| F4 | 病院の病棟などサービス建物の拡張を含めて Asset Audit を実行する | 拡張が「建物の拡張」に分類され、小物との外れ値比較に現れない | NOT RUN |
| F5 | 画質プリセット「高」で GPU 負荷の高いキャプチャを診断し、提案を適用して Undo する | 「中」への変更が確認後に適用され、Undo で「高」に戻る。「カスタム」のときは提案されない | NOT RUN |
| F6 | ゲームをポーズした状態で待つ | 自動キャプチャが始まらない | NOT RUN |
