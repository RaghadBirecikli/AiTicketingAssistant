import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs/operators';
import { ApiError } from '../../../../core/api/api-error';
import { AuthService } from '../../../../core/auth/auth.service';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { UserRole } from '../../../../core/models/role.model';
import { DashboardQuickActionComponent } from '../../components/dashboard-quick-action/dashboard-quick-action.component';
import { DashboardStatCardComponent } from '../../components/dashboard-stat-card/dashboard-stat-card.component';
import { DashboardService } from '../../data-access/dashboard.service';
import { DashboardStats, hasAdminStatsFields } from '../../models/dashboard-stats.models';

interface StatCardViewModel {
  label: string;
  value: number;
  explanation: string;
  link?: string;
  queryParams?: Record<string, string | number | boolean>;
}

interface QuickActionViewModel {
  label: string;
  description: string;
  link: string;
  queryParams?: Record<string, string | number | boolean>;
}

@Component({
  selector: 'app-ticket-dashboard-page',
  standalone: true,
  imports: [DashboardQuickActionComponent, DashboardStatCardComponent, TranslatePipe],
  templateUrl: './ticket-dashboard-page.component.html',
  styleUrl: './ticket-dashboard-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketDashboardPageComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly dashboardService = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);
  readonly localization = inject(LocalizationService);

  readonly role = this.authService.currentRole;
  readonly stats = signal<DashboardStats | null>(null);
  readonly isLoading = signal(false);
  readonly hasLoadedOnce = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly refreshMessage = signal<string | null>(null);

  readonly title = computed(() => {
    switch (this.role()) {
      case 'Admin':
        return this.localization.t('dashboard.adminTitle');
      case 'Agent':
        return this.localization.t('dashboard.agentTitle');
      case 'Customer':
        return this.localization.t('dashboard.customerTitle');
      default:
        return this.localization.t('navigation.dashboard');
    }
  });

  readonly description = computed(() => {
    switch (this.role()) {
      case 'Admin':
        return this.localization.t('dashboard.adminDescription');
      case 'Agent':
        return this.localization.t('dashboard.agentDescription');
      case 'Customer':
        return this.localization.t('dashboard.customerDescription');
      default:
        return this.localization.t('dashboard.fallbackDescription');
    }
  });

  readonly statCards = computed(() => {
    const stats = this.stats();
    const role = this.role();
    if (!stats || !role) {
      return [];
    }

    return this.cardsForRole(role, stats);
  });
  readonly primaryStatCards = computed(() => this.statCards().slice(0, 4));
  readonly secondaryStatCards = computed(() => this.statCards().slice(4));

  readonly quickActions = computed(() => {
    const role = this.role();
    if (!role) {
      return [];
    }

    return this.actionsForRole(role);
  });

  ngOnInit(): void {
    this.loadStats();
  }

  refresh(): void {
    this.loadStats(true);
  }

  private loadStats(isRefresh = false): void {
    if (this.isLoading()) {
      return;
    }

    const role = this.role();
    if (!role) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.refreshMessage.set(null);

    const request = role === 'Admin'
      ? this.dashboardService.getAdminStats()
      : this.dashboardService.getMyStats();

    request
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: stats => {
          this.stats.set(stats);
          this.hasLoadedOnce.set(true);
          this.refreshMessage.set(isRefresh ? this.localization.t('dashboard.refreshed') : null);
        },
        error: error => {
          this.hasLoadedOnce.set(true);
          this.errorMessage.set(this.safeDashboardError(error));
        }
      });
  }

  private cardsForRole(role: UserRole, stats: DashboardStats): readonly StatCardViewModel[] {
    const baseListRoute = this.ticketListRoute(role);
    const ownerLabel = role === 'Agent'
      ? this.localization.t('dashboard.assignedTickets')
      : role === 'Customer'
        ? this.localization.t('dashboard.ownedTickets')
        : this.localization.t('dashboard.totalTickets');

    const cards: StatCardViewModel[] = [
      {
        label: ownerLabel,
        value: stats.total,
        explanation: role === 'Admin' ? this.localization.t('dashboard.allActive') : this.localization.t('dashboard.visibleToYou'),
        link: baseListRoute
      },
      {
        label: this.localization.enumLabel('status', 'Open'),
        value: stats.open,
        explanation: this.localization.t('dashboard.openExplanation'),
        link: baseListRoute,
        queryParams: { status: 'Open' }
      },
      {
        label: this.localization.enumLabel('status', 'InProgress'),
        value: stats.inProgress,
        explanation: this.localization.t('dashboard.inProgressExplanation'),
        link: baseListRoute,
        queryParams: { status: 'InProgress' }
      },
      {
        label: this.localization.enumLabel('priority', 'Urgent'),
        value: stats.urgentPriority,
        explanation: this.localization.t('dashboard.urgentExplanation'),
        link: baseListRoute,
        queryParams: { priority: 'Urgent' }
      },
      {
        label: this.localization.enumLabel('status', 'Resolved'),
        value: stats.resolved,
        explanation: this.localization.t('dashboard.resolvedExplanation'),
        link: baseListRoute,
        queryParams: { status: 'Resolved' }
      },
      {
        label: this.localization.enumLabel('status', 'Closed'),
        value: stats.closed,
        explanation: this.localization.t('dashboard.closedExplanation'),
        link: baseListRoute,
        queryParams: { status: 'Closed' }
      }
    ];

    if (hasAdminStatsFields(stats)) {
      cards.push({
        label: this.localization.t('dashboard.unassigned'),
        value: stats.unassigned,
        explanation: this.localization.t('dashboard.unassignedExplanation'),
        link: '/admin/tickets',
        queryParams: { unassigned: true }
      });
    }

    return cards;
  }

  private actionsForRole(role: UserRole): readonly QuickActionViewModel[] {
    switch (role) {
      case 'Admin':
        return [
          { label: this.localization.t('dashboard.viewAllTickets'), description: this.localization.t('dashboard.viewAllTicketsDescription'), link: '/admin/tickets' },
          { label: this.localization.t('dashboard.viewOpenTickets'), description: this.localization.t('dashboard.viewOpenTicketsDescription'), link: '/admin/tickets', queryParams: { status: 'Open' } },
          { label: this.localization.t('dashboard.viewUnassignedTickets'), description: this.localization.t('dashboard.viewUnassignedTicketsDescription'), link: '/admin/tickets', queryParams: { unassigned: true } },
          { label: this.localization.t('dashboard.viewUrgentTickets'), description: this.localization.t('dashboard.viewUrgentTicketsDescription'), link: '/admin/tickets', queryParams: { priority: 'Urgent' } }
        ];
      case 'Agent':
        return [
          { label: this.localization.t('dashboard.viewMyTickets'), description: this.localization.t('dashboard.viewMyTicketsDescription'), link: '/agent/tickets' },
          { label: this.localization.t('dashboard.viewOpenTickets'), description: this.localization.t('dashboard.viewOpenMyTicketsDescription'), link: '/agent/tickets', queryParams: { status: 'Open' } },
          { label: this.localization.t('dashboard.viewUrgentTickets'), description: this.localization.t('dashboard.viewUrgentMyTicketsDescription'), link: '/agent/tickets', queryParams: { priority: 'Urgent' } }
        ];
      case 'Customer':
        return [
          { label: this.localization.t('dashboard.viewMyTickets'), description: this.localization.t('dashboard.viewMyTicketHistoryDescription'), link: '/customer/tickets' },
          { label: this.localization.t('navigation.createTicket'), description: this.localization.t('dashboard.createTicketDescription'), link: '/customer/tickets/new' },
          { label: this.localization.t('dashboard.viewOpenTickets'), description: this.localization.t('dashboard.viewOpenCustomerTicketsDescription'), link: '/customer/tickets', queryParams: { status: 'Open' } }
        ];
    }
  }

  private ticketListRoute(role: UserRole): string {
    switch (role) {
      case 'Admin':
        return '/admin/tickets';
      case 'Agent':
        return '/agent/tickets';
      case 'Customer':
        return '/customer/tickets';
    }
  }

  private safeDashboardError(error: unknown): string {
    if (error instanceof ApiError && error.kind === 'forbidden') {
      return this.localization.t('dashboard.forbidden');
    }

    return this.localization.t('dashboard.error');
  }
}
