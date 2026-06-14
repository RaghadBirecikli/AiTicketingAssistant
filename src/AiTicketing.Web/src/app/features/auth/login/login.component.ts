import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiError } from '../../../core/api/api-error';
import { AuthService } from '../../../core/auth/auth.service';
import { LanguageSelectorComponent } from '../../../core/localization/language-selector.component';
import { LocalizationService } from '../../../core/localization/localization.service';
import { TranslatePipe } from '../../../core/localization/translate.pipe';
import { homeRouteForRole } from '../../../core/models/role.model';
import { UiIconComponent } from '../../../shared/components/ui-icon/ui-icon.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [LanguageSelectorComponent, ReactiveFormsModule, RouterLink, TranslatePipe, UiIconComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly localization = inject(LocalizationService);
  private readonly router = inject(Router);

  readonly errorMessage = signal<string | null>(null);
  readonly isSubmitting = signal(false);
  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]]
  });
  readonly canSubmit = computed(() => this.form.valid && !this.isSubmitting());

  submit(): void {
    this.errorMessage.set(null);
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.isSubmitting.set(true);
    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        void this.router.navigateByUrl(homeRouteForRole(this.authService.currentRole()));
      },
      error: error => {
        this.isSubmitting.set(false);
        this.errorMessage.set(this.messageForError(error));
      }
    });
  }

  private messageForError(error: unknown): string {
    if (error instanceof ApiError && (error.kind === 'validation' || error.kind === 'unauthenticated')) {
      return this.localization.t('auth.incorrect');
    }

    return this.localization.t('auth.unavailable');
  }
}
