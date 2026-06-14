import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { LocalizationService } from '../../../../core/localization/localization.service';

@Component({
  selector: 'app-ticket-badge',
  standalone: true,
  template: '<span class="badge" [class.priority]="kind === \'priority\'">{{ label }}</span>',
  styles: [`
    .badge {
      display: inline-flex;
      border: 1px solid color-mix(in srgb, var(--info), var(--border) 55%);
      border-radius: 999px;
      background: var(--info-soft);
      color: var(--info);
      font-size: .78rem;
      font-weight: 600;
      padding: .25rem .55rem;
      word-break: break-word;
    }

    .badge.priority {
      border-color: color-mix(in srgb, var(--warning), var(--border) 55%);
      background: var(--warning-soft);
      color: var(--warning);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketBadgeComponent {
  private readonly localization = inject(LocalizationService);
  @Input({ required: true }) value = '';
  @Input() kind: 'status' | 'priority' = 'status';

  get label(): string {
    return this.kind === 'priority'
      ? this.localization.enumLabel('priority', this.value)
      : this.localization.enumLabel('status', this.value);
  }
}
