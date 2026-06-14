import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { UiIconComponent, UiIconName } from '../../../../shared/components/ui-icon/ui-icon.component';

@Component({
  selector: 'app-dashboard-stat-card',
  standalone: true,
  imports: [UiIconComponent],
  templateUrl: './dashboard-stat-card.component.html',
  styleUrl: './dashboard-stat-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardStatCardComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) value = 0;
  @Input() explanation: string | null = null;
  @Input() link: string | null = null;
  @Input() queryParams: Record<string, string | number | boolean> | null = null;

  get iconName(): UiIconName {
    const label = this.label.toLowerCase();
    if (label.includes('urgent')) {
      return 'alert';
    }
    if (label.includes('resolved') || label.includes('closed')) {
      return 'check';
    }
    if (label.includes('progress') || label.includes('open')) {
      return 'clock';
    }
    if (label.includes('unassigned')) {
      return 'user';
    }
    return 'ticket';
  }
}
