import { computed, Injectable, signal } from '@angular/core';
import { CurrentUser } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly currentUserSignal = signal<CurrentUser | null>(null);
  private readonly loadingSignal = signal(true);

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isLoading = this.loadingSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);

  setLoading(isLoading: boolean): void {
    this.loadingSignal.set(isLoading);
  }

  setCurrentUser(user: CurrentUser): void {
    this.currentUserSignal.set(user);
  }

  clearCurrentUser(): void {
    this.currentUserSignal.set(null);
  }
}
