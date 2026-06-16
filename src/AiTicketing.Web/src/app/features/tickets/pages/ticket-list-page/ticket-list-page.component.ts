import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiError } from '../../../../core/api/api-error';
import { AuthService } from '../../../../core/auth/auth.service';
import { LocalizationService } from '../../../../core/localization/localization.service';
import { TranslatePipe } from '../../../../core/localization/translate.pipe';
import { UserRole } from '../../../../core/models/role.model';
import { LocalDateTimePipe } from '../../../../shared/pipes/local-date-time.pipe';
import { TicketListService } from '../../data-access/ticket-list.service';
import { parseTicketListQuery, ticketListQueryToParams } from '../../data-access/ticket-list-query.util';
import {
  agentDisplayName,
} from '../../models/ticket-display.util';
import {
  AgentLookup,
  AssignmentFilterValue,
  pageSizeOptions,
  PagedTicketList,
  SortDirection,
  sortDirections,
  TicketListQuery,
  ticketPriorities,
  TicketPriority,
  ticketSortFields,
  TicketSortField,
  ticketStatuses,
  TicketStatus
} from '../../models/ticket-list.models';

interface TicketFiltersForm {
  search: string;
  status: TicketStatus | '';
  priority: TicketPriority | '';
  assignment: AssignmentFilterValue;
  sortBy: TicketSortField;
  sortDirection: SortDirection;
  pageSize: number;
}

@Component({
  selector: 'app-ticket-list-page',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, TranslatePipe, LocalDateTimePipe],
  templateUrl: './ticket-list-page.component.html',
  styleUrl: './ticket-list-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TicketListPageComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  readonly localization = inject(LocalizationService);
  private readonly ticketListService = inject(TicketListService);
  private readonly destroyRef = inject(DestroyRef);

  readonly statuses = ticketStatuses;
  readonly priorities = ticketPriorities;
  readonly sortFields = ticketSortFields;
  readonly sortDirections = sortDirections;
  readonly pageSizes = pageSizeOptions;

  readonly role = this.authService.currentRole;
  readonly title = signal('tickets.results');
  readonly tickets = signal<PagedTicketList | null>(null);
  readonly agents = signal<readonly AgentLookup[]>([]);
  readonly isLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly currentQuery = signal<TicketListQuery | null>(null);
  readonly isAdmin = computed(() => this.role() === 'Admin');
  readonly currentUserDisplayName = computed(() => {
    const user = this.authService.currentUser();
    return user?.displayName || user?.email || null;
  });
  readonly canGoPrevious = computed(() => this.tickets()?.hasPreviousPage ?? false);
  readonly canGoNext = computed(() => this.tickets()?.hasNextPage ?? false);
  readonly isFiltersOpen = signal(false);
  readonly activeFilterCount = computed(() => {
    const query = this.currentQuery();
    if (!query) {
      return 0;
    }

    return [
      query.search,
      query.status,
      query.priority,
      query.assignedToUserId,
      query.unassigned
    ].filter(Boolean).length;
  });

  readonly filtersForm = this.formBuilder.nonNullable.group({
    search: [''],
    status: ['' as TicketStatus | ''],
    priority: ['' as TicketPriority | ''],
    assignment: ['all' as AssignmentFilterValue],
    sortBy: ['createdAt' as TicketSortField],
    sortDirection: ['desc' as SortDirection],
    pageSize: [20]
  });

  ngOnInit(): void {
    this.title.set(String(this.route.snapshot.data['title'] ?? 'tickets.results'));

    if (this.role() === 'Admin') {
      this.loadAgents();
    }

    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const parsedQuery = parseTicketListQuery(params, this.role());
        this.currentQuery.set(parsedQuery);
        this.applyQueryToForm(parsedQuery);
        this.loadTickets(parsedQuery);
      });
  }

  applyFilters(): void {
    this.navigateWithQuery({ ...this.queryFromForm(), page: 1 });
    this.isFiltersOpen.set(false);
  }

  resetFilters(): void {
    this.navigateWithQuery({
      page: 1,
      pageSize: 20,
      sortBy: 'createdAt',
      sortDirection: 'desc'
    });
    this.isFiltersOpen.set(false);
  }

  toggleFilters(): void {
    this.isFiltersOpen.update(value => !value);
  }

  changeSort(): void {
    this.navigateWithQuery({ ...this.queryFromForm(), page: 1 });
  }

  changePageSize(): void {
    this.navigateWithQuery({ ...this.queryFromForm(), page: 1 });
  }

  previousPage(): void {
    const query = this.currentQuery();
    if (!query || !this.canGoPrevious()) {
      return;
    }

    this.navigateWithQuery({ ...query, page: Math.max(1, query.page - 1) });
  }

  nextPage(): void {
    const query = this.currentQuery();
    const tickets = this.tickets();
    if (!query || !tickets?.hasNextPage) {
      return;
    }

    this.navigateWithQuery({ ...query, page: Math.min(tickets.totalPages, query.page + 1) });
  }

  retry(): void {
    const query = this.currentQuery();
    if (query) {
      this.loadTickets(query);
    }
  }

  ticketDetailsRoute(ticketId: string): string {
    switch (this.role()) {
      case 'Admin':
        return `/admin/tickets/${ticketId}`;
      case 'Agent':
        return `/agent/tickets/${ticketId}`;
      case 'Customer':
        return `/customer/tickets/${ticketId}`;
      default:
        return '/';
    }
  }

  ticketDetailsQueryParams(): { returnUrl: string } {
    return { returnUrl: this.router.url };
  }

  statusLabel(status: TicketStatus): string {
    return this.localization.enumLabel('status', status);
  }

  priorityLabel(priority: TicketPriority): string {
    return this.localization.enumLabel('priority', priority);
  }

  categoryLabel(category: string): string {
    return this.localization.enumLabel('category', category);
  }

  sortFieldLabel(sortField: TicketSortField): string {
    return this.localization.enumLabel('sort', sortField);
  }

  sortDirectionLabel(direction: SortDirection): string {
    return this.localization.enumLabel('direction', direction);
  }

  agentName(agent: AgentLookup): string {
    return agentDisplayName(agent) ?? this.localization.enumLabel('role', 'Agent');
  }

  assignedLabel(assignedToUserId: string | null): string {
    if (!assignedToUserId) {
      return this.localization.t('tickets.unassigned');
    }

    const agent = this.agents().find(item => item.id === assignedToUserId);
    const lookupName = agentDisplayName(agent);
    if (lookupName) {
      return lookupName;
    }

    const currentUser = this.authService.currentUser();
    if (currentUser?.id === assignedToUserId && this.currentUserDisplayName()) {
      return this.currentUserDisplayName() ?? this.localization.t('tickets.assignedFallback');
    }

    return this.localization.t('tickets.assignedFallback');
  }

  customerDirection(ticket: { customerName: string | null; customerEmail: string | null }): 'auto' | 'ltr' {
    return ticket.customerName ? 'auto' : ticket.customerEmail ? 'ltr' : 'auto';
  }

  private loadTickets(query: TicketListQuery): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.ticketListService.getTickets(query, this.role())
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: result => this.tickets.set(result),
        error: error => {
          this.tickets.set(null);
          this.errorMessage.set(this.safeError(error));
        }
      });
  }

  private loadAgents(): void {
    this.ticketListService.getAgents()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: agents => this.agents.set(agents),
        error: () => this.agents.set([])
      });
  }

  private applyQueryToForm(query: TicketListQuery): void {
    this.filtersForm.setValue({
      search: query.search ?? '',
      status: query.status ?? '',
      priority: query.priority ?? '',
      assignment: this.assignmentFromQuery(query),
      sortBy: query.sortBy ?? 'createdAt',
      sortDirection: query.sortDirection ?? 'desc',
      pageSize: query.pageSize
    }, { emitEvent: false });
  }

  private queryFromForm(): TicketListQuery {
    const value: TicketFiltersForm = this.filtersForm.getRawValue();
    const query: TicketListQuery = {
      page: this.currentQuery()?.page ?? 1,
      pageSize: Number(value.pageSize),
      sortBy: value.sortBy,
      sortDirection: value.sortDirection
    };

    const search = value.search.trim();
    if (search) {
      query.search = search;
    }

    if (value.status) {
      query.status = value.status;
    }

    if (value.priority) {
      query.priority = value.priority;
    }

    if (this.role() === 'Admin') {
      if (value.assignment === 'unassigned') {
        query.unassigned = true;
      } else if (value.assignment !== 'all') {
        query.assignedToUserId = value.assignment;
      }
    }

    return query;
  }

  private assignmentFromQuery(query: TicketListQuery): AssignmentFilterValue {
    if (this.role() !== 'Admin') {
      return 'all';
    }

    if (query.unassigned) {
      return 'unassigned';
    }

    return query.assignedToUserId ?? 'all';
  }

  private navigateWithQuery(query: TicketListQuery): void {
    const params = ticketListQueryToParams(query, this.role());
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params
    });
  }

  private safeError(error: unknown): string {
    if (error instanceof ApiError) {
      switch (error.kind) {
        case 'validation':
          return this.localization.t('tickets.validationError');
        case 'unauthenticated':
          return this.localization.t('tickets.unauthenticatedError');
        case 'forbidden':
          return this.localization.t('tickets.forbiddenError');
        case 'rate-limited':
          return this.localization.t('tickets.rateLimitedError');
        default:
          return this.localization.t('tickets.loadError');
      }
    }

    return this.localization.t('tickets.loadError');
  }
}
