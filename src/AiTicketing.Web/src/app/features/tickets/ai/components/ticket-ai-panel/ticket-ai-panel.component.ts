import { ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, Input, Output, WritableSignal, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiError } from '../../../../../core/api/api-error';
import { LocalizationService } from '../../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../../core/localization/translate.pipe';
import { UserRole } from '../../../../../core/models/role.model';
import { ClipboardService } from '../../../../../shared/services/clipboard.service';
import { UiIconComponent } from '../../../../../shared/components/ui-icon/ui-icon.component';
import { TicketAiService } from '../../data-access/ticket-ai.service';
import { TicketTriageSuggestionResponse } from '../../models/ticket-ai.models';

type OperationState = 'idle' | 'loading' | 'success' | 'error';
type AiTab = 'reply' | 'summary' | 'triage';

const instructionMaxLength = 500;

@Component({
  selector: 'app-ticket-ai-panel',
  standalone: true,
  imports: [ReactiveFormsModule, UiIconComponent, TranslatePipe],
  templateUrl: './ticket-ai-panel.component.html',
  styleUrl: './ticket-ai-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketAiPanelComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly ticketAiService = inject(TicketAiService);
  private readonly clipboard = inject(ClipboardService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly localization = inject(LocalizationService);

  @Input({ required: true }) ticketId = '';
  @Input() role: UserRole | null = null;
  @Output() readonly insertReply = new EventEmitter<string>();

  readonly instructionMaxLength = instructionMaxLength;
  readonly activeTab = signal<AiTab>('reply');
  readonly replyState = signal<OperationState>('idle');
  readonly summaryState = signal<OperationState>('idle');
  readonly triageState = signal<OperationState>('idle');
  readonly replyError = signal<string | null>(null);
  readonly summaryError = signal<string | null>(null);
  readonly triageError = signal<string | null>(null);
  readonly replyCopyMessage = signal<string | null>(null);
  readonly summaryCopyMessage = signal<string | null>(null);
  readonly triageCopyMessage = signal<string | null>(null);
  readonly suggestedReply = signal<string | null>(null);
  readonly summary = signal<string | null>(null);
  readonly triage = signal<TicketTriageSuggestionResponse | null>(null);

  readonly replyForm = this.formBuilder.nonNullable.group({
    instruction: ['', [Validators.maxLength(instructionMaxLength)]]
  });

  readonly summaryForm = this.formBuilder.nonNullable.group({
    includeInternalNotes: [false]
  });

  readonly triageForm = this.formBuilder.nonNullable.group({
    instruction: ['', [Validators.maxLength(instructionMaxLength)]]
  });

  get canIncludeInternalNotes(): boolean {
    return this.role === 'Admin';
  }

  setActiveTab(tab: AiTab): void {
    this.activeTab.set(tab);
  }

  suggestReply(): void {
    this.activeTab.set('reply');
    this.replyForm.markAllAsTouched();
    if (!this.ticketId || this.replyForm.invalid || this.replyState() === 'loading') {
      return;
    }

    this.replyState.set('loading');
    this.replyError.set(null);
    this.replyCopyMessage.set(null);
    this.ticketAiService.suggestReply(this.ticketId, this.optionalInstruction(this.replyForm.controls.instruction))
      .pipe(
        finalize(() => {
          if (this.replyState() === 'loading') {
            this.replyState.set(this.suggestedReply() ? 'success' : 'idle');
          }
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: response => {
          this.suggestedReply.set(response.suggestedReply);
          this.replyState.set('success');
        },
        error: error => {
          this.replyError.set(this.safeAiError(error));
          this.replyState.set('error');
        }
      });
  }

  summarize(): void {
    this.activeTab.set('summary');
    if (!this.ticketId || this.summaryState() === 'loading') {
      return;
    }

    const includeInternalNotes = this.canIncludeInternalNotes
      ? this.summaryForm.controls.includeInternalNotes.value
      : false;

    this.summaryState.set('loading');
    this.summaryError.set(null);
    this.summaryCopyMessage.set(null);
    this.ticketAiService.summarizeTicket(this.ticketId, includeInternalNotes)
      .pipe(
        finalize(() => {
          if (this.summaryState() === 'loading') {
            this.summaryState.set(this.summary() ? 'success' : 'idle');
          }
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: response => {
          this.summary.set(response.summary);
          this.summaryState.set('success');
        },
        error: error => {
          this.summaryError.set(this.safeAiError(error));
          this.summaryState.set('error');
        }
      });
  }

  suggestTriage(): void {
    this.activeTab.set('triage');
    this.triageForm.markAllAsTouched();
    if (!this.ticketId || this.triageForm.invalid || this.triageState() === 'loading') {
      return;
    }

    this.triageState.set('loading');
    this.triageError.set(null);
    this.triageCopyMessage.set(null);
    this.ticketAiService.suggestTriage(this.ticketId, this.optionalInstruction(this.triageForm.controls.instruction))
      .pipe(
        finalize(() => {
          if (this.triageState() === 'loading') {
            this.triageState.set(this.triage() ? 'success' : 'idle');
          }
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: response => {
          this.triage.set(response);
          this.triageState.set('success');
        },
        error: error => {
          this.triageError.set(this.safeAiError(error));
          this.triageState.set('error');
        }
      });
  }

  clearReply(): void {
    this.suggestedReply.set(null);
    this.replyError.set(null);
    this.replyCopyMessage.set(null);
    this.replyState.set('idle');
  }

  clearSummary(): void {
    this.summary.set(null);
    this.summaryError.set(null);
    this.summaryCopyMessage.set(null);
    this.summaryState.set('idle');
  }

  clearTriage(): void {
    this.triage.set(null);
    this.triageError.set(null);
    this.triageCopyMessage.set(null);
    this.triageState.set('idle');
  }

  insertSuggestedReply(): void {
    const reply = this.suggestedReply();
    if (reply) {
      this.insertReply.emit(reply);
    }
  }

  copyReply(): void {
    this.copy(this.suggestedReply(), this.replyCopyMessage);
  }

  copySummary(): void {
    this.copy(this.summary(), this.summaryCopyMessage);
  }

  copyTriage(): void {
    const triage = this.triage();
    if (!triage) {
      return;
    }

    this.copy([
      `${this.localization.t('ai.currentPriority')}: ${this.localization.enumLabel('priority', triage.currentPriority)}`,
      `${this.localization.t('ai.suggestedPriority')}: ${this.localization.enumLabel('priority', triage.suggestedPriority)}`,
      `${this.localization.t('ai.suggestedCategory')}: ${this.localization.enumLabel('category', triage.suggestedCategory)}`,
      `${this.localization.t('ai.escalationRecommended')}: ${triage.escalationRecommended ? this.localization.t('ai.yes') : this.localization.t('ai.no')}`,
      triage.escalationReason ? `${this.localization.t('ai.escalationReason')}: ${triage.escalationReason}` : null,
      `${this.localization.t('ai.rationale')}: ${triage.rationale}`
    ].filter((line): line is string => line !== null).join('\n'), this.triageCopyMessage);
  }

  currentPriorityLabel(priority: string): string {
    return this.localization.enumLabel('priority', priority);
  }

  suggestedPriorityLabel(priority: string): string {
    return this.localization.enumLabel('priority', priority);
  }

  suggestedCategoryLabel(category: string): string {
    return this.localization.enumLabel('category', category);
  }

  private optionalInstruction(control: AbstractControl<string>): string | null {
    const value = control.value.trim();
    return value.length > 0 ? value : null;
  }

  private copy(value: string | null, message: WritableSignal<string | null>): void {
    if (!value) {
      return;
    }

    message.set(null);
    void this.clipboard.copyText(value)
      .then(() => message.set(this.localization.t('common.copied')))
      .catch(() => message.set(this.localization.t('ai.copyError')));
  }

  private safeAiError(error: unknown): string {
    if (error instanceof ApiError) {
      switch (error.kind) {
        case 'validation':
          return this.localization.t('ai.requestInvalid');
        case 'forbidden':
          return this.localization.t('ai.forbidden');
        case 'not-found':
          return this.localization.t('ai.notFound');
        case 'rate-limited':
          return error.retryAfterSeconds
            ? this.localization.t('ai.rateLimited', { seconds: error.retryAfterSeconds })
            : this.localization.t('ai.rateLimitedNoSeconds');
        case 'unavailable':
          return this.localization.t('ai.unavailable');
        default:
          return this.localization.t('ai.error');
      }
    }

    return this.localization.t('ai.error');
  }
}
