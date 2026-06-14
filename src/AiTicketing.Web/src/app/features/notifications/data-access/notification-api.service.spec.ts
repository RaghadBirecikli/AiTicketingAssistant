import { TestBed } from '@angular/core/testing';
import { NotificationApiService } from './notification-api.service';
import { ApiService } from '../../../core/api/api.service';

describe('NotificationApiService', () => {
  let api: jasmine.SpyObj<ApiService>;
  let service: NotificationApiService;

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['get', 'patch']);
    TestBed.configureTestingModule({
      providers: [
        NotificationApiService,
        { provide: ApiService, useValue: api }
      ]
    });

    service = TestBed.inject(NotificationApiService);
  });

  it('uses the exact list endpoint', () => {
    service.getNotifications();

    expect(api.get).toHaveBeenCalledWith('/api/notifications');
  });

  it('uses the exact unread-count endpoint', () => {
    service.getUnreadCount();

    expect(api.get).toHaveBeenCalledWith('/api/notifications/unread-count');
  });

  it('mark-one uses the exact PATCH endpoint', () => {
    service.markAsRead('notification-id');

    expect(api.patch).toHaveBeenCalledWith('/api/notifications/notification-id/read', {});
  });

  it('mark-all uses the exact PATCH endpoint', () => {
    service.markAllAsRead();

    expect(api.patch).toHaveBeenCalledWith('/api/notifications/read-all', {});
  });
});
