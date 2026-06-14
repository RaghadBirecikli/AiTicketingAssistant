import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { CurrentUser } from '../auth/auth.models';
import { authGuard } from './auth.guard';
import { roleGuard } from './role.guard';
import { roleHomeRedirectGuard } from './role-home-redirect.guard';
import { UserRole } from '../models/role.model';

class AuthServiceStub {
  currentUser = signal<CurrentUser | null>(null);
  isLoading = signal(false);
  isAuthenticated = signal(false);
  currentRole = signal<UserRole | null>(null);

  hasAnyRole(roles: readonly UserRole[]): boolean {
    return roles.some(role => this.currentUser()?.roles.includes(role));
  }
}

describe('route guards', () => {
  let auth: AuthServiceStub;

  beforeEach(() => {
    auth = new AuthServiceStub();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    });
  });

  it('redirects anonymous users to login', () => {
    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/admin' } as RouterStateSnapshot));

    expect(result instanceof UrlTree).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Fadmin');
  });

  it('allows authenticated users through protected routes', () => {
    auth.isAuthenticated.set(true);

    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, { url: '/admin' } as RouterStateSnapshot));

    expect(result).toBeTrue();
  });

  it('allows matching roles and redirects nonmatching roles to unauthorized', () => {
    auth.currentUser.set({
      id: 'agent-id',
      email: 'agent@example.com',
      displayName: 'Agent',
      roles: ['Agent']
    });

    const allowedRoute = { data: { roles: ['Agent'] } } as unknown as ActivatedRouteSnapshot;
    const deniedRoute = { data: { roles: ['Admin'] } } as unknown as ActivatedRouteSnapshot;

    const allowed = TestBed.runInInjectionContext(() => roleGuard(allowedRoute, {} as RouterStateSnapshot));
    const denied = TestBed.runInInjectionContext(() => roleGuard(deniedRoute, {} as RouterStateSnapshot));

    expect(allowed).toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(denied as UrlTree)).toBe('/unauthorized');
  });

  it('redirects home to the primary role route', () => {
    auth.currentRole.set('Customer');

    const result = TestBed.runInInjectionContext(() => roleHomeRedirectGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot));

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/customer');
  });
});
