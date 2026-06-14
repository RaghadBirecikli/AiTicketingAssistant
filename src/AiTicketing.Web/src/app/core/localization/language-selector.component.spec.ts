import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LanguageSelectorComponent } from './language-selector.component';
import { LocalizationService } from './localization.service';

describe('LanguageSelectorComponent', () => {
  let fixture: ComponentFixture<LanguageSelectorComponent>;
  let localization: LocalizationService;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LanguageSelectorComponent]
    }).compileComponents();

    localization = TestBed.inject(LocalizationService);
    fixture = TestBed.createComponent(LanguageSelectorComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.lang = 'en';
    document.documentElement.dir = 'ltr';
  });

  it('renders a compact accessible language trigger', () => {
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;

    expect(trigger.textContent).toContain('AR');
    expect(trigger.getAttribute('aria-haspopup')).toBe('menu');
    expect(trigger.getAttribute('aria-label')).toContain('العربية');
  });

  it('switches to Arabic, persists it, and updates document direction', () => {
    const trigger = (fixture.nativeElement as HTMLElement).querySelector('button') as HTMLButtonElement;
    trigger.click();
    fixture.detectChanges();

    const arabicOption = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[role="menuitemradio"]'))
      .find(option => option.textContent?.includes('العربية'));
    arabicOption?.click();
    fixture.detectChanges();

    expect(localization.language()).toBe('ar');
    expect(localStorage.getItem('ai-ticketing-language')).toBe('ar');
    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(trigger.textContent).toContain('EN');
  });
});
