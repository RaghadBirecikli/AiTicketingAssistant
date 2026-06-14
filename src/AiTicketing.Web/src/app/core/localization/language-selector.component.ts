import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { LocalizationService } from './localization.service';
import { AppLanguage } from './localization.models';
import { TranslatePipe } from './translate.pipe';

@Component({
  selector: 'app-language-selector',
  standalone: true,
  imports: [TranslatePipe],
  template: `
    <div class="language-selector">
      <button
        class="language-trigger"
        type="button"
        aria-haspopup="menu"
        [attr.aria-expanded]="isOpen()"
        [attr.aria-label]="'language.switchTo' | t: { language: targetLabel() }"
        (click)="toggle()"
        (keydown)="onKeydown($event)">
        <span aria-hidden="true">{{ targetCode().toUpperCase() }}</span>
      </button>

      @if (isOpen()) {
        <div class="language-menu" role="menu" [attr.aria-label]="'language.menu' | t" (keydown)="onKeydown($event)">
          @for (option of language.supportedLanguages; track option.code) {
            <button
              type="button"
              role="menuitemradio"
              [attr.aria-checked]="language.language() === option.code"
              (click)="select(option.code)">
              {{ label(option.code) }}
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .language-selector {
      position: relative;
    }

    .language-trigger {
      display: inline-grid;
      min-width: 2.25rem;
      min-height: 2.25rem;
      place-items: center;
      border: 1px solid transparent;
      border-radius: var(--radius-sm);
      background: transparent;
      color: var(--text-secondary);
      cursor: pointer;
      font: inherit;
      font-size: 0.78rem;
      font-weight: 600;
    }

    .language-trigger:hover,
    .language-trigger[aria-expanded="true"] {
      background: var(--surface-muted);
      color: var(--text-primary);
    }

    .language-menu {
      position: absolute;
      inset-block-start: calc(100% + 0.45rem);
      inset-inline-end: 0;
      z-index: var(--z-popover);
      display: grid;
      min-inline-size: 10rem;
      border: 1px solid var(--border);
      border-radius: var(--radius-md);
      background: var(--surface-elevated);
      box-shadow: var(--shadow-md);
      padding: 0.35rem;
    }

    .language-menu button {
      min-height: 2.25rem;
      border: 0;
      border-radius: var(--radius-sm);
      background: transparent;
      color: var(--text-primary);
      cursor: pointer;
      font: inherit;
      font-weight: 500;
      padding: 0.45rem 0.6rem;
      text-align: start;
    }

    .language-menu button:hover,
    .language-menu button:focus-visible {
      background: var(--surface-muted);
    }

    .language-menu button[aria-checked="true"] {
      color: var(--primary);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LanguageSelectorComponent {
  readonly language = inject(LocalizationService);
  readonly isOpen = signal(false);

  toggle(): void {
    this.isOpen.update(value => !value);
  }

  select(language: AppLanguage): void {
    this.language.setLanguage(language);
    this.close();
  }

  close(): void {
    this.isOpen.set(false);
  }

  currentLabel(): string {
    return this.label(this.language.language());
  }

  targetCode(): AppLanguage {
    return this.language.language() === 'ar' ? 'en' : 'ar';
  }

  targetLabel(): string {
    return this.label(this.targetCode());
  }

  label(language: AppLanguage): string {
    return this.language.t(language === 'ar' ? 'language.arabic' : 'language.english');
  }

  onKeydown(event: KeyboardEvent): void {
    if ((event.key === 'Enter' || event.key === ' ') && !this.isOpen()) {
      event.preventDefault();
      this.isOpen.set(true);
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    }
  }

  @HostListener('document:keydown.escape')
  onDocumentEscape(): void {
    this.close();
  }
}
