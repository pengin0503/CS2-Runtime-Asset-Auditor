import { translate, type Locale } from "../i18n/locale";

// The backend sends canonical English text for dynamic reasons, warnings and diagnostics. English UIs show it
// unchanged; Japanese UIs translate the known sentences and fall back to the original for anything unknown.

export function captureStateLabel(state: string, isDeepCapture: boolean, locale: Locale): string {
  if (isDeepCapture || state === "DeepCapture") return translate(locale, "state.deepCapture");
  switch (state) {
    case "Monitoring": return translate(locale, "state.monitoring");
    case "PostBuffer": return translate(locale, "state.postBuffer");
    case "Cooldown": return translate(locale, "state.cooldown");
    default: return state || translate(locale, "state.unknown");
  }
}

export function confidenceLabel(value: string, locale: Locale): string {
  switch (value) {
    case "Full": return translate(locale, "confidence.full");
    case "Managed": return translate(locale, "confidence.managed");
    case "Indirect": return translate(locale, "confidence.indirect");
    case "Unavailable": return translate(locale, "confidence.unavailable");
    default: return value || translate(locale, "confidence.unavailable");
  }
}

export function triggerKindLabel(value: string, locale: Locale): string {
  switch (value) {
    case "Manual": return translate(locale, "trigger.manual");
    case "Automatic":
    case "AutomaticLowEfficiency": return translate(locale, "trigger.automatic");
    default: return value || translate(locale, "trigger.unknown");
  }
}

/** Formats the shared export protocol: "ok:<file>" or "error:<code>: <message>" / "error:<message>". */
export function exportResultLabel(value: string, locale: Locale): string {
  if (!value) return "";
  if (value.startsWith("ok:")) return translate(locale, "export.ok", { file: value.slice(3) });
  if (value.startsWith("error:")) return translate(locale, "export.error", { message: value.slice(6) });
  return value;
}

type Rule = [RegExp, (match: RegExpMatchArray) => string];

function translateByRules(text: string, exact: Record<string, string>, rules: Rule[]): string {
  if (exact[text]) return exact[text];
  for (const [pattern, format] of rules) {
    const match = text.match(pattern);
    if (match) return format(match);
  }
  return text;
}

const METRIC_REASONS_JA: Record<string, string> = {
  "A prior verified pending sample is required.": "比較には直前の検証済み待機サンプルが必要です。",
  "No verified runtime request counter is available for this game build.": "このゲーム環境では検証済みの要求カウンターを取得できません。",
  "No verified runtime result counter is available for this game build.": "このゲーム環境では検証済みの結果カウンターを取得できません。",
  "m_PathfindActions is not available in this runtime build.": "このゲーム環境では m_PathfindActions を取得できません。",
  "m_PathfindActions returned null.": "m_PathfindActions が null を返しました。",
  "ActionList layout is not verified in this runtime build.": "このゲーム環境では ActionList の構造を検証できていません。",
  "ActionList m_Items is not a countable collection.": "ActionList の m_Items は件数を取得できるコレクションではありません。",
  "unsupported: no verified generic service-vehicle component is available for this game build.": "未対応: このゲーム環境では検証済みの汎用サービス車両コンポーネントを取得できません。"
};

const METRIC_REASON_RULES_JA: Rule[] = [
  [/^Runtime field for '(.+)' is not available\.$/, m => `ランタイムフィールド「${m[1]}」を取得できません。`],
  [/^Runtime field for '(.+)' is not a countable collection\.$/, m => `ランタイムフィールド「${m[1]}」は件数を取得できるコレクションではありません。`],
  [/^Runtime method for '(.+)' is not available\.$/, m => `ランタイムメソッド「${m[1]}」を取得できません。`],
  [/^Reading pathfind action queue failed: (.+)$/, m => `経路探索アクションキューの読み取りに失敗しました: ${m[1]}`],
  [/^Reading '(.+)' failed: (.+)$/, m => `「${m[1]}」の読み取りに失敗しました: ${m[2]}`],
  [/^Counting (.+) failed: (.+)$/, m => `「${m[1]}」の件数取得に失敗しました: ${m[2]}`],
  [/^unsupported: no verified EntityQuery is registered for (.+)\.$/, m => `未対応: 「${m[1]}」用の検証済み EntityQuery が登録されていません。`],
  [/^unsupported: (.+)$/, m => `未対応: ${m[1]}`],
  [/^Metric '(.+)' is unavailable\.$/, m => `メトリクス「${m[1]}」は利用できません。`]
];

export function metricReasonLabel(reason: string | null | undefined, locale: Locale): string {
  if (!reason) return "";
  return locale === "ja" ? translateByRules(reason, METRIC_REASONS_JA, METRIC_REASON_RULES_JA) : reason;
}

const OVERHEAD_NOTE_JA = "この負荷率には管理コードの SystemBase 時間計測コストは含まれません。";

const CAPTURE_WARNINGS_JA: Record<string, string> = {
  "System timing projection failed for this capture; per-system timing is unavailable.": "このキャプチャのシステム時間集計に失敗したため、システム別時間は利用できません。",
  "The loaded city changed; the active capture was finalized early.": "読み込まれた都市が変わったため、実行中のキャプチャを早期終了しました。",
  "Monitoring was disabled; the active capture was finalized early and recorder activity was stopped.": "監視が無効化されたため、実行中のキャプチャを早期終了し、記録を停止しました。",
  "Managed SystemBase timing finalization failed; marker timing remains available.": "管理コードの SystemBase 時間の集計に失敗しました。マーカーによる時間は引き続き利用できます。"
};

const CAPTURE_WARNING_RULES_JA: Rule[] = [
  [/^Profiler overhead remains high; sampling stride increased to (\d+)\.$/, m => `プロファイラー負荷が高い状態が続いているため、サンプリング間引きを ${m[1]} に増やしました。`],
  [/^Profiler overhead exceeded ([^;]+); marker batching reduced to (\d+) concurrent recorders\.$/, m => `プロファイラー負荷が ${m[1]} を超えたため、同時記録数を ${m[2]} に抑えた分割計測へ切り替えました。`],
  [/^Measured capture-controller overhead remains high; sampling stride increased to (\d+)\. This overhead metric does not include managed SystemBase timing instrumentation cost\.$/, m => `詳細キャプチャ制御部分の計測負荷が高い状態が続いたため、サンプリング間引きを ${m[1]} に増やしました。${OVERHEAD_NOTE_JA}`],
  [/^Measured capture-controller overhead exceeded ([^;]+) repeatedly; marker batching reduced to (\d+) concurrent recorders\. This overhead metric does not include managed SystemBase timing instrumentation cost\.$/, m => `詳細キャプチャ制御部分の計測負荷が ${m[1]} を繰り返し超えたため、同時記録数を ${m[2]} に抑えました。${OVERHEAD_NOTE_JA}`],
  [/^Measured capture-controller overhead remained above ([^ ]+) after repeated load reductions; the capture was finalized early\. This overhead metric does not include managed SystemBase timing instrumentation cost\.$/, m => `負荷削減後も詳細キャプチャ制御部分の計測負荷が ${m[1]} を超え続けたため、キャプチャを早期終了しました。${OVERHEAD_NOTE_JA}`],
  [/^Profiler memory grew by ([^ ]+) MiB during this capture; marker batching reduced to (\d+) concurrent recorders\.$/, m => `詳細キャプチャ中のプロファイラーメモリが ${m[1]} MiB 増加したため、同時記録数を ${m[2]} に抑えました。`],
  [/^Profiler memory grew by ([^ ]+) MiB during this capture; sampling stride increased to (\d+)\.$/, m => `詳細キャプチャ中のプロファイラーメモリが ${m[1]} MiB 増加したため、サンプリング間引きを ${m[2]} に増やしました。`],
  [/^Profiler memory grew by ([^ ]+) MiB during this capture, exceeding the 512 MiB safety limit; the capture was finalized early\.$/, m => `詳細キャプチャ中のプロファイラーメモリが ${m[1]} MiB 増加して安全上限の 512 MiB を超えたため、キャプチャを早期終了しました。`],
  [/^Profiler memory baseline increased across four consecutive captures by ([^ ]+) MiB; this is a retention pressure signal, not proof of a memory leak\.$/, m => `プロファイラーメモリの基準値が4回連続のキャプチャで ${m[1]} MiB 増加しました。保持圧力の兆候ですが、メモリリークを示す証拠ではありません。`],
  [/^System timing mixes native ECS marker timing \((\d+) systems\) with managed synchronous SystemBase fallback \((\d+) systems\)\. Managed rows exclude Job\/Burst worker time, so Systems\/Mods totals do not represent total CPU cost\.$/, m => `システム時間はネイティブ ECS マーカー ${m[1]} 件と管理コード同期フォールバック ${m[2]} 件の混在です。管理コード行には Job/Burst ワーカー時間が含まれないため、システム/Mod 合計は CPU 総コストではありません。`],
  [/^Automatic capture is paused for (\d+) s after safety stop #(\d+); repeated stops extend the pause\.$/, m => `安全上限による停止（${m[2]} 回目）のため、自動キャプチャを ${m[1]} 秒間停止します。停止が繰り返されると停止時間を延長します。`],
  [/^Recorder activation failed for '(.+)': (.+)$/, m => `記録の開始に失敗しました（${m[1]}）: ${m[2]}`],
  [/^Managed SystemBase timing fallback unavailable: (.+)$/, m => `管理コードの SystemBase 時間フォールバックを利用できません: ${m[1]}`]
];

export function captureWarningLabel(warning: string, locale: Locale): string {
  if (!warning) return "";
  return locale === "ja" ? translateByRules(warning, CAPTURE_WARNINGS_JA, CAPTURE_WARNING_RULES_JA) : warning;
}

const DIAGNOSTIC_MESSAGES_JA: Record<string, string> = {
  "Per-system timing becomes available once a matching Deep Capture completes.": "システム別の実行時間は、対応する詳細キャプチャが作成されるまで利用できません。",
  "The timeline shows only the history retained by the selected capture; unavailable series are never synthesized.": "タイムラインは現在選択しているキャプチャが実際に保持した履歴だけを表示します。利用できない系列を推測で生成することはありません。",
  "Unavailable: no system timing snapshot.": "利用不可: システム時間スナップショットがありません。",
  "Patch information was detected in the current system timing snapshot.": "現在のシステム時間スナップショットでパッチ情報を検出しました。",
  "No patch owners were detected in the current system timing snapshot.": "現在のシステム時間スナップショットではパッチ所有者を検出していません。",
  "Historical capture has no retained pathfinding snapshot; this metric group is unavailable for this capture.": "過去のキャプチャには経路探索のスナップショットが保持されていないため、この指標は利用できません。",
  "Historical capture has no retained domain-metrics snapshot; this metric group is unavailable for this capture.": "過去のキャプチャにはドメイン指標のスナップショットが保持されていないため、この指標は利用できません。"
};

export function diagnosticMessageLabel(message: string, locale: Locale): string {
  if (!message) return "";
  return locale === "ja" ? DIAGNOSTIC_MESSAGES_JA[message] ?? message : message;
}

const ADVISOR_TEXTS_JA: Record<string, string> = {
  "Based on the measured GPU load, it is worth disabling depth of field and measuring again.": "計測したGPU負荷から、被写界深度を「無効」にして再計測する価値があります。",
  "Based on the measured rendering load, it is worth lowering the quality by one step and measuring again.": "計測した描画負荷から、品質を1段階下げて再計測する価値があります。",
  "Rendering has headroom under the current measurement conditions. If you raise it, measure again after the change.": "現在の計測条件では描画負荷に余裕があります。上げる場合は変更後に再計測してください。",
  "The current evidence alone does not justify a performance-motivated setting change.": "現在の根拠だけでは性能目的の設定変更を推奨できません。",
  "Both frame time and GPU time show elevated rendering load.": "フレーム時間とGPU時間の両方で描画負荷の上昇を確認しました。",
  "Direct GPU time is high. Total frame time is unavailable, so confidence is reduced.": "直接GPU時間が高い状態です。総フレーム時間は取得できないため確信度を抑えています。",
  "Simulation efficiency stays low across the measurement window, including the later part of the capture.": "キャプチャ後半を含む計測窓でシミュレーション効率の低下が継続しています。",
  "Simulation efficiency was low at the trigger, but no sustained drop was confirmed in the measurement window.": "トリガー時にはシミュレーション効率が低下していましたが、計測窓では継続的な低下を確認できませんでした。",
  "Rendering has headroom under the current measurement conditions. Diagnose again after changing settings.": "現在の計測条件では描画負荷に余裕があります。設定変更後は再診断してください。",
  "One or both captures lack this measurement.": "一方または両方のキャプチャにこの計測値がありません。",
  "Measurement definition or capability changed.": "計測の定義または取得可否が変わりました。",
  "A beneficial direction has not been established for this metric.": "この指標では改善の方向が定まっていません。",
  "Non-finite measurement.": "有限でない計測値です。",
  "Frame time is high and the main thread waits for the GPU to present each frame, so rendering on the GPU limits the frame rate.": "フレーム時間が長く、メインスレッドが毎フレームGPUの表示完了を待っているため、GPUの描画がフレームレートを制限しています。",
  "Frame time is high and the main thread is busy for almost the whole frame without waiting for the GPU, so CPU work on the main thread (including waiting for worker jobs) limits the frame rate. GPU time is not used here because it also counts time the GPU waits for the CPU.": "フレーム時間が長く、メインスレッドがGPUを待たずにほぼフレーム全体で処理を続けているため、メインスレッドのCPU処理（ワーカージョブの完了待ちを含む）がフレームレートを制限しています。GPU時間はCPUを待つ時間も含むため、この判定には使っていません。",
  "The simulation runs as fast as the frame rate allows: below 30 fps the game cannot run enough simulation steps per rendered frame (at most two per frame for each 1x of speed). Lowering frame time, not simulation load, raises the speed.": "シミュレーションはフレームレートが許す上限で動いています。30 fps を下回ると、1描画フレームで実行できるシミュレーションのステップ数（速度1倍につき最大2）が足りなくなります。速度を上げるにはシミュレーション負荷ではなくフレーム時間を下げる必要があります。",
  "The simulation often waited for pathfinding results: the game slows the simulation when fewer than 48 frames of pathfinding lead remain.": "シミュレーションが経路探索の結果を頻繁に待っていました。ゲームは経路探索の先行分が48フレームを下回るとシミュレーションを減速させます。"
};

// Sentences the backend appends to another rationale; each part is translated on its own.
const ADVISOR_SUFFIXES_JA: Record<string, string> = {
  " The Performance Preference option also limits simulation steps to the time left in each frame, so part of the slowdown may come from that setting.": "また Performance Preference 設定によりシミュレーションのステップが各フレームの残り時間に制限されるため、低下の一部はこの設定による可能性があります。"
};

/** Advisor rationale and comparison sentences are generated in English; unknown sentences stay as sent. */
export function advisorTextLabel(text: string, locale: Locale): string {
  if (!text) return "";
  if (locale !== "ja") return text;
  const exact = ADVISOR_TEXTS_JA[text];
  if (exact) return exact;
  for (const [suffix, translated] of Object.entries(ADVISOR_SUFFIXES_JA)) {
    if (!text.endsWith(suffix)) continue;
    const head = text.slice(0, -suffix.length);
    return (ADVISOR_TEXTS_JA[head] ?? head) + translated;
  }
  return text;
}

/** Canonical English sentences with a Japanese translation, for parity checks against the backend. */
export const advisorTranslatedTexts = (): readonly string[] =>
  [...Object.keys(ADVISOR_TEXTS_JA), ...Object.keys(ADVISOR_SUFFIXES_JA).map(suffix => suffix.trimStart())];
