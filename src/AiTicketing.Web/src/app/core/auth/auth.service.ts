import { computed, inject, Injectable } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap } from 'rxjs';
import { ApiEndpoints } from '../api/api-endpoints';
import { ApiService } from '../api/api.service';
import { UserRole, isUserRole, primaryRole } from '../models/role.model';
import { AuthResponse, CurrentUser, LoginRequest } from './auth.models';
import { AuthStateService } from './auth-state.service';
import { AuthStorageService } from './auth-storage.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly storage = inject(AuthStorageService);
  private readonly state = inject(AuthStateService);

  readonly currentUser = this.state.currentUser;
  readonly isAuthenticated = this.state.isAuthenticated;
  readonly isLoading = this.state.isLoading;
  readonly currentRole = computed(() => primaryRole(this.currentUser()?.roles ?? []));

  restoreSession(): Observable<void> {
    const token = this.storage.getToken();
    if (!token) {
      this.state.clearCurrentUser();
      this.state.setLoading(false);
      return of(void 0);
    }

    this.state.setLoading(true);
    return this.loadCurrentUser().pipe(
      map(() => void 0),
      catchError(() => {
        this.clearSession();
        return of(void 0);
      }),
      tap(() => this.state.setLoading(false))
    );
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    this.state.setLoading(true);
    return this.api.post<LoginRequest, AuthResponse>(ApiEndpoints.auth.login, request).pipe(
      tap(response => this.storage.setToken(response.token)),
      switchMap(() => this.loadCurrentUser()),
      tap(() => this.state.setLoading(false)),
      catchError(error => {
        this.clearSession();
        throw error;
      })
    );
  }

  logout(): void {
    this.clearSession();
  }

  hasRole(role: UserRole): boolean {
    return this.currentUser()?.roles.includes(role) ?? false;
  }

  hasAnyRole(roles: readonly UserRole[]): boolean {
    return roles.some(role => this.hasRole(role));
  }

  handleUnauthenticatedResponse(): void {
    this.clearSession();
  }

  private loadCurrentUser(): Observable<CurrentUser> {
    return this.api.get<CurrentUser>(ApiEndpoints.me).pipe(
      map(user => ({
        ...user,
        roles: user.roles.filter(isUserRole)
      })),
      tap(user => this.state.setCurrentUser(user))
    );
  }

  private clearSession(): void {
    this.storage.clearToken();
    this.state.clearCurrentUser();
    this.state.setLoading(false);
  }
}
