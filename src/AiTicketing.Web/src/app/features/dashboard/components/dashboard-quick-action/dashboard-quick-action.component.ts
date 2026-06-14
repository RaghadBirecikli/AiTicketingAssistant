import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiIconComponent, UiIconName } from '../../../../shared/components/ui-icon/ui-icon.component';

@Component({
  selector: 'app-dashboard-quick-action',
  standalone: true,
  imports: [RouterLink, UiIconComponent],
  templateUrl: './dashboard-quick-action.component.html',
  styleUrl: './dashboard-quick-action.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DashboardQuickActionComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) description = '';
  @Input({ required: true }) link = '/';
  @Input() queryParams: Record<string, string | number | boolean> | null = null;

  get iconName(): UiIconName {
    const label = this.label.toLowerCase();
    if (label.includes('create')) {
      return 'plus';
    }
    if (label.includes('urgent')) {
      return 'alert';
    }
    if (label.includes('unassigned')) {
      return 'user';
    }
    return 'ticket';
  }
}
