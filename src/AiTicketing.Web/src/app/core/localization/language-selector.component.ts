import { ChangeDetectionStrategy, Component, HostListener, inject, signal } from '@angular/core';
import { LocalizationService } from './localization.service';
import { AppLanguage } from './localization.models';
import { TranslatePipe } from './translate.pipe';
import { UiIconComponent } from '../../shared/components/ui-icon/ui-icon.component';

@Component({
  selector: 'app-language-selector',
  standalone: true,
  imports: [TranslatePipe, UiIconComponent],
  template: `
    <div class="language-selector" (click)="$event.stopPropagation()">
      <button
        class="language-trigger"
        type="button"
        aria-haspopup="menu"
        [attr.aria-expanded]="isOpen()"
        [attr.aria-label]="'language.menu' | t"
        (click)="toggle()"
        (keydown)="onKeydown($event)">
        <app-ui-icon name="globe" aria-hidden="true" />
        <span>{{ 'language.menu' | t }}</span>
      </button>

      @if (isOpen()) {
        <div class="language-menu" role="menu" [attr.aria-label]="'language.menu' | t" (keydown)="onKeydown($event)">
          @for (option of language.supportedLanguages; track option.code) {
            <button
              type="button"
              class="language-option"
              role="menuitemradio"
              [attr.aria-checked]="language.language() === option.code"
              (click)="select(option.code)">
              <app-ui-icon name="check" aria-hidden="true" />
              <span>{{ label(option.code) }}</span>
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
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.4rem;
      min-width: 2.55rem;
      min-height: var(--control-height-compact);
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      background: var(--surface-elevated);
      color: var(--text-secondary);
      cursor: pointer;
      font: inherit;
      font-size: 0.78rem;
      font-weight: 600;
      box-shadow: var(--shadow-sm);
      transition: background var(--transition-fast), border-color var(--transition-fast), color var(--transition-fast);
    }

    .language-trigger app-ui-icon {
      width: 1rem;
      height: 1rem;
    }

    .language-trigger:hover,
    .language-trigger[aria-expanded="true"] {
      border-color: color-mix(in srgb, var(--primary), var(--border) 45%);
      background: var(--primary-soft);
      color: var(--text-primary);
    }

    @media (max-width: 520px) {
      .language-trigger span {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
      }
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

    .language-option {
      display: grid;
      grid-template-columns: 1rem minmax(0, 1fr);
      align-items: center;
      gap: 0.5rem;
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

    .language-option app-ui-icon {
      visibility: hidden;
      width: 0.95rem;
      height: 0.95rem;
    }

    .language-option:hover,
    .language-option:focus-visible {
      background: var(--surface-muted);
    }

    .language-option[aria-checked="true"] {
      color: var(--primary);
    }

    .language-option[aria-checked="true"] app-ui-icon {
      visibility: visible;
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

  @HostListener('document:click')
  onDocumentClick(): void {
    this.close();
  }
}
