import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiEndpoints } from '../../../core/api/api-endpoints';
import { ApiService } from '../../../core/api/api.service';
import { AdminTicketStats, MyTicketStats } from '../models/dashboard-stats.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiService);

  getAdminStats(): Observable<AdminTicketStats> {
    return this.api.get<AdminTicketStats>(ApiEndpoints.ticketStats);
  }

  getMyStats(): Observable<MyTicketStats> {
    return this.api.get<MyTicketStats>(ApiEndpoints.myTicketStats);
  }
}
