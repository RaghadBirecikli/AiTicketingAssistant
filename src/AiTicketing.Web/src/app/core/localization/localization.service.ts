import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, effect, inject, signal } from '@angular/core';
import { AppLanguage, SupportedLanguage, TextDirection, TranslationDictionary, TranslationParams } from './localization.models';
import { translations } from './translations';

const storageKey = 'ai-ticketing-language';
const fallbackLanguage: AppLanguage = 'en';
const mojibakePattern = /[ØÙ]/;

@Injectable({ providedIn: 'root' })
export class LocalizationService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  private readonly languageSignal = signal<AppLanguage>(this.readLanguage());

  readonly language = this.languageSignal.asReadonly();
  readonly supportedLanguages: readonly SupportedLanguage[] = [
    { code: 'en', label: 'English' },
    { code: 'ar', label: 'العربية' }
  ];

  constructor() {
    this.applyLanguage(this.languageSignal());

    effect(() => {
      this.applyLanguage(this.languageSignal());
    });
  }

  setLanguage(language: AppLanguage): void {
    this.languageSignal.set(language);
    this.applyLanguage(language);
  }

  direction(): TextDirection {
    return this.directionFor(this.languageSignal());
  }

  locale(): string {
    return this.languageSignal() === 'ar' ? 'ar' : 'en-US';
  }

  storageKey(): string {
    return storageKey;
  }

  t(key: string, params: TranslationParams = {}): string {
    const value = this.lookup(translations[this.languageSignal()], key)
      ?? this.lookup(translations[fallbackLanguage], key)
      ?? key;

    return this.interpolate(this.normalizeTranslation(value), params);
  }

  enumLabel(group: 'status' | 'priority' | 'category' | 'source' | 'role' | 'sort' | 'direction' | 'notificationType', value: string | null | undefined): string {
    if (!value) {
      return this.t('common.unknown');
    }

    const translated = this.lookup(translations[this.languageSignal()], `enums.${group}.${value}`)
      ?? this.lookup(translations[fallbackLanguage], `enums.${group}.${value}`);
    return translated ? this.normalizeTranslation(translated) : this.readableValue(value);
  }

  private readLanguage(): AppLanguage {
    if (!this.isBrowser) {
      return fallbackLanguage;
    }

    const stored = localStorage.getItem(storageKey);
    return stored === 'ar' || stored === 'en' ? stored : fallbackLanguage;
  }

  private directionFor(language: AppLanguage): TextDirection {
    return language === 'ar' ? 'rtl' : 'ltr';
  }

  private applyLanguage(language: AppLanguage): void {
    const direction = this.directionFor(language);
    const root = this.document.documentElement;
    root.lang = language;
    root.dir = direction;
    root.dataset['language'] = language;
    root.dataset['direction'] = direction;
    this.document.title = this.t('common.appName');

    if (this.isBrowser) {
      localStorage.setItem(storageKey, language);
    }
  }

  private lookup(dictionary: TranslationDictionary, key: string): string | null {
    let current: string | TranslationDictionary | undefined = dictionary;
    for (const segment of key.split('.')) {
      if (typeof current !== 'object' || current === null) {
        return null;
      }
      current = current[segment];
    }

    return typeof current === 'string' ? current : null;
  }

  private interpolate(value: string, params: TranslationParams): string {
    return value.replace(/\{(\w+)\}/g, (_match, name: string) =>
      Object.prototype.hasOwnProperty.call(params, name) ? String(params[name]) : `{${name}}`);
  }

  private normalizeTranslation(value: string): string {
    if (!mojibakePattern.test(value) || typeof TextDecoder === 'undefined') {
      return value;
    }

    try {
      const bytes = Uint8Array.from(Array.from(value, character => character.charCodeAt(0) & 0xff));
      return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    } catch {
      return value;
    }
  }

  private readableValue(value: string): string {
    return value
      .replace(/([a-z])([A-Z])/g, '$1 $2')
      .replace(/[_-]+/g, ' ')
      .trim();
  }
}
