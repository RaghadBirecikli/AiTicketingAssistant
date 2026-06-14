import { ChangeDetectionStrategy, Component, DestroyRef, ViewChild, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiError } from '../../../../core/api/api-error';
import { AuthService } from '../../../../core/auth/auth.service';
import { UserRole } from '../../../../core/models/role.model';
import {
  TicketCreateFormComponent,
  TicketCreateSubmit
} from '../../components/ticket-create-form/ticket-create-form.component';
import { TicketListService } from '../../data-access/ticket-list.service';

@Component({
  selector: 'app-ticket-create-page',
  standalone: true,
  imports: [TicketCreateFormComponent],
  templateUrl: './ticket-create-page.component.html',
  styleUrl: './ticket-create-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketCreatePageComponent {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly ticketListService = inject(TicketListService);
  private readonly destroyRef = inject(DestroyRef);

  readonly role = this.authService.currentRole;
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  private submittedSuccessfully = false;

  @ViewChild(TicketCreateFormComponent) private readonly createForm?: TicketCreateFormComponent;

  createTicket(request: TicketCreateSubmit): void {
    if (this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.ticketListService.createTicket(request.title, request.description)
      .pipe(
        finalize(() => this.isSubmitting.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: response => {
          this.submittedSuccessfully = true;
          this.successMessage.set('Ticket created.');
          void this.router.navigateByUrl(this.detailsRouteForRole(this.role(), response.ticket.id), { replaceUrl: true });
        },
        error: error => this.errorMessage.set(this.safeCreateError(error))
      });
  }

  cancel(): void {
    void this.router.navigateByUrl(this.listRouteForRole(this.role()));
  }

  canDeactivate(): boolean {
    if (this.submittedSuccessfully || this.isSubmitting() || !this.createForm?.dirty) {
      return true;
    }

    return window.confirm('You have unsaved ticket details. Leave this page?');
  }

  private safeCreateError(error: unknown): string {
    if (error instanceof ApiError) {
      switch (error.kind) {
        case 'validation':
          return 'Please check the ticket details and try again.';
        case 'forbidden':
          return 'You are not allowed to create a ticket.';
        default:
          return error.status === 409
            ? 'The ticket could not be created because of a conflicting change.'
            : 'The ticket could not be created. Please try again.';
      }
    }

    return 'The ticket could not be created. Please try again.';
  }

  private detailsRouteForRole(role: UserRole | null, ticketId: string): string {
    switch (role) {
      case 'Admin':
        return `/admin/tickets/${ticketId}`;
      case 'Agent':
        return `/agent/tickets/${ticketId}`;
      case 'Customer':
        return `/customer/tickets/${ticketId}`;
      default:
        return `/customer/tickets/${ticketId}`;
    }
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
