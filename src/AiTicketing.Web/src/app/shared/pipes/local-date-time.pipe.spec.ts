import { TestBed } from '@angular/core/testing';
import { LocalizationService } from '../../core/localization/localization.service';
import { LocalDateTimePipe } from './local-date-time.pipe';

describe('LocalDateTimePipe', () => {
  let pipe: LocalDateTimePipe;
  let localization: LocalizationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LocalDateTimePipe]
    });
    pipe = TestBed.inject(LocalDateTimePipe);
    localization = TestBed.inject(LocalizationService);
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.lang = 'en';
    document.documentElement.dir = 'ltr';
  });

  it('uses localized fallback labels', () => {
    localization.setLanguage('ar');

    expect(pipe.transform(null)).toBe('غير محدد');
    expect(pipe.transform('not-a-date')).toBe('تاريخ غير صالح');
  });

  it('formats dates using the active locale', () => {
    const value = '2026-06-02T11:56:12.6717286+00:00';

    localization.setLanguage('en');
    const english = pipe.transform(value);

    localization.setLanguage('ar');
    const arabic = pipe.transform(value);

    expect(english).not.toBe(arabic);
    expect(arabic.length).toBeGreaterThan(0);
  });
});
