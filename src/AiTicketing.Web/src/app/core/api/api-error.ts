export type ApiErrorKind = 'validation' | 'unauthenticated' | 'forbidden' | 'not-found' | 'rate-limited' | 'unavailable' | 'unknown';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly kind: ApiErrorKind,
    message: string,
    readonly errors: readonly string[] = [],
    readonly retryAfterSeconds: number | null = null
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

export function kindFromStatus(status: number): ApiErrorKind {
  switch (status) {
    case 400:
      return 'validation';
    case 401:
      return 'unauthenticated';
    case 403:
      return 'forbidden';
    case 404:
      return 'not-found';
    case 429:
      return 'rate-limited';
    case 503:
      return 'unavailable';
    default:
      return 'unknown';
  }
}
