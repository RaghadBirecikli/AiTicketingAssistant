import { TestBed } from '@angular/core/testing';
import { LocalizationService } from './localization.service';

describe('LocalizationService', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    document.documentElement.lang = 'en';
    document.documentElement.dir = 'ltr';
  });

  it('defaults to English and LTR when no language is stored', () => {
    const service = TestBed.inject(LocalizationService);

    expect(service.language()).toBe('en');
    expect(service.direction()).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
  });

  it('restores Arabic from its dedicated storage key and applies RTL', () => {
    localStorage.setItem('ai-ticketing-language', 'ar');
    localStorage.setItem('ai-ticketing-theme', 'dark');
    TestBed.resetTestingModule();

    const service = TestBed.inject(LocalizationService);

    expect(service.language()).toBe('ar');
    expect(service.direction()).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('dark');
  });

  it('persists language changes without touching auth or theme storage', () => {
    localStorage.setItem('ai-ticketing-theme', 'light');
    sessionStorage.setItem('ai-ticketing-auth', 'token');

    const service = TestBed.inject(LocalizationService);
    service.setLanguage('ar');

    expect(localStorage.getItem(service.storageKey())).toBe('ar');
    expect(localStorage.getItem('ai-ticketing-theme')).toBe('light');
    expect(sessionStorage.getItem('ai-ticketing-auth')).toBe('token');
  });

  it('translates known enum labels without changing enum values', () => {
    const service = TestBed.inject(LocalizationService);
    service.setLanguage('ar');

    expect(service.enumLabel('status', 'InProgress')).toBe('قيد المعالجة');
    expect(service.enumLabel('priority', 'Urgent')).toBe('عاجلة');
    expect('InProgress').toBe('InProgress');
  });
});
