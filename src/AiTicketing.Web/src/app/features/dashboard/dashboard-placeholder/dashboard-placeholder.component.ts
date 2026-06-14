import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-dashboard-placeholder',
  standalone: true,
  templateUrl: './dashboard-placeholder.component.html',
  styleUrl: './dashboard-placeholder.component.scss'
})
export class DashboardPlaceholderComponent {
  constructor(readonly route: ActivatedRoute) {}
}
