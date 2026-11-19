import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, switchMap, tap } from 'rxjs';
import { ReservationApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { ReservationQuery, ReservationStatus } from '../../core/models';
import { IconComponent } from '../../shared/icon.component';
import { PaginationComponent } from '../../shared/pagination.component';
import { SchedulePipe } from '../../shared/schedule.pipe';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

type TabKey = 'all' | ReservationStatus | 'Overdue';

interface Tab {
  key: TabKey;
  label: string;
  query: Pick<ReservationQuery, 'status' | 'overdueOnly'>;
}

const TABS: Tab[] = [
  { key: 'all', label: 'All', query: {} },
  { key: 'Pending', label: 'Pending', query: { status: 'Pending' } },
  { key: 'Approved', label: 'Approved', query: { status: 'Approved' } },
  { key: 'Borrowed', label: 'Borrowed', query: { status: 'Borrowed' } },
  { key: 'Overdue', label: 'Overdue', query: { overdueOnly: true } },
  { key: 'Returned', label: 'Returned', query: { status: 'Returned' } },
  { key: 'Rejected', label: 'Rejected', query: { status: 'Rejected' } },
  { key: 'Cancelled', label: 'Cancelled', query: { status: 'Cancelled' } },
];

const PAGE_SIZE = 15;

@Component({
  selector: 'app-reservation-list-page',
  imports: [FormsModule, RouterLink, IconComponent, StatusBadgeComponent, SchedulePipe, PaginationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reservation-list.page.html',
})
export class ReservationListPage {
  private readonly api = inject(ReservationApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  /** Pre-selected from the dashboard via ?tab=… */
  readonly tab = input<TabKey>();

  protected readonly tabs = TABS;
  protected readonly activeTab = signal<TabKey>('all');
  protected readonly search = signal('');
  protected readonly page = signal(1);
  protected readonly loading = signal(true);

  private readonly query = computed<ReservationQuery>(() => ({
    ...TABS.find((t) => t.key === this.activeTab())!.query,
    search: this.search().trim(),
    page: this.page(),
    pageSize: PAGE_SIZE,
  }));

  protected readonly result = toSignal(
    toObservable(this.query).pipe(
      debounceTime(200),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
      tap(() => this.loading.set(true)),
      switchMap((q) => this.api.list(q)),
      tap(() => this.loading.set(false)),
    ),
  );

  ngOnInit(): void {
    const requested = this.tab();
    if (requested && TABS.some((t) => t.key === requested)) this.activeTab.set(requested);
  }

  protected selectTab(key: TabKey): void {
    this.activeTab.set(key);
    this.page.set(1);
  }

  protected onSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  protected open(id: number): void {
    this.router.navigate(['/reservations', id]);
  }
}
