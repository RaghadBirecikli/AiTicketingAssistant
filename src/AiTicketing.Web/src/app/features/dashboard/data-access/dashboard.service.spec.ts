import { TestBed } from '@angular/core/testing';
import { DashboardService } from './dashboard.service';
import { ApiService } from '../../../core/api/api.service';

describe('DashboardService', () => {
  let api: jasmine.SpyObj<ApiService>;
  let service: DashboardService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get']);
    TestBed.configureTestingModule({
      providers: [
        DashboardService,
        { provide: ApiService, useValue: api }
      ]
    });

    service = TestBed.inject(DashboardService);
  });

  it('calls the exact Admin stats endpoint', () => {
    service.getAdminStats();

    expect(api.get).toHaveBeenCalledWith('/api/tickets/stats');
  });

  it('calls the exact current-user stats endpoint', () => {
    service.getMyStats();

    expect(api.get).toHaveBeenCalledWith('/api/tickets/my-stats');
  });
});
