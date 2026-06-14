import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page-state">
      <div class="card state-card">
        <h1>Access unavailable</h1>
        <p>Your account does not have access to that area.</p>
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
export class UnauthorizedComponent {}
