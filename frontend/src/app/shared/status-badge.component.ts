import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { EquipmentStatus, ReservationStatus } from '../core/models';

type Tone = 'green' | 'blue' | 'amber' | 'red' | 'gray';

interface StatusStyle {
  label: string;
  tone: Tone;
}

// One colour language across the app: green = free/done, blue = booked, amber = out, red = problem.
export const EQUIPMENT_STATUS: Record<EquipmentStatus, StatusStyle> = {
  Available: { label: 'Available', tone: 'green' },
  Reserved: { label: 'Reserved', tone: 'blue' },
  Borrowed: { label: 'Borrowed', tone: 'amber' },
  UnderRepair: { label: 'Under repair', tone: 'red' },
  Missing: { label: 'Missing', tone: 'red' },
  Retired: { label: 'Retired', tone: 'gray' },
};

export const RESERVATION_STATUS: Record<ReservationStatus, StatusStyle> = {
  Pending: { label: 'Pending approval', tone: 'amber' },
  Approved: { label: 'Approved', tone: 'blue' },
  Borrowed: { label: 'Borrowed', tone: 'amber' },
  Returned: { label: 'Returned', tone: 'green' },
  Rejected: { label: 'Rejected', tone: 'red' },
  Cancelled: { label: 'Cancelled', tone: 'gray' },
};

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="style().tone">{{ style().label }}</span>`,
})
export class StatusBadgeComponent {
  readonly equipment = input<EquipmentStatus>();
  readonly reservation = input<ReservationStatus>();
  readonly overdue = input(false);

  protected readonly style = computed<StatusStyle>(() => {
    if (this.overdue()) return { label: 'Overdue', tone: 'red' };
    const equipment = this.equipment();
    if (equipment) return EQUIPMENT_STATUS[equipment];
    const reservation = this.reservation();
    return reservation ? RESERVATION_STATUS[reservation] : { label: '—', tone: 'gray' };
  });
}
