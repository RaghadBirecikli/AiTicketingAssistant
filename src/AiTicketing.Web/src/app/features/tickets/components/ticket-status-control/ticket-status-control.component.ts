import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { TicketStatus, ticketStatuses } from '../../models/ticket-list.models';

@Component({
  selector: 'app-ticket-status-control',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './ticket-status-control.component.html',
  styleUrl: './ticket-status-control.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketStatusControlComponent implements OnChanges {
  private readonly formBuilder = inject(FormBuilder);
  private readonly localization = inject(LocalizationService);

  @Input({ required: true }) currentStatus: TicketStatus = 'Open';
  @Input() isSubmitting = false;
  @Input() errorMessage: string | null = null;
  @Input() successMessage: string | null = null;
  @Output() readonly changeStatus = new EventEmitter<TicketStatus>();

  readonly statuses = ticketStatuses;
  readonly confirmationPending = signal(false);
  readonly form = this.formBuilder.nonNullable.group({
    status: ['Open' as TicketStatus, Validators.required]
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentStatus']) {
      this.form.controls.status.setValue(this.currentStatus, { emitEvent: false });
      this.confirmationPending.set(false);
    }
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    const status = this.form.controls.status.value;
    if (status === 'Closed' && !this.confirmationPending()) {
      this.confirmationPending.set(true);
      return;
    }

    this.changeStatus.emit(status);
  }

  cancelConfirmation(): void {
    this.confirmationPending.set(false);
  }

  statusLabel(status: TicketStatus): string {
    return this.localization.enumLabel('status', status);
  }
}
