import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { AuthStorageService } from './auth-storage.service';
import { authInterceptor } from '../interceptors/auth.interceptor';

describe('AuthService', () => {
  let service: AuthService;
  let storage: AuthStorageService;
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AuthService);
    storage = TestBed.inject(AuthStorageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('successful login stores the token through AuthStorageService and calls /api/me', () => {
    let completed = false;

    service.login({ email: 'admin@example.com', password: 'password' }).subscribe(() => {
      completed = true;
    });

    http.expectOne('https://localhost:7194/api/auth/login').flush({
      success: true,
      data: {
        userId: 'admin-id',
        fullName: 'Admin User',
        email: 'admin@example.com',
        role: 'Admin',
        token: 'jwt-token',
        expiresAtUtc: '2026-06-08T10:00:00+00:00'
      },
      message: 'Login successful.',
      errors: null
    });

    const meRequest = http.expectOne('https://localhost:7194/api/me');
    expect(meRequest.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    meRequest.flush({
      success: true,
      data: {
        id: 'admin-id',
        email: 'admin@example.com',
        displayName: 'Admin User',
        roles: ['Admin']
      },
      message: null,
      errors: null
    });

    expect(completed).toBeTrue();
    expect(storage.getToken()).toBe('jwt-token');
    expect(service.currentUser()?.id).toBe('admin-id');
  });

  it('invalid login shows failure without storing a token', () => {
    let failed = false;

    service.login({ email: 'bad@example.com', password: 'wrong' }).subscribe({
      error: () => {
        failed = true;
      }
    });

    http.expectOne('https://localhost:7194/api/auth/login').flush({
      success: false,
      data: null,
      message: 'Invalid login attempt.',
      errors: null
    }, { status: 400, statusText: 'Bad Request' });

    expect(failed).toBeTrue();
    expect(storage.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('existing valid session restores the current user', () => {
    storage.setToken('existing-token');
    let completed = false;

    service.restoreSession().subscribe(() => {
      completed = true;
    });

    const request = http.expectOne('https://localhost:7194/api/me');
    expect(request.request.headers.get('Authorization')).toBe('Bearer existing-token');
    request.flush({
      success: true,
      data: {
        id: 'agent-id',
        email: 'agent@example.com',
        displayName: 'Support Agent',
        roles: ['Agent']
      },
      message: null,
      errors: null
    });

    expect(completed).toBeTrue();
    expect(service.currentUser()?.id).toBe('agent-id');
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('invalid session clears authentication after 401', () => {
    storage.setToken('expired-token');

    service.restoreSession().subscribe();

    http.expectOne('https://localhost:7194/api/me').flush({
      success: false,
      data: null,
      message: 'Authenticated user is no longer available.',
      errors: null
    }, { status: 401, statusText: 'Unauthorized' });

    expect(storage.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('logout clears the session', () => {
    storage.setToken('token');
    service.logout();

    expect(storage.getToken()).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });
});
