import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthStorageService } from '../auth/auth-storage.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let http: HttpTestingController;
  let storage: AuthStorageService;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    storage = TestBed.inject(AuthStorageService);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('adds the Bearer token for authenticated requests', () => {
    storage.setToken('token-value');

    httpClient.get('/api/me').subscribe();

    const request = http.expectOne('/api/me');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-value');
    request.flush({});
  });

  it('does not add a token for anonymous requests', () => {
    httpClient.get('/api/me').subscribe();

    const request = http.expectOne('/api/me');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('clears authentication on 401 without issuing another request', () => {
    storage.setToken('expired-token');

    httpClient.get('/api/me').subscribe({ error: () => undefined });

    const request = http.expectOne('/api/me');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(storage.getToken()).toBeNull();
    http.expectNone('/login');
  });
});
