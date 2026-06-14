import { ChangeDetectionStrategy, Component, HostListener, inject } from '@angular/core';
import { NotificationStateService } from '../../services/notification-state.service';
import { NotificationPanelComponent } from '../notification-panel/notification-panel.component';
import { UiIconComponent } from '../../../../shared/components/ui-icon/ui-icon.component';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [NotificationPanelComponent, TranslatePipe, UiIconComponent],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotificationBellComponent {
  readonly notifications = inject(NotificationStateService);

  @HostListener('document:keydown.escape')
  closeOnEscape(): void {
    this.notifications.closePanel();
  }
}
