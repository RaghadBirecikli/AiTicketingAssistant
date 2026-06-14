export type AppLanguage = 'en' | 'ar';
export type TextDirection = 'ltr' | 'rtl';

export interface SupportedLanguage {
  code: AppLanguage;
  label: string;
}

export type TranslationParams = Record<string, string | number>;
export interface TranslationDictionary {
  [key: string]: string | TranslationDictionary;
}
