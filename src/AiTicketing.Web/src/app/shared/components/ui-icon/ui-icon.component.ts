import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

export type UiIconName =
  | 'activity'
  | 'ai'
  | 'alert'
  | 'bell'
  | 'check'
  | 'chevron-left'
  | 'chevron-right'
  | 'clock'
  | 'dashboard'
  | 'globe'
  | 'menu'
  | 'message'
  | 'moon'
  | 'plus'
  | 'refresh'
  | 'search'
  | 'sun'
  | 'ticket'
  | 'user';

@Component({
  selector: 'app-ui-icon',
  standalone: true,
  template: `
    <svg
      aria-hidden="true"
      focusable="false"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round">
      @switch (name) {
        @case ('activity') {
          <path d="M3 12h4l3-7 4 14 3-7h4" />
        }
        @case ('ai') {
          <path d="M12 3l1.6 5.2L19 10l-5.4 1.8L12 17l-1.6-5.2L5 10l5.4-1.8L12 3z" />
          <path d="M19 16l.7 2.3L22 19l-2.3.7L19 22l-.7-2.3L16 19l2.3-.7L19 16z" />
        }
        @case ('alert') {
          <path d="M12 8v5" />
          <path d="M12 17h.01" />
          <path d="M10.3 3.9L2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" />
        }
        @case ('bell') {
          <path d="M18 8a6 6 0 1 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" />
          <path d="M10 21a2 2 0 0 0 4 0" />
        }
        @case ('check') {
          <path d="M20 6L9 17l-5-5" />
        }
        @case ('chevron-left') {
          <path d="M15 18l-6-6 6-6" />
        }
        @case ('chevron-right') {
          <path d="M9 18l6-6-6-6" />
        }
        @case ('clock') {
          <circle cx="12" cy="12" r="9" />
          <path d="M12 7v5l3 2" />
        }
        @case ('dashboard') {
          <path d="M4 13a8 8 0 0 1 16 0" />
          <path d="M12 13l4-4" />
          <path d="M5 20h14" />
        }
        @case ('globe') {
          <circle cx="12" cy="12" r="9" />
          <path d="M3 12h18" />
          <path d="M12 3a13.5 13.5 0 0 1 0 18" />
          <path d="M12 3a13.5 13.5 0 0 0 0 18" />
        }
        @case ('menu') {
          <path d="M4 7h16" />
          <path d="M4 12h16" />
          <path d="M4 17h16" />
        }
        @case ('message') {
          <path d="M21 15a4 4 0 0 1-4 4H8l-5 3V7a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4v8z" />
        }
        @case ('moon') {
          <path d="M21 12.8A8.5 8.5 0 1 1 11.2 3 6.5 6.5 0 0 0 21 12.8z" />
        }
        @case ('plus') {
          <path d="M12 5v14" />
          <path d="M5 12h14" />
        }
        @case ('refresh') {
          <path d="M20 12a8 8 0 0 1-13.7 5.7" />
          <path d="M4 12A8 8 0 0 1 17.7 6.3" />
          <path d="M17 2v5h5" />
          <path d="M7 22v-5H2" />
        }
        @case ('search') {
          <circle cx="11" cy="11" r="7" />
          <path d="M20 20l-3.5-3.5" />
        }
        @case ('sun') {
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2" />
          <path d="M12 20v2" />
          <path d="M4.9 4.9l1.4 1.4" />
          <path d="M17.7 17.7l1.4 1.4" />
          <path d="M2 12h2" />
          <path d="M20 12h2" />
          <path d="M4.9 19.1l1.4-1.4" />
          <path d="M17.7 6.3l1.4-1.4" />
        }
        @case ('ticket') {
          <path d="M4 7a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v3a2 2 0 1 0 0 4v3a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-3a2 2 0 1 0 0-4V7z" />
          <path d="M9 9h6" />
          <path d="M9 15h6" />
        }
        @case ('user') {
          <circle cx="12" cy="8" r="4" />
          <path d="M4 21a8 8 0 0 1 16 0" />
        }
      }
    </svg>
  `,
  styles: [`
    :host {
      display: inline-grid;
      width: 1.25rem;
      height: 1.25rem;
      place-items: center;
      color: currentColor;
    }

    svg {
      width: 100%;
      height: 100%;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UiIconComponent {
  @Input({ required: true }) name: UiIconName = 'ticket';
}
