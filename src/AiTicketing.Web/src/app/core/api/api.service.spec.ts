import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { ApiError } from './api-error';

describe('ApiService', () => {
  let service: ApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(ApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('unwraps the backend API response envelope', () => {
    let value: { ok: boolean } | undefined;

    service.get<{ ok: boolean }>('/api/test').subscribe(result => {
      value = result;
    });

    http.expectOne('https://localhost:7194/api/test').flush({
      success: true,
      data: { ok: true },
      message: null,
      errors: null
    });

    expect(value).toEqual({ ok: true });
  });

  it('normalizes 400 validation errors safely', () => {
    let apiError: ApiError | undefined;

    service.get('/api/test').subscribe({
      error: error => {
        apiError = error;
      }
    });

    http.expectOne('https://localhost:7194/api/test').flush({
      success: false,
      data: null,
      message: 'Validation failed.',
      errors: ['Email is required.']
    }, { status: 400, statusText: 'Bad Request' });

    expect(apiError?.kind).toBe('validation');
    expect(apiError?.message).toBe('Validation failed.');
    expect(apiError?.errors).toEqual(['Email is required.']);
  });

  it('normalizes rate limiting and unavailable errors without raw exception text', () => {
    const errors: ApiError[] = [];

    service.get('/api/rate-limited').subscribe({ error: error => errors.push(error) });
    http.expectOne('https://localhost:7194/api/rate-limited').flush({}, { status: 429, statusText: 'Too Many Requests' });

    service.get('/api/unavailable').subscribe({ error: error => errors.push(error) });
    http.expectOne('https://localhost:7194/api/unavailable').flush({}, { status: 503, statusText: 'Unavailable' });

    expect(errors.map(error => error.kind)).toEqual(['rate-limited', 'unavailable']);
    expect(errors[1].message).toBe('The service is temporarily unavailable.');
  });

  it('reads Retry-After seconds from rate-limited responses', () => {
    let apiError: ApiError | undefined;

    service.get('/api/rate-limited').subscribe({
      error: error => {
        apiError = error;
      }
    });

    http.expectOne('https://localhost:7194/api/rate-limited').flush({}, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '30' }
    });

    expect(apiError?.kind).toBe('rate-limited');
    expect(apiError?.retryAfterSeconds).toBe(30);
  });
});
