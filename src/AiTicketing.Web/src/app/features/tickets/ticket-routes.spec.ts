import { routes } from '../../app.routes';

describe('ticket routes', () => {
  it('protects Admin, Agent, and Customer dashboard routes with matching role metadata', () => {
    const shell = routes.find(route => Array.isArray(route.children));
    const children = shell?.children ?? [];

    expect(children.find(route => route.path === 'admin')?.data?.['roles']).toEqual(['Admin']);
    expect(children.find(route => route.path === 'agent')?.data?.['roles']).toEqual(['Agent']);
    expect(children.find(route => route.path === 'customer')?.data?.['roles']).toEqual(['Customer']);
  });

  it('protects Admin, Agent, and Customer ticket routes with matching role metadata', () => {
    const shell = routes.find(route => Array.isArray(route.children));
    const children = shell?.children ?? [];

    expect(children.find(route => route.path === 'admin/tickets')?.data?.['roles']).toEqual(['Admin']);
    expect(children.find(route => route.path === 'agent/tickets')?.data?.['roles']).toEqual(['Agent']);
    expect(children.find(route => route.path === 'customer/tickets')?.data?.['roles']).toEqual(['Customer']);
  });

  it('protects the Customer ticket creation route and keeps it before ticket details', () => {
    const shell = routes.find(route => Array.isArray(route.children));
    const children = shell?.children ?? [];
    const createIndex = children.findIndex(route => route.path === 'customer/tickets/new');
    const detailsIndex = children.findIndex(route => route.path === 'customer/tickets/:id');

    expect(children[createIndex]?.data?.['roles']).toEqual(['Customer']);
    expect(children[createIndex]?.canDeactivate?.length).toBe(1);
    expect(createIndex).toBeGreaterThan(-1);
    expect(createIndex).toBeLessThan(detailsIndex);
  });

  it('protects Admin, Agent, and Customer ticket details routes with matching role metadata', () => {
    const shell = routes.find(route => Array.isArray(route.children));
    const children = shell?.children ?? [];

    expect(children.find(route => route.path === 'admin/tickets/:id')?.data?.['roles']).toEqual(['Admin']);
    expect(children.find(route => route.path === 'agent/tickets/:id')?.data?.['roles']).toEqual(['Agent']);
    expect(children.find(route => route.path === 'customer/tickets/:id')?.data?.['roles']).toEqual(['Customer']);
  });
});
