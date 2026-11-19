import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { DashboardApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { EquipmentStatus, ScheduleEntry } from '../../core/models';
import { CountUpDirective } from '../../shared/count-up.directive';
import { IconComponent, IconName } from '../../shared/icon.component';
import { SchedulePipe } from '../../shared/schedule.pipe';

interface Stat {
  label: string;
  value: number;
  tone: 'green' | 'blue' | 'amber' | 'red' | 'neutral';
  icon: IconName;
  link: string;
  query: Record<string, string>;
}

interface Panel {
  title: string;
  empty: string;
  entries: ScheduleEntry[];
  time: 'start' | 'end';
  tone?: 'red';
}

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, IconComponent, SchedulePipe, CountUpDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage {
  protected readonly auth = inject(AuthService);
  protected readonly data = toSignal(inject(DashboardApi).get());

  protected readonly greeting = (() => {
    const hour = new Date().getHours();
    return hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
  })();

  protected readonly today = new Intl.DateTimeFormat('en-PH', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date());

  protected readonly stats = computed<Stat[]>(() => {
    const d = this.data();
    if (!d) return [];
    const count = (s: EquipmentStatus) => d.equipmentByStatus[s] ?? 0;

    const stats: Stat[] = [
      { label: 'Available now', value: count('Available'), tone: 'green', icon: 'check', link: '/equipment', query: { status: 'Available' } },
      { label: 'Reserved · next 24h', value: count('Reserved'), tone: 'blue', icon: 'calendar', link: '/equipment', query: { status: 'Reserved' } },
      { label: 'Borrowed', value: count('Borrowed'), tone: 'amber', icon: 'send', link: '/equipment', query: { status: 'Borrowed' } },
      {
        label: 'Under repair / missing',
        value: count('UnderRepair') + count('Missing'),
        tone: 'red',
        icon: 'alert',
        link: '/equipment',
        query: { status: 'UnderRepair' },
      },
      {
        label: this.auth.isAdmin() ? 'Awaiting approval' : 'My pending requests',
        value: d.pendingApprovals,
        tone: 'neutral',
        icon: 'inbox',
        link: '/reservations',
        query: { tab: 'Pending' },
      },
    ];
    return stats;
  });

  /** Inventory split by status, as segments of one horizontal bar. */
  protected readonly meter = computed(() => {
    const d = this.data();
    if (!d || d.totalEquipment === 0) return [];
    const parts: { label: string; tone: string; value: number }[] = [
      { label: 'Available', tone: 'green', value: d.equipmentByStatus.Available ?? 0 },
      { label: 'Reserved', tone: 'blue', value: d.equipmentByStatus.Reserved ?? 0 },
      { label: 'Borrowed', tone: 'amber', value: d.equipmentByStatus.Borrowed ?? 0 },
      {
        label: 'Repair / missing',
        tone: 'red',
        value: (d.equipmentByStatus.UnderRepair ?? 0) + (d.equipmentByStatus.Missing ?? 0),
      },
    ];
    return parts
      .filter((p) => p.value > 0)
      .map((p) => ({ ...p, percent: (p.value / d.totalEquipment) * 100 }));
  });

  protected readonly panels = computed<Panel[]>(() => {
    const d = this.data();
    if (!d) return [];
    const mine = !this.auth.isAdmin();

    return [
      {
        title: mine ? 'My upcoming pick-ups' : 'To release · next 48 hours',
        empty: 'Nothing scheduled for pick-up.',
        entries: d.forRelease,
        time: 'start',
      },
      {
        title: mine ? 'Due back from me' : 'Due back · next 48 hours',
        empty: 'Nothing due back soon.',
        entries: d.dueForReturn,
        time: 'end',
      },
      {
        title: 'Overdue',
        empty: 'No overdue equipment.',
        entries: d.overdue,
        time: 'end',
        tone: 'red',
      },
    ];
  });

  protected relative(iso: string): string {
    const diffMinutes = Math.round((new Date(iso).getTime() - Date.now()) / 60000);
    const abs = Math.abs(diffMinutes);
    const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
    if (abs < 60) return rtf.format(diffMinutes, 'minute');
    if (abs < 60 * 24) return rtf.format(Math.round(diffMinutes / 60), 'hour');
    return rtf.format(Math.round(diffMinutes / (60 * 24)), 'day');
  }
}
