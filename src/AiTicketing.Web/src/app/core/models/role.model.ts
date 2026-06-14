export type UserRole = 'Admin' | 'Agent' | 'Customer';

export const rolePrecedence: readonly UserRole[] = ['Admin', 'Agent', 'Customer'];

export function isUserRole(value: string): value is UserRole {
  return value === 'Admin' || value === 'Agent' || value === 'Customer';
}

export function primaryRole(roles: readonly string[]): UserRole | null {
  for (const role of rolePrecedence) {
    if (roles.includes(role)) {
      return role;
    }
  }

  return null;
}

export function homeRouteForRole(role: UserRole | null): string {
  switch (role) {
    case 'Admin':
      return '/admin';
    case 'Agent':
      return '/agent';
    case 'Customer':
      return '/customer';
    default:
      return '/login';
  }
}
