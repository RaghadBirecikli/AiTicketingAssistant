import { ChangeDetectionStrategy, Component, Input, inject } from '@angular/core';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { LocalDateTimePipe } from '../../../../shared/pipes/local-date-time.pipe';
import { agentDisplayName } from '../../models/ticket-display.util';
import { AgentLookup, TicketDetails } from '../../models/ticket-list.models';
import { TicketBadgeComponent } from '../ticket-badge/ticket-badge.component';

@Component({
  selector: 'app-ticket-summary-card',
  standalone: true,
  imports: [LocalDateTimePipe, TicketBadgeComponent, TranslatePipe],
  templateUrl: './ticket-summary-card.component.html',
  styleUrl: './ticket-summary-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketSummaryCardComponent {
  readonly localization = inject(LocalizationService);
  @Input({ required: true }) ticket!: TicketDetails;
  @Input() agents: readonly AgentLookup[] = [];
  @Input() currentUserId: string | null = null;
  @Input() currentUserDisplayName: string | null = null;

  categoryLabel(): string {
    return this.localization.enumLabel('category', this.ticket.category);
  }

  sourceLabel(): string {
    return this.localization.enumLabel('source', this.ticket.source);
  }

  customerLabel(): string {
    return this.ticket.customerName || this.ticket.customerEmail || this.localization.t('tickets.notProvided');
  }

  customerDirection(): 'auto' | 'ltr' {
    return this.ticket.customerName ? 'auto' : this.ticket.customerEmail ? 'ltr' : 'auto';
  }

  assignedLabel(): string {
    if (!this.ticket.assignedToUserId) {
      return this.localization.t('tickets.unassigned');
    }

    const agent = this.agents.find(item => item.id === this.ticket.assignedToUserId);
    const lookupName = agentDisplayName(agent);
    if (lookupName) {
      return lookupName;
    }

    if (this.currentUserId === this.ticket.assignedToUserId && this.currentUserDisplayName) {
      return this.currentUserDisplayName;
    }

    return this.localization.t('tickets.assignedFallback');
  }
}
