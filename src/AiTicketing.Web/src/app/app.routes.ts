import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { roleHomeRedirectGuard } from './core/guards/role-home-redirect.guard';
import { ticketCreateCanDeactivateGuard } from './features/tickets/pages/ticket-create-page/ticket-create-page.guard';

const loadAuthenticatedLayout = () =>
  import('./layouts/authenticated-layout/authenticated-layout.component').then(m => m.AuthenticatedLayoutComponent);

const loadDashboardPage = () =>
  import('./features/dashboard/pages/ticket-dashboard-page/ticket-dashboard-page.component').then(m => m.TicketDashboardPageComponent);

const loadTicketListPage = () =>
  import('./features/tickets/pages/ticket-list-page/ticket-list-page.component').then(m => m.TicketListPageComponent);

const loadTicketDetailsPage = () =>
  import('./features/tickets/pages/ticket-details-page/ticket-details-page.component').then(m => m.TicketDetailsPageComponent);

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard, roleHomeRedirectGuard],
    loadComponent: loadAuthenticatedLayout
  },
  {
    path: '',
    loadComponent: loadAuthenticatedLayout,
    canActivate: [authGuard],
    children: [
      {
        path: 'admin',
        loadComponent: loadDashboardPage,
        canActivate: [roleGuard],
        data: {
          roles: ['Admin'],
          title: 'Admin dashboard',
          description: 'Ticket operations, assignment, agent lookup, and admin reporting will live here.'
        }
      },
      {
        path: 'admin/tickets',
        loadComponent: loadTicketListPage,
        canActivate: [roleGuard],
        data: { roles: ['Admin'], title: 'Tickets' }
      },
      {
        path: 'admin/tickets/:id',
        loadComponent: loadTicketDetailsPage,
        canActivate: [roleGuard],
        data: { roles: ['Admin'] }
      },
      {
        path: 'agent',
        loadComponent: loadDashboardPage,
        canActivate: [roleGuard],
        data: {
          roles: ['Agent'],
          title: 'Agent dashboard',
          description: 'Assigned tickets, conversations, and AI-assisted workflows will live here.'
        }
      },
      {
        path: 'agent/tickets',
        loadComponent: loadTicketListPage,
        canActivate: [roleGuard],
        data: { roles: ['Agent'], title: 'My Tickets' }
      },
      {
        path: 'agent/tickets/:id',
        loadComponent: loadTicketDetailsPage,
        canActivate: [roleGuard],
        data: { roles: ['Agent'] }
      },
      {
        path: 'customer',
        loadComponent: loadDashboardPage,
        canActivate: [roleGuard],
        data: {
          roles: ['Customer'],
          title: 'Customer home',
          description: 'Owned tickets, new requests, and notification entry points will live here.'
        }
      },
      {
        path: 'customer/tickets',
        loadComponent: loadTicketListPage,
        canActivate: [roleGuard],
        data: { roles: ['Customer'], title: 'My Tickets' }
      },
      {
        path: 'customer/tickets/new',
        loadComponent: () => import('./features/tickets/pages/ticket-create-page/ticket-create-page.component').then(m => m.TicketCreatePageComponent),
        canActivate: [roleGuard],
        canDeactivate: [ticketCreateCanDeactivateGuard],
        data: { roles: ['Customer'] }
      },
      {
        path: 'customer/tickets/:id',
        loadComponent: loadTicketDetailsPage,
        canActivate: [roleGuard],
        data: { roles: ['Customer'] }
      }
    ]
  },
  {
    path: 'unauthorized',
    loadComponent: () => import('./features/unauthorized/unauthorized.component').then(m => m.UnauthorizedComponent)
  },
  {
    path: '**',
    loadComponent: () => import('./features/not-found/not-found.component').then(m => m.NotFoundComponent)
  }
];
