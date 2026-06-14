import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page-state">
      <div class="card state-card">
        <h1>Page not found</h1>
        <p>The page you requested does not exist.</p>
        <a class="button" routerLink="/">Go to dashboard</a>
      </div>
    </section>
  `,
  styles: [`
    .state-card { display: grid; gap: 1rem; padding: 1.5rem; }
    h1, p { margin: 0; }
    a { text-decoration: none; }
  `]
})
export class NotFoundComponent {}
