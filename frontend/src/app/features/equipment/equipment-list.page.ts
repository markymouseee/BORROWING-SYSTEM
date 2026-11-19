import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { EquipmentApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { Equipment, EquipmentStatus } from '../../core/models';
import { IconComponent } from '../../shared/icon.component';
import { PaginationComponent, paginate } from '../../shared/pagination.component';
import { SchedulePipe } from '../../shared/schedule.pipe';
import { EQUIPMENT_STATUS, StatusBadgeComponent } from '../../shared/status-badge.component';
import { EquipmentFormDialog } from './equipment-form.dialog';

@Component({
  selector: 'app-equipment-list-page',
  imports: [FormsModule, RouterLink, IconComponent, StatusBadgeComponent, SchedulePipe, EquipmentFormDialog, PaginationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './equipment-list.page.html',
})
export class EquipmentListPage {
  private readonly api = inject(EquipmentApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthService);

  /** Pre-selected from the dashboard via ?status=… */
  readonly status = input<EquipmentStatus | undefined>();

  // The whole inventory is a few hundred rows at most, so it is loaded once and filtered in memory.
  protected readonly all = signal<Equipment[] | null>(null);
  protected readonly categories = signal<string[]>([]);
  protected readonly search = signal('');
  protected readonly category = signal('');
  protected readonly statusFilter = signal<EquipmentStatus | ''>('');

  protected readonly page = signal(1);
  protected readonly pageSize = 10;

  protected readonly dialogOpen = signal(false);
  protected readonly editing = signal<Equipment | null>(null);

  protected readonly statusOptions = Object.entries(EQUIPMENT_STATUS).map(([value, s]) => ({
    value: value as EquipmentStatus,
    label: s.label,
  }));

  protected readonly filtered = computed(() => {
    const list = this.all() ?? [];
    const term = this.search().trim().toLowerCase();
    const category = this.category();
    const status = this.statusFilter();

    return list.filter(
      (e) =>
        (!category || e.category === category) &&
        (!status || e.status === status) &&
        (!term || `${e.assetTag} ${e.name} ${e.category}`.toLowerCase().includes(term)),
    );
  });

  protected readonly pageRows = computed(() => paginate(this.filtered(), this.page(), this.pageSize));

  constructor() {
    // Any change to the filters starts again from page 1.
    effect(() => {
      this.search();
      this.category();
      this.statusFilter();
      untracked(() => this.page.set(1));
    });
  }

  ngOnInit(): void {
    this.statusFilter.set(this.status() ?? '');
    this.load();
  }

  protected load(): void {
    this.api.list({ includeRetired: this.auth.isAdmin() }).subscribe((list) => this.all.set(list));
    this.api.categories().subscribe((c) => this.categories.set(c));
  }

  protected open(e: Equipment): void {
    this.router.navigate(['/equipment', e.id]);
  }

  protected add(): void {
    this.editing.set(null);
    this.dialogOpen.set(true);
  }

  protected edit(e: Equipment, event: Event): void {
    event.stopPropagation();
    this.editing.set(e);
    this.dialogOpen.set(true);
  }

  protected onSaved(saved: Equipment): void {
    this.dialogOpen.set(false);
    this.toast.success(`${saved.assetTag} saved.`);
    this.load();
  }

  protected clearFilters(): void {
    this.search.set('');
    this.category.set('');
    this.statusFilter.set('');
  }
}
