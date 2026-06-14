import { Injectable } from '@angular/core';

const tokenKey = 'aiticketing.auth.token';

@Injectable({ providedIn: 'root' })
export class AuthStorageService {
  getToken(): string | null {
    return sessionStorage.getItem(tokenKey);
  }

  setToken(token: string): void {
    sessionStorage.setItem(tokenKey, token);
  }

  clearToken(): void {
    sessionStorage.removeItem(tokenKey);
  }
}
