import { UserRole } from '../models/role.model';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  email: string;
  role: UserRole;
  token: string;
  expiresAtUtc: string;
}

export interface CurrentUser {
  id: string;
  email: string | null;
  displayName: string | null;
  roles: readonly UserRole[];
}
