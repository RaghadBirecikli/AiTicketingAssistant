import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { TicketDetailsMessage } from '../../models/ticket-list.models';
import { TicketMessageComponent } from '../ticket-message/ticket-message.component';

@Component({
  selector: 'app-ticket-conversation',
  standalone: true,
  imports: [TicketMessageComponent, TranslatePipe],
  templateUrl: './ticket-conversation.component.html',
  styleUrl: './ticket-conversation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketConversationComponent {
  @Input({ required: true }) messages: readonly TicketDetailsMessage[] = [];
}
