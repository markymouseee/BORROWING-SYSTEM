import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DepartmentApi, UserApi } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { ConfirmService } from '../../core/confirm.service';
import { errorMessage } from '../../core/errors';
import { SaveUser, User, UserRole } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon.component';
import { ModalComponent } from '../../shared/modal.component';
import { PaginationComponent, paginate } from '../../shared/pagination.component';

interface UserForm {
  fullName: string;
  email: string;
  role: UserRole;
  departmentId: number | null;
  isActive: boolean;
  password: string;
}

@Component({
  selector: 'app-users-page',
  imports: [FormsModule, IconComponent, ModalComponent, PaginationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users.page.html',
})
export class UsersPage {
  private readonly api = inject(UserApi);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  protected readonly auth = inject(AuthService);

  protected readonly users = signal<User[] | null>(null);
  protected readonly departments = toSignal(inject(DepartmentApi).list(), { initialValue: [] });
  protected readonly search = signal('');

  protected readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return (this.users() ?? []).filter(
      (u) => !term || `${u.fullName} ${u.email} ${u.departmentName ?? ''}`.toLowerCase().includes(term),
    );
  });

  protected readonly page = signal(1);
  protected readonly pageSize = 10;
  protected readonly pageRows = computed(() => paginate(this.filtered(), this.page(), this.pageSize));

  protected readonly dialog = signal<'edit' | 'password' | null>(null);
  protected readonly editing = signal<User | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected form: UserForm = this.blankForm();
  protected newPassword = '';

  constructor() {
    effect(() => {
      this.search();
      untracked(() => this.page.set(1));
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected openEdit(user: User | null): void {
    this.editing.set(user);
    this.form = user
      ? { ...user, password: '' }
      : this.blankForm();
    this.error.set(null);
    this.dialog.set('edit');
  }

  protected openPassword(user: User): void {
    this.editing.set(user);
    this.newPassword = '';
    this.error.set(null);
    this.dialog.set('password');
  }

  protected async save(): Promise<void> {
    const f = this.form;
    if (!f.fullName.trim() || !f.email.trim()) return this.error.set('Name and email are required.');
    if (f.role === 'Faculty' && !f.departmentId) return this.error.set('Faculty accounts need a department.');
    const user = this.editing();
    if (!user && f.password.length < 8) return this.error.set('The initial password must be at least 8 characters.');

    if (user?.isActive && !f.isActive) {
      const ok = await this.confirm.ask({
        title: `Disable ${user.fullName}?`,
        message: 'They will be signed out and unable to sign in until the account is enabled again.',
        confirmText: 'Disable account',
        tone: 'danger',
      });
      if (!ok) return;
    }

    const body: SaveUser = {
      fullName: f.fullName.trim(),
      email: f.email.trim(),
      role: f.role,
      departmentId: f.departmentId,
      isActive: f.isActive,
      password: user ? undefined : f.password,
    };

    this.saving.set(true);
    (user ? this.api.update(user.id, body) : this.api.create(body)).subscribe({
      next: (saved) => this.done(`${saved.fullName} saved.`),
      error: (err) => this.fail(err),
    });
  }

  protected resetPassword(): void {
    if (this.newPassword.length < 8) return this.error.set('Use at least 8 characters.');
    this.saving.set(true);
    this.api.resetPassword(this.editing()!.id, this.newPassword).subscribe({
      next: () => this.done(`Password reset for ${this.editing()!.fullName}.`),
      error: (err) => this.fail(err),
    });
  }

  private done(message: string): void {
    this.saving.set(false);
    this.dialog.set(null);
    this.toast.success(message);
    this.load();
  }

  private fail(err: unknown): void {
    this.saving.set(false);
    this.error.set(errorMessage(err));
  }

  private blankForm(): UserForm {
    return { fullName: '', email: '', role: 'Faculty', departmentId: null, isActive: true, password: '' };
  }

  private load(): void {
    this.api.list().subscribe((list) => this.users.set(list));
  }
}
