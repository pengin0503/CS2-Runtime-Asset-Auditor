import React, { createContext, useCallback, useContext, useMemo } from "react";
import { bindValue, useValue } from "cs2/api";
import { messages, type MessageKey } from "./messages";

export type Locale = "en" | "ja";

// Published by ProfilerUISystem from the game's active interface locale ("ja-JP", "en-US", ...).
const localeBinding = bindValue<string>("CS2RuntimeAssetAuditor", "locale", "en-US");

/** Japanese game locales use the Japanese texts; every other locale uses English. */
export function resolveLocale(localeId: string | null | undefined): Locale {
  return (localeId ?? "").trim().toLowerCase().startsWith("ja") ? "ja" : "en";
}

const LocaleOverride = createContext<Locale | null>(null);

/** Forces a locale for a subtree (tests and previews); the game binding decides otherwise. */
export function LocaleProvider({ locale, children }: { locale: Locale; children?: React.ReactNode }) {
  return <LocaleOverride.Provider value={locale}>{children}</LocaleOverride.Provider>;
}

export function useLocale(): Locale {
  const override = useContext(LocaleOverride);
  const localeId = useValue(localeBinding);
  return override ?? resolveLocale(localeId);
}

export type TextParams = Record<string, string | number>;
export type Translate = (key: MessageKey, params?: TextParams) => string;

export function translate(locale: Locale, key: MessageKey, params?: TextParams): string {
  const template = messages[locale][key] ?? messages.en[key] ?? key;
  if (!params) return template;
  return template.replace(/\{(\w+)\}/g, (match, name: string) => (name in params ? String(params[name]) : match));
}

export function useText(): { locale: Locale; t: Translate } {
  const locale = useLocale();
  const t = useCallback<Translate>((key, params) => translate(locale, key, params), [locale]);
  return useMemo(() => ({ locale, t }), [locale, t]);
}
