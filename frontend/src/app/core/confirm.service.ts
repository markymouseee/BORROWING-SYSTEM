import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title: string;
  message?: string;
  confirmText?: string;
  cancelText?: string;
  /** 'danger' paints the confirm button red (sign out, cancel, delete…). */
  tone?: 'primary' | 'danger';
}

interface PendingConfirm extends Required<Omit<ConfirmOptions, 'message'>> {
  message: string | null;
  resolve: (ok: boolean) => void;
}

/**
 * App-wide "Are you sure?" dialog. Call `await confirm.ask({...})` from anywhere;
 * the single <app-confirm-dialog> mounted in the root component renders it.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly current = signal<PendingConfirm | null>(null);

  ask(options: ConfirmOptions): Promise<boolean> {
    // Only one dialog at a time: a new question dismisses the old one.
    this.current()?.resolve(false);

    return new Promise<boolean>((resolve) => {
      this.current.set({
        title: options.title,
        message: options.message ?? null,
        confirmText: options.confirmText ?? 'Confirm',
        cancelText: options.cancelText ?? 'Cancel',
        tone: options.tone ?? 'primary',
        resolve,
      });
    });
  }

  close(ok: boolean): void {
    const pending = this.current();
    if (!pending) return;
    this.current.set(null);
    pending.resolve(ok);
  }
}
