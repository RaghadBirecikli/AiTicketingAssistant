import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiError } from '../../../../core/api/api-error';
import { AuthService } from '../../../../core/auth/auth.service';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { UserRole } from '../../../../core/models/role.model';
import { TicketAiPanelComponent } from '../../ai/components/ticket-ai-panel/ticket-ai-panel.component';
import { TicketAssignmentControlComponent, AssignTicketSubmit } from '../../components/ticket-assignment-control/ticket-assignment-control.component';
import { TicketConversationComponent } from '../../components/ticket-conversation/ticket-conversation.component';
import { MessageComposerSubmit, TicketMessageComposerComponent } from '../../components/ticket-message-composer/ticket-message-composer.component';
import { TicketStatusControlComponent } from '../../components/ticket-status-control/ticket-status-control.component';
import { TicketSummaryCardComponent } from '../../components/ticket-summary-card/ticket-summary-card.component';
import { TicketListService } from '../../data-access/ticket-list.service';
import { AgentLookup, TicketDetails, TicketDetailsMessage, TicketListItem, TicketStatus } from '../../models/ticket-list.models';

type DetailsState = 'ready' | 'not-found' | 'forbidden' | 'error';

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

@Component({
  selector: 'app-ticket-details-page',
  standalone: true,
  imports: [
    TicketAssignmentControlComponent,
    TicketAiPanelComponent,
    TicketConversationComponent,
    TicketMessageComposerComponent,
    TicketStatusControlComponent,
    TicketSummaryCardComponent,
    TranslatePipe
  ],
  templateUrl: './ticket-details-page.component.html',
  styleUrl: './ticket-details-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketDetailsPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  readonly localization = inject(LocalizationService);
  private readonly ticketListService = inject(TicketListService);
  private readonly destroyRef = inject(DestroyRef);

  readonly role = this.authService.currentRole;
  readonly currentUser = this.authService.currentUser;
  readonly currentUserDisplayName = computed(() => {
    const user = this.currentUser();
    return user?.displayName || user?.email || null;
  });
  readonly ticket = signal<TicketDetails | null>(null);
  readonly isLoading = signal(false);
  readonly state = signal<DetailsState>('ready');
  readonly errorMessage = computed(() => {
    switch (this.state()) {
      case 'not-found':
        return this.localization.t('ticketDetails.notFound');
      case 'forbidden':
        return this.localization.t('ticketDetails.forbidden');
      case 'error':
        return this.localization.t('ticketDetails.error');
      default:
        return null;
    }
  });
  readonly sortedMessages = computed(() => this.sortMessages(this.ticket()?.messages ?? []));
  readonly canAddInternalNote = computed(() => this.role() === 'Admin' || this.role() === 'Agent');
  readonly canUseAi = computed(() => this.role() === 'Admin' || this.role() === 'Agent');
  readonly canAssignTicket = computed(() => this.role() === 'Admin');
  readonly canChangeStatus = computed(() => this.role() === 'Admin' || this.role() === 'Agent');
  readonly agents = signal<readonly AgentLookup[]>([]);
  readonly isSendingMessage = signal(false);
  readonly isSendingInternalNote = signal(false);
  readonly isAssigning = signal(false);
  readonly isChangingStatus = signal(false);
  readonly messageError = signal<string | null>(null);
  readonly internalNoteError = signal<string | null>(null);
  readonly assignmentError = signal<string | null>(null);
  readonly statusError = signal<string | null>(null);
  readonly messageSuccess = signal<string | null>(null);
  readonly internalNoteSuccess = signal<string | null>(null);
  readonly assignmentSuccess = signal<string | null>(null);
  readonly statusSuccess = signal<string | null>(null);

  private ticketId: string | null = null;

  @ViewChild('publicComposer') private publicComposer?: TicketMessageComposerComponent;
  @ViewChild('internalComposer') private internalComposer?: TicketMessageComposerComponent;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || !guidPattern.test(id)) {
      this.state.set('not-found');
      return;
    }

    this.ticketId = id;
    this.loadTicket(id);
    if (this.role() === 'Admin') {
      this.loadAgents();
    }
  }

  retry(): void {
    if (this.ticketId) {
      this.loadTicket(this.ticketId);
    }
  }

  backToTickets(): void {
    void this.router.navigateByUrl(this.safeReturnUrl());
  }

  addPublicMessage(submit: MessageComposerSubmit): void {
    if (!this.ticketId || submit.mode !== 'public' || this.isSendingMessage()) {
      return;
    }

    this.isSendingMessage.set(true);
    this.messageError.set(null);
    this.messageSuccess.set(null);

    this.ticketListService.addMessage(this.ticketId, submit.body.trim())
      .pipe(
        finalize(() => this.isSendingMessage.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: message => {
          this.appendMessage({ ...message, senderRole: this.role() ?? message.senderRole });
          this.publicComposer?.clear();
          this.messageSuccess.set(this.localization.t('messages.sent'));
        },
        error: error => this.messageError.set(this.safeComposerError(error))
      });
  }

  addInternalNote(submit: MessageComposerSubmit): void {
    if (!this.ticketId || submit.mode !== 'internal' || this.isSendingInternalNote()) {
      return;
    }

    this.isSendingInternalNote.set(true);
    this.internalNoteError.set(null);
    this.internalNoteSuccess.set(null);

    this.ticketListService.addInternalNote(this.ticketId, submit.body.trim())
      .pipe(
        finalize(() => this.isSendingInternalNote.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: message => {
          this.appendMessage(message);
          this.internalComposer?.clear();
          this.internalNoteSuccess.set(this.localization.t('messages.noteAdded'));
        },
        error: error => this.internalNoteError.set(this.safeComposerError(error))
      });
  }

  assignTicket(submit: AssignTicketSubmit): void {
    if (!this.ticketId || this.isAssigning()) {
      return;
    }

    this.isAssigning.set(true);
    this.assignmentError.set(null);
    this.assignmentSuccess.set(null);

    this.ticketListService.assignTicket(this.ticketId, submit.assignedToUserId)
      .pipe(
        finalize(() => this.isAssigning.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ticket => {
          this.mergeTicket(ticket);
          this.assignmentSuccess.set(this.localization.t('workflows.assigned'));
        },
        error: error => this.assignmentError.set(this.safeWorkflowError(error))
      });
  }

  changeStatus(status: TicketStatus): void {
    if (!this.ticketId || this.isChangingStatus()) {
      return;
    }

    this.isChangingStatus.set(true);
    this.statusError.set(null);
    this.statusSuccess.set(null);

    this.ticketListService.changeStatus(this.ticketId, status)
      .pipe(
        finalize(() => this.isChangingStatus.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ticket => {
          this.mergeTicket(ticket);
          this.statusSuccess.set(this.localization.t('workflows.statusUpdated'));
        },
        error: error => this.statusError.set(this.safeWorkflowError(error))
      });
  }

  insertSuggestedReply(reply: string): void {
    if (!this.publicComposer) {
      return;
    }

    if (this.publicComposer.hasDraft() &&
      !window.confirm(this.localization.t('ai.replaceDraftConfirm'))) {
      return;
    }

    this.publicComposer.setDraft(reply);
  }

  statusLabel(status: TicketStatus): string {
    return this.localization.enumLabel('status', status);
  }

  priorityLabel(priority: string): string {
    return this.localization.enumLabel('priority', priority);
  }

  categoryLabel(category: string): string {
    return this.localization.enumLabel('category', category);
  }

  private loadTicket(ticketId: string): void {
    this.isLoading.set(true);
    this.state.set('ready');
    this.ticket.set(null);

    this.ticketListService.getTicketById(ticketId)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: ticket => {
          this.ticket.set(ticket);
          this.state.set('ready');
        },
        error: error => {
          this.ticket.set(null);
          this.state.set(this.stateForError(error));
        }
      });
  }

  private loadAgents(): void {
    this.ticketListService.getAgents()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agents => this.agents.set(agents),
        error: () => this.agents.set([])
      });
  }

  private sortMessages(messages: readonly TicketDetailsMessage[]): readonly TicketDetailsMessage[] {
    return [...messages].sort((left, right) =>
      new Date(left.createdAtUtc).getTime() - new Date(right.createdAtUtc).getTime());
  }

  private appendMessage(message: TicketDetailsMessage): void {
    const ticket = this.ticket();
    if (!ticket || ticket.messages.some(existing => existing.id === message.id)) {
      return;
    }

    this.ticket.set({
      ...ticket,
      messages: [...ticket.messages, message]
    });
  }

  private mergeTicket(updated: TicketListItem): void {
    const current = this.ticket();
    if (!current) {
      return;
    }

    this.ticket.set({
      ...current,
      ...updated,
      messages: current.messages
    });
  }

  private stateForError(error: unknown): DetailsState {
    if (error instanceof ApiError) {
      if (error.kind === 'not-found') {
        return 'not-found';
      }

      if (error.kind === 'forbidden') {
        return 'forbidden';
      }
    }

    return 'error';
  }

  private safeComposerError(error: unknown): string {
    if (error instanceof ApiError) {
      switch (error.kind) {
        case 'validation':
          return this.localization.t('messages.checkMessage');
        case 'forbidden':
          return this.localization.t('messages.forbidden');
        case 'not-found':
          return this.localization.t('messages.notFound');
        default:
          return this.localization.t('messages.error');
      }
    }

    return this.localization.t('messages.error');
  }

  private safeWorkflowError(error: unknown): string {
    if (error instanceof ApiError) {
      switch (error.kind) {
        case 'validation':
          return this.localization.t('workflows.validation');
        case 'forbidden':
          return this.localization.t('workflows.forbidden');
        case 'not-found':
          return this.localization.t('workflows.notFound');
        default:
          if (error.status === 409) {
            return this.localization.t('workflows.conflict');
          }

          return this.localization.t('workflows.error');
      }
    }

    return this.localization.t('workflows.error');
  }

  private safeReturnUrl(): string {
    const requested = this.route.snapshot.queryParamMap.get('returnUrl');
    const fallback = this.listRouteForRole(this.role());

    if (!requested || !requested.startsWith('/') || requested.startsWith('//') || requested.includes('://')) {
      return fallback;
    }

    const allowedPrefix = `${fallback}?`;
    if (requested === fallback || requested.startsWith(allowedPrefix)) {
      return requested;
    }

    return fallback;
  }

  private listRouteForRole(role: UserRole | null): string {
    switch (role) {
      case 'Admin':
        return '/admin/tickets';
      case 'Agent':
        return '/agent/tickets';
      case 'Customer':
        return '/customer/tickets';
      default:
        return '/';
    }
  }
}
