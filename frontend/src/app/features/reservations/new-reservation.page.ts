import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, of, switchMap, tap } from 'rxjs';
import { DepartmentApi, EquipmentApi, ReservationApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';
import { Availability } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon.component';
import { SchedulePipe } from '../../shared/schedule.pipe';

interface Period {
  start: Date | null;
  end: Date | null;
  error: string | null;
}

const invalid = (error: string): Period => ({ start: null, end: null, error });

interface CategoryGroup {
  category: string;
  units: Availability[];
  freeCount: number;
}

/** "2026-11-09T08:00" in the browser's local time, as <input type="datetime-local"> expects. */
function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function atHour(daysFromNow: number, hour: number): Date {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  d.setHours(hour, 0, 0, 0);
  return d;
}

@Component({
  selector: 'app-new-reservation-page',
  imports: [FormsModule, RouterLink, IconComponent, SchedulePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './new-reservation.page.html',
  styleUrl: './new-reservation.page.scss',
})
export class NewReservationPage {
  private readonly equipmentApi = inject(EquipmentApi);
  private readonly reservationApi = inject(ReservationApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthService);

  /** Optional ?equipment=<id> to pre-select a unit (from the equipment page). */
  readonly equipment = input<string>();

  // --- Step 1: when ---
  protected readonly start = signal(toLocalInput(atHour(1, 8)));
  protected readonly end = signal(toLocalInput(atHour(1, 12)));

  protected readonly period = computed<Period>(() => {
    const start = new Date(this.start());
    const end = new Date(this.end());
    if (isNaN(start.getTime()) || isNaN(end.getTime())) return invalid('Enter both a pick-up and a return time.');
    if (end <= start) return invalid('The return time must be after the pick-up time.');
    if (end <= new Date()) return invalid('That time has already passed.');
    return { start, end, error: null };
  });

  // --- Step 2: what ---
  protected readonly loading = signal(false);
  protected readonly selected = signal<ReadonlySet<number>>(new Set());
  protected readonly categoryFilter = signal('');
  private readonly refreshTick = signal(0);

  private readonly availability = toSignal(
    toObservable(computed(() => ({ period: this.period(), tick: this.refreshTick() }))).pipe(
      debounceTime(250),
      tap(() => this.loading.set(true)),
      switchMap(({ period: p }) =>
        p.error
          ? of([] as Availability[])
          : this.equipmentApi.availability(p.start!, p.end!).pipe(catchError(() => of([] as Availability[]))),
      ),
      tap((list) => {
        this.loading.set(false);
        this.dropUnavailableSelections(list);
      }),
    ),
    { initialValue: [] as Availability[] },
  );

  /** Availability grouped by category, preserving the API's ordering. */
  protected readonly groups = computed<CategoryGroup[]>(() => {
    const byCategory = new Map<string, Availability[]>();
    for (const a of this.availability()) {
      const list = byCategory.get(a.equipment.category) ?? [];
      list.push(a);
      byCategory.set(a.equipment.category, list);
    }
    return [...byCategory].map(([category, units]) => ({
      category,
      units,
      freeCount: units.filter((u) => u.isAvailable).length,
    }));
  });

  protected readonly visibleGroups = computed(() => {
    const filter = this.categoryFilter();
    return filter ? this.groups().filter((g) => g.category === filter) : this.groups();
  });

  protected readonly selectedUnits = computed(() =>
    this.availability().filter((a) => this.selected().has(a.equipment.id)),
  );

  // --- Step 3: event details ---
  protected eventName = '';
  protected venue = '';
  protected purpose = '';
  protected departmentId: number | null = null;
  protected readonly departments = toSignal(inject(DepartmentApi).list(), { initialValue: [] });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    // Honour ?equipment=<id> once, the first time availability arrives.
    toObservable(this.availability)
      .pipe(takeUntilDestroyed())
      .subscribe((list) => {
        const wanted = Number(this.equipment());
        if (wanted && list.some((a) => a.equipment.id === wanted && a.isAvailable) && this.selected().size === 0) {
          this.selected.set(new Set([wanted]));
        }
      });
  }

  protected toggle(unit: Availability): void {
    if (!unit.isAvailable) return;
    const next = new Set(this.selected());
    if (next.has(unit.equipment.id)) next.delete(unit.equipment.id);
    else next.add(unit.equipment.id);
    this.selected.set(next);
  }

  protected remove(id: number): void {
    const next = new Set(this.selected());
    next.delete(id);
    this.selected.set(next);
  }

  protected submit(): void {
    const period = this.period();
    if (period.error) return this.error.set(period.error);
    if (this.selected().size === 0) return this.error.set('Pick at least one item to borrow.');
    if (!this.eventName.trim()) return this.error.set('Give the event a name so the custodian knows what it is for.');
    if (this.auth.isAdmin() && !this.departmentId) return this.error.set('Choose the department borrowing the items.');

    this.error.set(null);
    this.submitting.set(true);

    this.reservationApi
      .create({
        eventName: this.eventName.trim(),
        venue: this.venue.trim() || null,
        purpose: this.purpose.trim() || null,
        departmentId: this.auth.isAdmin() ? this.departmentId : null,
        startAt: period.start!.toISOString(),
        endAt: period.end!.toISOString(),
        equipmentIds: [...this.selected()],
      })
      .subscribe({
        next: (created) => {
          this.toast.success(
            created.status === 'Pending'
              ? `${created.code} sent to the Property Office for approval.`
              : `${created.code} is booked.`,
          );
          this.router.navigate(['/reservations', created.id]);
        },
        error: (err) => {
          this.submitting.set(false);
          this.error.set(errorMessage(err));
          // Someone may have just taken an item; refresh the list.
          this.refreshTick.update((n) => n + 1);
        },
      });
  }

  /** If the time changes and a picked unit is now booked, un-pick it and say so. */
  private dropUnavailableSelections(list: Availability[]): void {
    const free = new Set(list.filter((a) => a.isAvailable).map((a) => a.equipment.id));
    const kept = [...this.selected()].filter((id) => free.has(id));
    if (kept.length !== this.selected().size) {
      this.selected.set(new Set(kept));
      this.toast.error('Some picked items are not free at the new time and were removed.');
    }
  }
}
