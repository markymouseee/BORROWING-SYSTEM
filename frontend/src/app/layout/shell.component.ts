import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, forkJoin, map, startWith, switchMap } from 'rxjs';
import { ReservationApi } from '../core/api';
import { AuthService } from '../core/auth.service';
import { ConfirmService } from '../core/confirm.service';
import { ThemeMode, ThemeService } from '../core/theme.service';
import { errorMessage } from '../core/errors';
import { ToastService } from '../core/toast.service';
import { IconComponent, IconName } from '../shared/icon.component';
import { ModalComponent } from '../shared/modal.component';

type BadgeKey = 'pending' | 'overdue';

interface NavItem {
  label: string;
  path: string;
  icon: IconName;
  exact?: boolean;
  adminOnly?: boolean;
  badges?: BadgeKey[];
}

interface NavSection {
  title: string;
  items: NavItem[];
}

const NAVIGATION: NavSection[] = [
  {
    title: 'Overview',
    items: [{ label: 'Dashboard', path: '/', icon: 'dashboard', exact: true }],
  },
  {
    title: 'Borrowing',
    items: [
      { label: 'Reservations', path: '/reservations', icon: 'calendar', badges: ['overdue', 'pending'] },
      { label: 'Equipment', path: '/equipment', icon: 'box' },
    ],
  },
  {
    title: 'Administration',
    items: [
      { label: 'Departments', path: '/admin/departments', icon: 'building', adminOnly: true },
      { label: 'Users', path: '/admin/users', icon: 'users', adminOnly: true },
    ],
  },
];

const COLLAPSED_KEY = 'lcc-borrowing.sidebar-collapsed';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, IconComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  protected readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly reservations = inject(ReservationApi);
  private readonly router = inject(Router);

  protected readonly theme = inject(ThemeService);
  protected readonly themeOptions: { mode: ThemeMode; icon: IconName; label: string }[] = [
    { mode: 'light', icon: 'sun', label: 'Light' },
    { mode: 'dark', icon: 'moon', label: 'Dark' },
    { mode: 'system', icon: 'monitor', label: 'System' },
  ];

  /** Breadcrumb shown in the top bar, e.g. "Borrowing / Reservations". */
  protected readonly crumb = signal<{ section: string; page: string }>({ section: '', page: '' });

  protected readonly collapsed = signal(localStorage.getItem(COLLAPSED_KEY) === '1');
  protected readonly mobileOpen = signal(false);
  protected readonly indicator = signal<{ top: number; height: number } | null>(null);
  private readonly nav = viewChild<ElementRef<HTMLElement>>('nav');
  protected readonly accountMenuOpen = signal(false);

  protected readonly passwordOpen = signal(false);
  protected readonly passwordError = signal<string | null>(null);
  protected readonly savingPassword = signal(false);
  protected passwordForm = { current: '', next: '', confirm: '' };

  /** Counts shown next to "Reservations"; refreshed after every navigation. */
  protected readonly badges = signal<Record<BadgeKey, number>>({ pending: 0, overdue: 0 });

  protected readonly sections = computed(() => {
    const isAdmin = this.auth.isAdmin();
    return NAVIGATION.map((s) => ({ ...s, items: s.items.filter((i) => !i.adminOnly || isAdmin) })).filter(
      (s) => s.items.length > 0,
    );
  });

  protected readonly initials = computed(() =>
    (this.auth.user()?.fullName ?? '')
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0].toUpperCase())
      .join(''),
  );

  protected readonly roleLabel = computed(() =>
    this.auth.isAdmin() ? 'Property custodian' : (this.auth.user()?.departmentName ?? 'Faculty'),
  );

  constructor() {
    const navigated$ = this.router.events.pipe(filter((e) => e instanceof NavigationEnd));

    navigated$.pipe(startWith(null), takeUntilDestroyed()).subscribe(() => {
      this.mobileOpen.set(false);
      this.accountMenuOpen.set(false);
      this.crumb.set(this.currentCrumb());
    });

    navigated$
      .pipe(
        startWith(null),
        switchMap(() =>
          forkJoin({
            pending: this.reservations.list({ status: 'Pending', pageSize: 5 }),
            overdue: this.reservations.list({ overdueOnly: true, pageSize: 5 }),
          }),
        ),
        map(({ pending, overdue }) => ({ pending: pending.totalCount, overdue: overdue.totalCount })),
        takeUntilDestroyed(),
      )
      .subscribe((counts) => this.badges.set(counts));
  }

  protected toggleCollapsed(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem(COLLAPSED_KEY, next ? '1' : '0');
    setTimeout(() => this.moveIndicator(), 220); // after the width transition
  }

  /** routerLinkActive tells us exactly when a link becomes (in)active. */
  protected onActiveChange(isActive: boolean, link: HTMLElement): void {
    if (isActive) requestAnimationFrame(() => this.moveIndicator(link));
    else requestAnimationFrame(() => this.moveIndicator());
  }

  /** Slides the highlight pill to the given link, or to whichever link is currently active. */
  private moveIndicator(link?: HTMLElement): void {
    const nav = this.nav()?.nativeElement;
    const active = link ?? nav?.querySelector<HTMLElement>('.nav-link.active') ?? null;
    if (!nav || !active) return this.indicator.set(null);

    // Measure against the nav itself; offsetTop would be skewed by the animated sections.
    const top = active.getBoundingClientRect().top - nav.getBoundingClientRect().top + nav.scrollTop;
    this.indicator.set({ top, height: active.offsetHeight });
  }

  /** Close the profile menu when clicking anywhere else. */
  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!(event.target as HTMLElement).closest('.profile')) this.accountMenuOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.accountMenuOpen.set(false);
  }

  private currentCrumb(): { section: string; page: string } {
    let route = this.router.routerState.snapshot.root;
    while (route.firstChild) route = route.firstChild;

    const url = this.router.url.split('?')[0];
    const section = NAVIGATION.find((s) =>
      s.items.some((i) => (i.path === '/' ? url === '/' : url.startsWith(i.path))),
    );
    return { section: section?.title ?? '', page: route.title ?? '' };
  }

  protected async signOut(): Promise<void> {
    this.accountMenuOpen.set(false);
    const ok = await this.confirm.ask({
      title: 'Sign out?',
      message: 'You will need your email and password to sign back in.',
      confirmText: 'Sign out',
      tone: 'danger',
    });
    if (ok) this.auth.logout();
  }

  protected openPasswordDialog(): void {
    this.accountMenuOpen.set(false);
    this.passwordForm = { current: '', next: '', confirm: '' };
    this.passwordError.set(null);
    this.passwordOpen.set(true);
  }

  protected changePassword(): void {
    const { current, next, confirm } = this.passwordForm;
    if (next.length < 8) return this.passwordError.set('The new password must be at least 8 characters.');
    if (next !== confirm) return this.passwordError.set('The new passwords do not match.');

    this.savingPassword.set(true);
    this.auth.changePassword(current, next).subscribe({
      next: () => {
        this.savingPassword.set(false);
        this.passwordOpen.set(false);
        this.toast.success('Your password has been changed.');
      },
      error: (err) => {
        this.savingPassword.set(false);
        this.passwordError.set(errorMessage(err));
      },
    });
  }
}
