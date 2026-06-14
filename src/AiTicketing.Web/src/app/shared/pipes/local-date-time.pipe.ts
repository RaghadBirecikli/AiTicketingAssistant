import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocalizationService } from '../../core/localization/localization.service';

@Pipe({
  name: 'localDateTime',
  standalone: true,
  pure: false
})
export class LocalDateTimePipe implements PipeTransform {
  private readonly localization = inject(LocalizationService);

  transform(value: string | null | undefined): string {
    if (!value) {
      return this.localization.t('common.notSet');
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return this.localization.t('common.invalidDate');
    }

    return new Intl.DateTimeFormat(this.localization.locale(), {
      dateStyle: 'medium',
      timeStyle: 'short'
    }).format(parsed);
  }
}
