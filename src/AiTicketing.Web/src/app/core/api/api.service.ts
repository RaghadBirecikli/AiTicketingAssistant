import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError, kindFromStatus } from './api-error';
import { ApiResponse } from './api-response.model';
import { LocalizationService } from '../localization/localization.service';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly localization = inject(LocalizationService);
  private readonly baseUrl = environment.apiBaseUrl.replace(/\/$/, '');

  get<T>(endpoint: string, query?: Record<string, string | number | boolean | null | undefined>): Observable<T> {
    return this.http.get<ApiResponse<T>>(this.url(endpoint), { params: this.toParams(query) }).pipe(
      map(response => this.unwrap(response)),
      catchError(error => this.normalizeError(error))
    );
  }

  post<TRequest extends object, TResponse>(endpoint: string, body: TRequest): Observable<TResponse> {
    return this.http.post<ApiResponse<TResponse>>(this.url(endpoint), body).pipe(
      map(response => this.unwrap(response)),
      catchError(error => this.normalizeError(error))
    );
  }

  patch<TRequest extends object, TResponse>(endpoint: string, body: TRequest): Observable<TResponse> {
    return this.http.patch<ApiResponse<TResponse>>(this.url(endpoint), body).pipe(
      map(response => this.unwrap(response)),
      catchError(error => this.normalizeError(error))
    );
  }

  private url(endpoint: string): string {
    return `${this.baseUrl}${endpoint}`;
  }

  private toParams(query?: Record<string, string | number | boolean | null | undefined>): HttpParams {
    let params = new HttpParams();
    if (!query) {
      return params;
    }

    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return params;
  }

  private unwrap<T>(response: ApiResponse<T>): T {
    if (response.success && response.data !== null) {
      return response.data;
    }

    throw new ApiError(0, 'unknown', response.message ?? 'The request did not complete successfully.', response.errors ?? []);
  }

  private normalizeError(error: unknown): Observable<never> {
    if (error instanceof ApiError) {
      return throwError(() => error);
    }

    if (error instanceof HttpErrorResponse) {
      const envelope = this.tryReadEnvelope(error.error);
      const message = this.safeMessage(error.status, envelope?.message);
      return throwError(() => new ApiError(
        error.status,
        kindFromStatus(error.status),
        message,
        envelope?.errors ?? [],
        this.retryAfterSeconds(error)
      ));
    }

    return throwError(() => new ApiError(0, 'unknown', 'The request could not be completed.'));
  }

  private tryReadEnvelope(errorBody: unknown): ApiResponse<unknown> | null {
    if (typeof errorBody !== 'object' || errorBody === null) {
      return null;
    }

    const candidate = errorBody as Partial<ApiResponse<unknown>>;
    return {
      success: Boolean(candidate.success),
      data: candidate.data ?? null,
      message: typeof candidate.message === 'string' ? candidate.message : null,
      errors: Array.isArray(candidate.errors) ? candidate.errors.filter((item): item is string => typeof item === 'string') : null
    };
  }

  private safeMessage(status: number, backendMessage: string | null | undefined): string {
    if (backendMessage && status >= 400 && status < 500) {
      return backendMessage;
    }

    switch (status) {
      case 401:
        return this.localization.t('errors.unauthorized');
      case 403:
        return this.localization.t('errors.forbidden');
      case 404:
        return this.localization.t('errors.notFound');
      case 429:
        return this.localization.t('errors.rateLimited');
      case 503:
        return this.localization.t('errors.unavailable');
      default:
        return this.localization.t('errors.generic');
    }
  }

  private retryAfterSeconds(error: HttpErrorResponse): number | null {
    const retryAfter = error.headers?.get('Retry-After');
    if (!retryAfter) {
      return null;
    }

    const seconds = Number(retryAfter);
    if (Number.isFinite(seconds) && seconds >= 0) {
      return seconds;
    }

    const date = Date.parse(retryAfter);
    if (Number.isNaN(date)) {
      return null;
    }

    return Math.max(0, Math.ceil((date - Date.now()) / 1000));
  }
}
