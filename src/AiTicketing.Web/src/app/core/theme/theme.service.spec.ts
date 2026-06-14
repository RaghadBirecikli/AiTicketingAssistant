import { TestBed } from '@angular/core/testing';
import { ThemePreference, ThemeService } from './theme.service';

describe('ThemeService', () => {
  let matchMediaSpy: jasmine.Spy;
  let mediaListener: ((event: MediaQueryListEvent) => void) | null;

  function mediaQueryList(matches: boolean): MediaQueryList {
    return {
      matches,
      media: '(prefers-color-scheme: dark)',
      onchange: null,
      addEventListener: jasmine.createSpy('addEventListener').and.callFake((_event: string, listener: (event: MediaQueryListEvent) => void) => {
        mediaListener = listener;
      }),
      removeEventListener: jasmine.createSpy('removeEventListener'),
      addListener: jasmine.createSpy('addListener'),
      removeListener: jasmine.createSpy('removeListener'),
      dispatchEvent: jasmine.createSpy('dispatchEvent')
    } as unknown as MediaQueryList;
  }

  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
    mediaListener = null;
    matchMediaSpy = spyOn(window, 'matchMedia').and.returnValue(mediaQueryList(false));
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('initializes from system preference by default', () => {
    const service = TestBed.inject(ThemeService);
    TestBed.flushEffects();

    expect(service.preference()).toBe('system');
    expect(service.effectiveTheme()).toBe('light');
    expect(document.documentElement.dataset['themePreference']).toBe('system');
    expect(document.documentElement.dataset['theme']).toBe('light');
  });

  it('uses dark system preference when selected system is dark', () => {
    matchMediaSpy.and.returnValue(mediaQueryList(true));

    const service = TestBed.inject(ThemeService);
    TestBed.flushEffects();

    expect(service.preference()).toBe('system');
    expect(service.effectiveTheme()).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('persists explicit theme selection', () => {
    const service = TestBed.inject(ThemeService);

    service.setPreference('dark');
    TestBed.flushEffects();

    expect(service.preference()).toBe('dark');
    expect(service.effectiveTheme()).toBe('dark');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('restores a persisted light theme preference', () => {
    localStorage.setItem('ai-ticketing-theme', 'light' satisfies ThemePreference);

    const service = TestBed.inject(ThemeService);
    TestBed.flushEffects();

    expect(service.preference()).toBe('light');
    expect(service.effectiveTheme()).toBe('light');
  });

  it('keeps light and system visually light while preserving distinct stored preferences', () => {
    const service = TestBed.inject(ThemeService);

    service.setPreference('light');
    TestBed.flushEffects();

    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('light');

    service.setPreference('system');
    TestBed.flushEffects();

    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('system');
    expect(document.documentElement.dataset['themePreference']).toBe('system');
  });

  it('updates system theme when OS preference changes', () => {
    const service = TestBed.inject(ThemeService);
    service.setPreference('system');
    TestBed.flushEffects();

    mediaListener?.({ matches: true } as MediaQueryListEvent);
    TestBed.flushEffects();

    expect(service.effectiveTheme()).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('aligns the root dataset before Angular pages render to avoid theme flash', () => {
    localStorage.setItem('ai-ticketing-theme', 'dark' satisfies ThemePreference);

    const service = TestBed.inject(ThemeService);
    TestBed.flushEffects();

    expect(service.preference()).toBe('dark');
    expect(document.documentElement.dataset['themePreference']).toBe('dark');
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });
});
