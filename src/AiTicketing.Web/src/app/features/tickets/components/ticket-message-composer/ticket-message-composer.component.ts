import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';

export type MessageComposerMode = 'public' | 'internal';

export interface MessageComposerSubmit {
  body: string;
  mode: MessageComposerMode;
}

export const messageBodyMaxLength = 4000;

@Component({
  selector: 'app-ticket-message-composer',
  standalone: true,
  imports: [ReactiveFormsModule, TranslatePipe],
  templateUrl: './ticket-message-composer.component.html',
  styleUrl: './ticket-message-composer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketMessageComposerComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly localization = inject(LocalizationService);

  @Input({ required: true }) mode: MessageComposerMode = 'public';
  @Input() isSubmitting = false;
  @Input() errorMessage: string | null = null;
  @Input() successMessage: string | null = null;
  @Output() readonly sendMessage = new EventEmitter<MessageComposerSubmit>();

  readonly maxLength = messageBodyMaxLength;
  readonly form = this.formBuilder.nonNullable.group({
    body: ['', [Validators.required, Validators.maxLength(messageBodyMaxLength), nonWhitespaceValidator]]
  });
  readonly bodyControl = this.form.controls.body;

  get remainingCharacters(): number {
    return messageBodyMaxLength - this.bodyControl.value.length;
  }

  get title(): string {
    return this.mode === 'internal' ? this.localization.t('messages.internalNote') : this.localization.t('messages.publicReply');
  }

  get description(): string {
    return this.mode === 'internal'
      ? this.localization.t('messages.internalDescription')
      : this.localization.t('messages.publicDescription');
  }

  get buttonText(): string {
    if (this.isSubmitting) {
      return this.mode === 'internal' ? this.localization.t('messages.adding') : this.localization.t('messages.sending');
    }

    return this.mode === 'internal' ? this.localization.t('messages.addInternalNote') : this.localization.t('messages.sendMessage');
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.isSubmitting) {
      return;
    }

    this.sendMessage.emit({
      body: this.bodyControl.value.trim(),
      mode: this.mode
    });
  }

  clear(): void {
    this.form.reset();
  }

  hasDraft(): boolean {
    return this.bodyControl.value.trim().length > 0;
  }

  setDraft(body: string): void {
    this.bodyControl.setValue(body);
    this.bodyControl.markAsDirty();
    this.bodyControl.markAsTouched();
  }
}

function nonWhitespaceValidator(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim().length > 0 ? null : { whitespace: true };
}
