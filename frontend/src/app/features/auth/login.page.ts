import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';
import { IconComponent } from '../../shared/icon.component';

interface Lockout {
  email: string; // the server locks per email (+ computer), so the screen does too
  until: number; // epoch ms
  total: number; // seconds, for the progress bar
}

const LOCK_KEY = 'lcc-borrowing.login-lock';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly error = signal<string | null>(null);
  protected readonly attemptsRemaining = signal<number | null>(null);
  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly year = new Date().getFullYear();

  // ---- Lockout countdown (the server enforces it; this only shows it) ----
  private readonly lockout = signal<Lockout | null>(restoreLockout());
  private readonly now = signal(Date.now());

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  private readonly email = toSignal(this.form.controls.email.valueChanges, { initialValue: '' });

  protected readonly secondsLeft = computed(() => {
    const lock = this.lockout();
    if (!lock || lock.email !== normalize(this.email())) return 0;
    return Math.max(0, Math.ceil((lock.until - this.now()) / 1000));
  });

  protected readonly isLocked = computed(() => this.secondsLeft() > 0);

  protected readonly countdown = computed(() => {
    const s = this.secondsLeft();
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  });

  /** 100 → 0 as the lock runs out. */
  protected readonly lockProgress = computed(() => {
    const lock = this.lockout();
    return lock ? (this.secondsLeft() / lock.total) * 100 : 0;
  });

  constructor() {
    // As soon as the user edits either field, the old error no longer applies (a lockout still does).
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      if (this.error()) this.error.set(null);
    });

    // Tick once a second; when the lock runs out, clear it.
    const timer = setInterval(() => {
      this.now.set(Date.now());
      const lock = this.lockout();
      if (lock && lock.until <= Date.now()) this.endLockout();
    }, 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected submit(): void {
    if (this.isLocked()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => {
        sessionStorage.removeItem(LOCK_KEY);
        this.router.navigateByUrl('/');
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        const body = err instanceof HttpErrorResponse ? err.error : null;

        if (err instanceof HttpErrorResponse && err.status === 429) {
          this.startLockout(Number(body?.retryAfterSeconds) || 60);
          return;
        }

        this.error.set(errorMessage(err));
        this.attemptsRemaining.set(typeof body?.attemptsRemaining === 'number' ? body.attemptsRemaining : null);
      },
    });
  }

  private startLockout(seconds: number): void {
    const lock: Lockout = {
      email: normalize(this.form.controls.email.value),
      until: Date.now() + seconds * 1000,
      total: seconds,
    };
    this.lockout.set(lock);
    this.now.set(Date.now());
    this.error.set(null);
    this.attemptsRemaining.set(null);
    this.form.controls.password.reset();
    sessionStorage.setItem(LOCK_KEY, JSON.stringify(lock));
  }

  private endLockout(): void {
    this.lockout.set(null);
    sessionStorage.removeItem(LOCK_KEY);
  }
}

function normalize(email: string): string {
  return email.trim().toLowerCase();
}

/** Keeps the countdown on screen if the page is refreshed mid-lock. */
function restoreLockout(): Lockout | null {
  try {
    const lock = JSON.parse(sessionStorage.getItem(LOCK_KEY) ?? 'null') as Lockout | null;
    return lock && lock.until > Date.now() ? lock : null;
  } catch {
    return null;
  }
}
