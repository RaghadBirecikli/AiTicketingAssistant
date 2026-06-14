import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { AgentLookup } from '../../models/ticket-list.models';

export interface AssignTicketSubmit {
  assignedToUserId: string;
  assignedToDisplayName: string | null;
}

@Component({
  selector: 'app-ticket-assignment-control',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './ticket-assignment-control.component.html',
  styleUrl: './ticket-assignment-control.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketAssignmentControlComponent implements OnChanges {
  private readonly formBuilder = inject(FormBuilder);
  readonly localization = inject(LocalizationService);

  @Input() agents: readonly AgentLookup[] = [];
  @Input() currentAssignedToUserId: string | null = null;
  @Input() isSubmitting = false;
  @Input() errorMessage: string | null = null;
  @Input() successMessage: string | null = null;
  @Output() readonly assignTicket = new EventEmitter<AssignTicketSubmit>();

  readonly form = this.formBuilder.nonNullable.group({
    assignedToUserId: ['', Validators.required]
  });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['currentAssignedToUserId']) {
      this.form.controls.assignedToUserId.setValue(this.currentAssignedToUserId ?? '', { emitEvent: false });
    }
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    const assignedToUserId = this.form.controls.assignedToUserId.value;
    const agent = this.agents.find(item => item.id === assignedToUserId);
    this.assignTicket.emit({
      assignedToUserId,
      assignedToDisplayName: agent?.displayName ?? agent?.email ?? null
    });
  }
}
