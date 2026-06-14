import { Pipe, PipeTransform, inject } from '@angular/core';
import { LocalizationService } from './localization.service';
import { TranslationParams } from './localization.models';

@Pipe({
  name: 't',
  standalone: true,
  pure: false
})
export class TranslatePipe implements PipeTransform {
  private readonly localization = inject(LocalizationService);

  transform(key: string, params?: TranslationParams): string {
    return this.localization.t(key, params);
  }
}
