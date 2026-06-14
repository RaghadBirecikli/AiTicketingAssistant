import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AuthStorageService } from '../auth/auth-storage.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const storage = inject(AuthStorageService);
  const authService = inject(AuthService);
  const token = storage.getToken();
  const authenticatedRequest = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authenticatedRequest).pipe(
    catchError(error => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        authService.handleUnauthenticatedResponse();
      }

      return throwError(() => error);
    })
  );
};
