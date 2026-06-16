import { ChangeDetectionStrategy, Component, EventEmitter, inject, Output } from '@angular/core';
import { NotificationStateService } from '../../services/notification-state.service';
import { NotificationRealtimeConnectionState, NotificationRealtimeService } from '../../services/notification-realtime.service';
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

  liveStateClass(): string {
    const state = this.realtime.connectionState();
    return `live-state live-state-${state}`;
  }

  liveStateLabel(): string {
    switch (this.realtime.connectionState() satisfies NotificationRealtimeConnectionState) {
      case 'connected':
        return 'notifications.liveConnected';
      case 'reconnecting':
        return 'notifications.liveReconnecting';
      case 'connecting':
        return 'notifications.liveConnecting';
      case 'disconnected':
        return 'notifications.liveUnavailable';
    }
  }
}
