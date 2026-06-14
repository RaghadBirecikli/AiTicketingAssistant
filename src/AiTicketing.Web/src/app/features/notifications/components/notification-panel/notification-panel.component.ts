import { ChangeDetectionStrategy, Component, EventEmitter, inject, Output } from '@angular/core';
import { NotificationStateService } from '../../services/notification-state.service';
import { NotificationRealtimeService } from '../../services/notification-realtime.service';
import { NotificationItemComponent } from '../notification-item/notification-item.component';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';

@Component({
  selector: 'app-notification-panel',
  standalone: true,
  imports: [NotificationItemComponent, TranslatePipe],
  templateUrl: './notification-panel.component.html',
  styleUrl: './notification-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotificationPanelComponent {
  readonly notifications = inject(NotificationStateService);
  readonly realtime = inject(NotificationRealtimeService);

  @Output() readonly closePanel = new EventEmitter<void>();
}
