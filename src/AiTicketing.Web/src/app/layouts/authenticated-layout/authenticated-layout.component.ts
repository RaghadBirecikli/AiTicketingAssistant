import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageSelectorComponent } from '../../core/localization/language-selector.component';
import { LocalizationService } from '../../core/localization/localization.service';
import { TranslatePipe } from '../../core/localization/translate.pipe';
import { UserRole } from '../../core/models/role.model';
import { ThemePreference, ThemeService } from '../../core/theme/theme.service';
import { NotificationBellComponent } from '../../features/notifications/components/notification-bell/notification-bell.component';
import { NotificationStateService } from '../../features/notifications/services/notification-state.service';
import { UiIconComponent, UiIconName } from '../../shared/components/ui-icon/ui-icon.component';

interface NavItem {
  labelKey: string;
  route: string;
  icon: UiIconName;
}

const navByRole: Record<UserRole, readonly NavItem[]> = {
  Admin: [
    { labelKey: 'navigation.dashboard', route: '/admin', icon: 'dashboard' },
    { labelKey: 'navigation.tickets', route: '/admin/tickets', icon: 'ticket' }
  ],
  Agent: [
    { labelKey: 'navigation.myDashboard', route: '/agent', icon: 'dashboard' },
    { labelKey: 'navigation.myTickets', route: '/agent/tickets', icon: 'ticket' }
  ],
  Customer: [
    { labelKey: 'navigation.home', route: '/customer', icon: 'dashboard' },
    { labelKey: 'navigation.myTickets', route: '/customer/tickets', icon: 'ticket' },
    { labelKey: 'navigation.createTicket', route: '/customer/tickets/new', icon: 'plus' }
  ]
};

@Component({
  selector: 'app-authenticated-layout',
  standalone: true,
  imports: [LanguageSelectorComponent, NotificationBellComponent, RouterLink, RouterLinkActive, RouterOutlet, TranslatePipe, UiIconComponent],
  templateUrl: './authenticated-layout.component.html',
  styleUrl: './authenticated-layout.component.scss'
})
export class AuthenticatedLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly notifications = inject(NotificationStateService);
  private readonly router = inject(Router);
  readonly localization = inject(LocalizationService);
  readonly theme = inject(ThemeService);

  readonly user = this.authService.currentUser;
  readonly role = this.authService.currentRole;
  readonly displayName = computed(() => this.user()?.displayName ?? this.user()?.email ?? this.localization.t('auth.signedInUser'));
  readonly email = computed(() => this.user()?.email ?? '');
  readonly initials = computed(() => this.displayName()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map(part => part[0]?.toUpperCase())
    .join('') || 'U');
  readonly navigation = computed(() => {
    const role = this.role();
    return role ? navByRole[role] : [];
  });
  readonly currentUrl = signal(this.router.url);
  readonly pageTitle = computed(() => this.currentPageTitle());
  readonly isSidebarCollapsed = signal(this.readBooleanPreference('ai-ticketing-sidebar-collapsed'));
  readonly isMobileNavOpen = signal(false);
  readonly isUserMenuOpen = signal(false);
  readonly isThemeMenuOpen = signal(false);
  readonly themeButtonLabel = computed(() => {
    const preference = this.theme.preference();
    if (preference === 'system') {
      return this.localization.t('theme.triggerSystem', {
        theme: this.localization.t(`theme.${this.theme.effectiveTheme()}`)
      });
    }

    return this.localization.t('theme.triggerExplicit', { theme: this.localization.t(`theme.${preference}`) });
  });
  readonly themeIcon = computed<UiIconName>(() => this.theme.effectiveTheme() === 'dark' ? 'moon' : 'sun');

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => {
        this.currentUrl.set(event.urlAfterRedirects);
        this.closeMobileNav();
        this.closeThemeMenu();
        this.closeUserMenu();
      });
  }

  setTheme(preference: ThemePreference): void {
    this.theme.setPreference(preference);
    this.closeThemeMenu();
  }

  toggleThemeMenu(): void {
    this.isThemeMenuOpen.update(value => !value);
  }

  closeThemeMenu(): void {
    this.isThemeMenuOpen.set(false);
  }

  themePreferenceLabel(preference: ThemePreference): string {
    switch (preference) {
      case 'system':
        return `${this.localization.t('theme.system')} (${this.localization.t('theme.currently', {
          theme: this.localization.t(`theme.${this.theme.effectiveTheme()}`)
        })})`;
      case 'light':
        return this.localization.t('theme.light');
      case 'dark':
        return this.localization.t('theme.dark');
    }
  }

  isThemeSelected(preference: ThemePreference): boolean {
    return this.theme.preference() === preference;
  }

  onThemeMenuKeydown(event: KeyboardEvent): void {
    if ((event.key === 'Enter' || event.key === ' ') && !this.isThemeMenuOpen()) {
      event.preventDefault();
      this.isThemeMenuOpen.set(true);
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeThemeMenu();
      return;
    }

    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const items = Array.from(document.querySelectorAll<HTMLButtonElement>('.theme-menu [role="menuitemradio"]'));
      if (items.length === 0) {
        return;
      }

      const currentIndex = items.findIndex(item => item === document.activeElement);
      const direction = event.key === 'ArrowDown' ? 1 : -1;
      const nextIndex = currentIndex === -1
        ? 0
        : (currentIndex + direction + items.length) % items.length;
      items[nextIndex]?.focus();
    }
  }

  toggleSidebar(): void {
    this.isSidebarCollapsed.update(value => {
      const next = !value;
      localStorage.setItem('ai-ticketing-sidebar-collapsed', String(next));
      return next;
    });
  }

  toggleMobileNav(): void {
    this.isMobileNavOpen.update(value => !value);
  }

  closeMobileNav(): void {
    this.isMobileNavOpen.set(false);
  }

  toggleUserMenu(): void {
    this.isUserMenuOpen.update(value => !value);
  }

  closeUserMenu(): void {
    this.isUserMenuOpen.set(false);
  }

  logout(): void {
    this.authService.logout();
    this.notifications.clear();
    void this.router.navigateByUrl('/login');
  }

  private currentPageTitle(): string {
    const url = this.currentUrl();
    if (url.includes('/tickets/new')) {
      return this.localization.t('navigation.createTicket');
    }

    if (/\/tickets\/[0-9a-f-]+/i.test(url)) {
      return this.localization.t('ticketDetails.title');
    }

    if (url.includes('/tickets')) {
      return this.role() === 'Agent' || this.role() === 'Customer'
        ? this.localization.t('navigation.myTickets')
        : this.localization.t('navigation.tickets');
    }

    if (url.includes('/unauthorized')) {
      return this.localization.t('navigation.unauthorized');
    }

    return this.localization.t('navigation.dashboard');
  }

  private readBooleanPreference(key: string): boolean {
    return localStorage.getItem(key) === 'true';
  }

  @HostListener('document:keydown.escape')
  onDocumentEscape(): void {
    this.closeThemeMenu();
    this.closeUserMenu();
  }
}
