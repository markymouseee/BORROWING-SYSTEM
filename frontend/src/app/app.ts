import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastService } from './core/toast.service';
import { ConfirmDialogComponent } from './shared/confirm-dialog.component';
import { IconComponent } from './shared/icon.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, IconComponent, ConfirmDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <router-outlet />
    <app-confirm-dialog />

    <div class="toasts" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class.error]="toast.kind === 'error'">
          <app-icon [name]="toast.kind === 'error' ? 'alert' : 'check'" />
          <span>{{ toast.message }}</span>
          <button type="button" (click)="toasts.dismiss(toast.id)" aria-label="Dismiss">
            <app-icon name="x" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      right: 20px;
      bottom: 20px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      z-index: 1000;
      max-width: min(420px, calc(100vw - 40px));
    }
    .toast {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      padding: 12px 12px 12px 14px;
      border-radius: var(--radius);
      background: var(--green-950);
      color: #fff;
      box-shadow: var(--shadow-lg);
      animation: enter 0.18s ease-out;
      app-icon { color: #7fd49b; margin-top: 1px; }
      &.error app-icon:first-child { color: #ffb4a9; }
      span { flex: 1; }
      button {
        border: 0; background: none; color: rgb(255 255 255 / 0.6); cursor: pointer; padding: 0;
        &:hover { color: #fff; }
      }
    }
    @keyframes enter {
      from { opacity: 0; transform: translateY(6px); }
    }
  `,
})
export class App {
  protected readonly toasts = inject(ToastService);
}
