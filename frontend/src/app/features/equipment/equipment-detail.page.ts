import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EquipmentApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { ToastService } from '../../core/toast.service';
import { Equipment, EquipmentDetail } from '../../core/models';
import { IconComponent } from '../../shared/icon.component';
import { SchedulePipe } from '../../shared/schedule.pipe';
import { StatusBadgeComponent } from '../../shared/status-badge.component';
import { EquipmentFormDialog } from './equipment-form.dialog';

@Component({
  selector: 'app-equipment-detail-page',
  imports: [RouterLink, IconComponent, StatusBadgeComponent, SchedulePipe, EquipmentFormDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './equipment-detail.page.html',
  styles: `
    .layout { display: grid; grid-template-columns: 320px minmax(0, 1fr); gap: 16px; align-items: start; }
    @media (max-width: 960px) { .layout { grid-template-columns: 1fr; } }
    dl { margin: 0; display: grid; gap: 14px; }
    dt { font-size: 12px; color: var(--muted); font-weight: 500; margin-bottom: 2px; }
    dd { margin: 0; }
    .stack { display: grid; gap: 16px; }
  `,
})
export class EquipmentDetailPage {
  private readonly api = inject(EquipmentApi);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly detail = signal<EquipmentDetail | null>(null);
  protected readonly dialogOpen = signal(false);

  protected readonly conditionLabel: Record<string, string> = {
    Good: 'Good',
    NeedsRepair: 'Needs repair',
    Missing: 'Missing',
  };

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.api.get(Number(this.id())).subscribe((d) => this.detail.set(d));
  }

  protected onSaved(saved: Equipment): void {
    this.dialogOpen.set(false);
    this.toast.success(`${saved.assetTag} updated.`);
    this.load();
  }
}
