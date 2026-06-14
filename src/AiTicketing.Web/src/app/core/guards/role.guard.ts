import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { UserRole } from '../models/role.model';

export const roleGuard: CanActivateFn = route => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const roles = (route.data['roles'] ?? []) as readonly UserRole[];

  if (roles.length === 0 || authService.hasAnyRole(roles)) {
    return true;
  }

  return router.createUrlTree(['/unauthorized']);
};
