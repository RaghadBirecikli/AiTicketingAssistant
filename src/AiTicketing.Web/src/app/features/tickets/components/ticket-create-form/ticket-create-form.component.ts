import { ChangeDetectionStrategy, Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

export interface TicketCreateSubmit {
  title: string;
  description: string;
}

function notWhitespace(value: string): boolean {
  return value.trim().length > 0;
}

const notWhitespaceValidator: ValidatorFn = (control: AbstractControl<string>): ValidationErrors | null =>
  notWhitespace(String(control.value ?? '')) ? null : { whitespace: true };

@Component({
  selector: 'app-ticket-create-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './ticket-create-form.component.html',
  styleUrl: './ticket-create-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketCreateFormComponent {
  private readonly formBuilder = inject(FormBuilder);

  @Input() isSubmitting = false;
  @Input() errorMessage: string | null = null;
  @Input() successMessage: string | null = null;

  @Output() readonly createTicket = new EventEmitter<TicketCreateSubmit>();
  @Output() readonly cancelCreate = new EventEmitter<void>();

  readonly titleMaxLength = 200;
  readonly descriptionMaxLength = 4000;

  readonly form = this.formBuilder.nonNullable.group({
    title: ['', [
      Validators.required,
      Validators.maxLength(this.titleMaxLength),
      notWhitespaceValidator
    ]],
    description: ['', [
      Validators.required,
      Validators.maxLength(this.descriptionMaxLength),
      notWhitespaceValidator
    ]]
  });

  get titleRemaining(): number {
    return this.titleMaxLength - this.form.controls.title.value.length;
  }

  get descriptionRemaining(): number {
    return this.descriptionMaxLength - this.form.controls.description.value.length;
  }

  get dirty(): boolean {
    return this.form.dirty;
  }

  submit(): void {
    this.form.markAllAsTouched();

    if (this.form.invalid || this.isSubmitting) {
      this.focusFirstInvalidControl();
      return;
    }

    this.createTicket.emit({
      title: this.form.controls.title.value.trim(),
      description: this.form.controls.description.value.trim()
    });
  }

  cancel(): void {
    this.cancelCreate.emit();
  }

  private focusFirstInvalidControl(): void {
    const firstInvalid = document.querySelector<HTMLElement>('[aria-invalid="true"]');
    firstInvalid?.focus();
  }
}
