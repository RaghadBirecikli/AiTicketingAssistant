import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { LocalDateTimePipe } from '../../../../shared/pipes/local-date-time.pipe';
import { TicketDetailsMessage } from '../../models/ticket-list.models';

@Component({
  selector: 'app-ticket-message',
  standalone: true,
  imports: [LocalDateTimePipe, TranslatePipe],
  templateUrl: './ticket-message.component.html',
  styleUrl: './ticket-message.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketMessageComponent {
  readonly localization = inject(LocalizationService);
  @Input({ required: true }) message!: TicketDetailsMessage;

  senderLabel(): string {
    if (this.message.senderDisplayName) {
      return this.message.senderDisplayName;
    }

    if (this.message.senderRole) {
      return this.localization.enumLabel('role', this.message.senderRole);
    }

    return this.localization.t('messages.unknownSender');
  }

  roleLabel(): string {
    return this.message.senderRole
      ? this.localization.enumLabel('role', this.message.senderRole)
      : this.localization.t('messages.unknownRole');
  }
}
