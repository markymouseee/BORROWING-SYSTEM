import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ReservationApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { ConfirmService } from '../../core/confirm.service';
import { ReservationAction, ReservationDetail, ReturnCondition } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon.component';
import { ModalComponent } from '../../shared/modal.component';
import { SchedulePipe, WhenPipe } from '../../shared/schedule.pipe';
import { StatusBadgeComponent } from '../../shared/status-badge.component';

type StepState = 'done' | 'current' | 'upcoming' | 'stopped';

interface TimelineStep {
  label: string;
  state: StepState;
  who?: string | null;
  when?: string | null;
  note?: string | null;
}

type Dialog = 'reject' | 'receive' | null;

@Component({
  selector: 'app-reservation-detail-page',
  imports: [FormsModule, RouterLink, IconComponent, ModalComponent, StatusBadgeComponent, SchedulePipe, WhenPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './reservation-detail.page.html',
  styleUrl: './reservation-detail.page.scss',
})
export class ReservationDetailPage {
  private readonly api = inject(ReservationApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly reservation = signal<ReservationDetail | null>(null);
  protected readonly busy = signal(false);
  protected readonly dialog = signal<Dialog>(null);

  protected rejectReason = '';
  protected returnRemarks = '';
  protected returnConditions: Record<number, ReturnCondition> = {};

  protected readonly can = computed(() => new Set<ReservationAction>(this.reservation()?.allowedActions ?? []));

  protected readonly conditionLabel: Record<ReturnCondition, string> = {
    Good: 'Good',
    Damaged: 'Damaged',
    Missing: 'Missing',
  };

  /** The lifecycle as a vertical timeline. Rejected/cancelled stop the line where they happened. */
  protected readonly timeline = computed<TimelineStep[]>(() => {
    const r = this.reservation();
    if (!r) return [];

    const steps: TimelineStep[] = [
      { label: 'Requested', state: 'done', who: r.requestedByName, when: r.createdAt },
    ];

    if (r.status === 'Rejected') {
      steps.push({ label: 'Rejected', state: 'stopped', who: r.reviewedByName, when: r.reviewedAt, note: r.rejectionReason });
      return steps;
    }

    const approved = r.reviewedAt !== null;
    steps.push({
      label: approved ? 'Approved' : 'Awaiting approval',
      state: approved ? 'done' : r.status === 'Cancelled' ? 'upcoming' : 'current',
      who: r.reviewedByName,
      when: r.reviewedAt,
    });

    if (r.status === 'Cancelled') {
      steps.push({ label: 'Cancelled', state: 'stopped' });
      return steps;
    }

    steps.push({
      label: r.borrowedAt ? 'Released to borrower' : 'Release equipment',
      state: r.borrowedAt ? 'done' : r.status === 'Approved' ? 'current' : 'upcoming',
      who: r.releasedByName,
      when: r.borrowedAt,
    });

    steps.push({
      label: r.returnedAt ? 'Returned' : r.isOverdue ? 'Return overdue' : 'Return equipment',
      state: r.returnedAt ? 'done' : r.status === 'Borrowed' ? (r.isOverdue ? 'stopped' : 'current') : 'upcoming',
      who: r.receivedByName,
      when: r.returnedAt,
      note: r.returnRemarks,
    });

    return steps;
  });

  ngOnInit(): void {
    this.api.get(Number(this.id())).subscribe((r) => this.reservation.set(r));
  }

  protected async approve(): Promise<void> {
    const r = this.current;
    const ok = await this.confirm.ask({
      title: 'Approve this request?',
      message: `${r.departmentCode} will be able to pick up ${this.itemCount(r)} for “${r.eventName}”.`,
      confirmText: 'Approve',
    });
    if (ok) this.run(this.api.approve(r.id), 'Request approved.');
  }

  protected async release(): Promise<void> {
    const r = this.current;
    const ok = await this.confirm.ask({
      title: 'Hand over the equipment?',
      message: `Confirm that ${r.requestedByName} has received ${this.itemCount(r)}. It will be marked as borrowed.`,
      confirmText: 'Mark as borrowed',
    });
    if (ok) this.run(this.api.release(r.id), 'Marked as borrowed. Equipment handed over.');
  }

  protected async cancel(): Promise<void> {
    const ok = await this.confirm.ask({
      title: 'Cancel this reservation?',
      message: 'The equipment will be freed for others to book. This cannot be undone.',
      confirmText: 'Cancel reservation',
      cancelText: 'Keep it',
      tone: 'danger',
    });
    if (ok) this.run(this.api.cancel(this.current.id), 'Reservation cancelled.');
  }

  private itemCount(r: ReservationDetail): string {
    return r.items.length === 1 ? '1 item' : `${r.items.length} items`;
  }

  protected openReceive(): void {
    this.returnRemarks = '';
    this.returnConditions = Object.fromEntries(this.current.items.map((i) => [i.equipmentId, 'Good' as ReturnCondition]));
    this.dialog.set('receive');
  }

  protected confirmReceive(): void {
    const items = Object.entries(this.returnConditions).map(([id, condition]) => ({
      equipmentId: Number(id),
      condition,
    }));
    this.run(this.api.receive(this.current.id, items, this.returnRemarks.trim() || null), 'Return recorded.');
  }

  protected openReject(): void {
    this.rejectReason = '';
    this.dialog.set('reject');
  }

  protected confirmReject(): void {
    if (!this.rejectReason.trim()) return;
    this.run(this.api.reject(this.current.id, this.rejectReason.trim()), 'Request rejected.');
  }

  private get current(): ReservationDetail {
    return this.reservation()!;
  }

  private run(request: Observable<ReservationDetail>, success: string): void {
    this.busy.set(true);
    request.subscribe({
      next: (updated) => {
        this.reservation.set(updated);
        this.busy.set(false);
        this.dialog.set(null);
        this.toast.success(success);
      },
      error: () => this.busy.set(false), // the interceptor already showed the message
    });
  }
}
