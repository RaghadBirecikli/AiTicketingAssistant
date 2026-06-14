import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';

export type ThemePreference = 'system' | 'light' | 'dark';
export type EffectiveTheme = 'light' | 'dark';

const storageKey = 'ai-ticketing-theme';
const validPreferences: readonly ThemePreference[] = ['system', 'light', 'dark'];

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly isBrowser = isPlatformBrowser(this.platformId);
  private readonly systemTheme = signal<EffectiveTheme>(this.readSystemTheme());

  readonly preference = signal<ThemePreference>(this.readPreference());
  readonly effectiveTheme = computed<EffectiveTheme>(() => {
    const preference = this.preference();
    return preference === 'system' ? this.systemTheme() : preference;
  });

  constructor() {
    if (this.isBrowser) {
      const media = window.matchMedia('(prefers-color-scheme: dark)');
      media.addEventListener('change', event => {
        this.systemTheme.set(event.matches ? 'dark' : 'light');
      });
    }

    effect(() => {
      const preference = this.preference();
      const theme = this.effectiveTheme();
      const root = this.document.documentElement;
      root.dataset['themePreference'] = preference;
      root.dataset['theme'] = theme;

      if (this.isBrowser) {
        localStorage.setItem(storageKey, preference);
      }
    });
  }

  setPreference(preference: ThemePreference): void {
    this.preference.set(preference);
  }

  cycleTheme(): void {
    const current = this.preference();
    this.preference.set(current === 'system' ? 'light' : current === 'light' ? 'dark' : 'system');
  }

  private readPreference(): ThemePreference {
    if (!this.isBrowser) {
      return 'system';
    }

    const stored = localStorage.getItem(storageKey);
    return validPreferences.includes(stored as ThemePreference) ? stored as ThemePreference : 'system';
  }

  private readSystemTheme(): EffectiveTheme {
    if (!this.isBrowser) {
      return 'light';
    }

    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
}
