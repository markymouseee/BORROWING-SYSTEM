import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, viewChild } from '@angular/core';
import { ConfirmService } from '../core/confirm.service';
import { IconComponent } from './icon.component';

@Component({
  selector: 'app-confirm-dialog',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog class="modal confirm" (close)="confirm.close(false)">
      @if (confirm.current(); as c) {
        <div class="confirm-body">
          <span class="confirm-icon" [class.danger]="c.tone === 'danger'">
            <app-icon [name]="c.tone === 'danger' ? 'alert' : 'check'" />
          </span>
          <div>
            <h2>{{ c.title }}</h2>
            @if (c.message) {
              <p>{{ c.message }}</p>
            }
          </div>
        </div>
        <div class="modal-footer">
          <button type="button" class="btn" (click)="confirm.close(false)">{{ c.cancelText }}</button>
          <button type="button" class="btn" autofocus
                  [class.primary]="c.tone === 'primary'" [class.danger-solid]="c.tone === 'danger'"
                  (click)="confirm.close(true)">
            {{ c.confirmText }}
          </button>
        </div>
      }
    </dialog>
  `,
  styles: `
    dialog.confirm { width: min(420px, calc(100vw - 32px)); }
    .confirm-body { display: flex; gap: 14px; padding: 22px 22px 20px; }
    h2 { font-size: 16px; margin-top: 2px; }
    p { color: var(--muted); margin-top: 6px; }
    .confirm-icon {
      display: grid; place-items: center; flex: none;
      width: 40px; height: 40px; border-radius: 12px;
      background: var(--green-50); color: var(--green-700);
      box-shadow: inset 0 0 0 1px var(--green-100);
      &.danger { background: var(--red-50); color: var(--red-700); box-shadow: inset 0 0 0 1px var(--red-100); }
    }
  `,
})
export class ConfirmDialogComponent {
  protected readonly confirm = inject(ConfirmService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      const open = this.confirm.current() !== null;
      if (open && !el.open) el.showModal();
      if (!open && el.open) el.close();
    });
  }
}
