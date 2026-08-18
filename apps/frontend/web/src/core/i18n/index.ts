import { createI18n } from "vue-i18n";

// Сборка локалей: каждый раздел и каждая фича держит переводы рядом с кодом в
// своей папке i18n/<locale>.json (co-location). Здесь они собираются в один набор,
// поэтому компоненты обращаются к ним через обычный useI18n(). Локаль берётся из
// имени файла (ru.json -> "ru").
//
// eager: значения нужны синхронно при старте приложения (до mount), поэтому не
// ленивый импорт.
const modules = import.meta.glob("../../**/i18n/**/*.json", {
  eager: true,
}) as Record<string, { default?: Record<string, unknown> }>;

export const UI_LOCALES = ["ru", "en", "kk"] as const;
export type UiLocale = (typeof UI_LOCALES)[number];

/**
 * Культура по умолчанию. Должна совпадать с `Cultures:DefaultCulture` на бэкенде:
 * именно в неё пишется инлайн-значение локализуемого поля, и она же служит
 * фолбэком при чтении на культуре без перевода (ADR 0021).
 */
export const DEFAULT_UI_LOCALE: UiLocale = "ru";

export function isUiLocale(value: string): value is UiLocale {
  return (UI_LOCALES as readonly string[]).includes(value);
}

function localeFromPath(path: string): string | null {
  const match = path.replace(/\\/g, "/").match(/\/([^/]+)\.json$/);
  return match?.[1] ?? null;
}

type LocaleMessages = Record<string, Record<string, never>>;

function collectMessages(): LocaleMessages {
  const messages: Record<string, Record<string, unknown>> = {};

  for (const [path, mod] of Object.entries(modules)) {
    const locale = localeFromPath(path);
    if (!locale || !mod.default) {
      continue;
    }

    // Глубокое слияние не нужно: словари разных фич не пересекаются по ключам
    // верхнего уровня - у каждого свой namespace по имени фичи.
    messages[locale] = { ...messages[locale], ...mod.default };
  }

  // Ключи словарей известны только в рантайме (они собираются глобом), поэтому
  // структуру сообщений vue-i18n здесь не вывести. Приведение отдаёт ему пустую
  // форму: типизация обращений t("...") в этом проекте не включена.
  return messages as LocaleMessages;
}

export function createAppI18n() {
  // legacy: false обязателен - иначе useI18n() в <script setup> не работает.
  return createI18n({
    legacy: false,
    locale: DEFAULT_UI_LOCALE,
    fallbackLocale: DEFAULT_UI_LOCALE,
    messages: collectMessages(),
  });
}
