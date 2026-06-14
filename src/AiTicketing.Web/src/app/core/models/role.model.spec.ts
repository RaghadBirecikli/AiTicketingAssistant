import { homeRouteForRole, primaryRole } from './role.model';

describe('role helpers', () => {
  it('applies Admin precedence for users with multiple roles', () => {
    expect(primaryRole(['Customer', 'Admin', 'Agent'])).toBe('Admin');
  });

  it('maps roles to home routes', () => {
    expect(homeRouteForRole('Admin')).toBe('/admin');
    expect(homeRouteForRole('Agent')).toBe('/agent');
    expect(homeRouteForRole('Customer')).toBe('/customer');
  });
});
