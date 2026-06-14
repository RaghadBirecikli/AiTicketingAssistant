import { CanDeactivateFn } from '@angular/router';
import { TicketCreatePageComponent } from './ticket-create-page.component';

export const ticketCreateCanDeactivateGuard: CanDeactivateFn<TicketCreatePageComponent> = component => component.canDeactivate();
