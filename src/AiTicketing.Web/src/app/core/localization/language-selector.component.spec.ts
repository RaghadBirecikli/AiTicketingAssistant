import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LanguageSelectorComponent } from './language-selector.component';
import { LocalizationService } from './localization.service';

describe('LanguageSelectorComponent', () => {
  let fixture: ComponentFixture<LanguageSelectorComponent>;
  let localization: LocalizationService;

  async function createComponent(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [LanguageSelectorComponent]
    }).compileComponents();

    localization = TestBed.inject(LocalizationService);
    fixture = TestBed.createComponent(LanguageSelectorComponent);
    fixture.detectChanges();
  }

  function trigger(): HTMLButtonElement {
    return (fixture.nativeElement as HTMLElement).querySelector('.language-trigger') as HTMLButtonElement;
  }

  function options(): HTMLButtonElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[role="menuitemradio"]'));
  }

  beforeEach(async () => {
    localStorage.clear();
    await createComponent();
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.lang = 'en';
    document.documentElement.dir = 'ltr';
    TestBed.resetTestingModule();
  });

  it('shows English checked while English is active', () => {
    trigger().click();
    fixture.detectChanges();

    const english = options().find(option => option.textContent?.includes('English'));
    const arabic = options().find(option => option.textContent?.includes(localization.t('language.arabic')));

    expect(trigger().textContent).toContain('Language');
    expect(trigger().getAttribute('aria-expanded')).toBe('true');
    expect(english?.getAttribute('aria-checked')).toBe('true');
    expect(arabic?.getAttribute('aria-checked')).toBe('false');
  });

  it('selecting Arabic updates language, direction, persistence, and checked option', () => {
    trigger().click();
    fixture.detectChanges();

    options().find(option => option.textContent?.includes(localization.t('language.arabic')))?.click();
    fixture.detectChanges();

    expect(localization.language()).toBe('ar');
    expect(localStorage.getItem('ai-ticketing-language')).toBe('ar');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(trigger().textContent).toContain('اللغة');
    expect(trigger().textContent).not.toContain('English');

    trigger().click();
    fixture.detectChanges();

    const english = options().find(option => option.textContent?.includes('English'));
    const arabic = options().find(option => option.textContent?.includes(localization.t('language.arabic')));
    expect(english?.getAttribute('aria-checked')).toBe('false');
    expect(arabic?.getAttribute('aria-checked')).toBe('true');
  });

  it('selecting English updates language and direction from Arabic', () => {
    localization.setLanguage('ar');
    fixture.detectChanges();
    trigger().click();
    fixture.detectChanges();

    options().find(option => option.textContent?.includes('English'))?.click();
    fixture.detectChanges();

    expect(localization.language()).toBe('en');
    expect(localStorage.getItem('ai-ticketing-language')).toBe('en');
    expect(document.documentElement.lang).toBe('en');
    expect(document.documentElement.dir).toBe('ltr');
    expect(trigger().textContent).toContain('Language');
  });

  it('closes with Escape and outside click', () => {
    trigger().click();
    fixture.detectChanges();
    expect(trigger().getAttribute('aria-expanded')).toBe('true');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(trigger().getAttribute('aria-expanded')).toBe('false');

    trigger().click();
    fixture.detectChanges();
    document.body.click();
    fixture.detectChanges();

    expect(trigger().getAttribute('aria-expanded')).toBe('false');
  });

  it('restores the persisted language before rendering', async () => {
    TestBed.resetTestingModule();
    localStorage.setItem('ai-ticketing-language', 'ar');

    await createComponent();

    expect(localization.language()).toBe('ar');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    trigger().click();
    fixture.detectChanges();

    const arabic = options().find(option => option.textContent?.includes(localization.t('language.arabic')));
    expect(arabic?.getAttribute('aria-checked')).toBe('true');
  });
});
