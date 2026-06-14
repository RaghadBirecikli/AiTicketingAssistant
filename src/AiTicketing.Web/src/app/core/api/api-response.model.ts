export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors: readonly string[] | null;
}
