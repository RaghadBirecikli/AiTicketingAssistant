import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { LocalDateTimePipe } from '../../../../shared/pipes/local-date-time.pipe';
import { NotificationResponse } from '../../models/notification.models';

@Component({
  selector: 'app-notification-item',
  standalone: true,
  imports: [LocalDateTimePipe, TranslatePipe],
  templateUrl: './notification-item.component.html',
  styleUrl: './notification-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NotificationItemComponent {
  private readonly localization = inject(LocalizationService);

  @Input({ required: true }) notification!: NotificationResponse;
  @Input() isMarking = false;

  @Output() readonly markRead = new EventEmitter<NotificationResponse>();
  @Output() readonly openTicket = new EventEmitter<NotificationResponse>();

  openRelatedTicket(): void {
    if (this.notification.ticketId) {
      this.openTicket.emit(this.notification);
    }
  }

  onItemKeydown(event: KeyboardEvent): void {
    if (!this.notification.ticketId || (event.key !== 'Enter' && event.key !== ' ')) {
      return;
    }

    event.preventDefault();
    this.openRelatedTicket();
  }

  typeLabel(): string {
    return this.notification.type
      ? this.localization.enumLabel('notificationType', this.notification.type)
      : this.localization.t('common.unknown');
  }
}
