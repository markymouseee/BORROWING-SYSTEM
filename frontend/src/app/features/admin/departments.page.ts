import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DepartmentApi } from '../../core/api';
import { errorMessage } from '../../core/errors';
import { Department } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { IconComponent } from '../../shared/icon.component';
import { ModalComponent } from '../../shared/modal.component';

@Component({
  selector: 'app-departments-page',
  imports: [FormsModule, IconComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <div class="eyebrow">Administration</div>
          <h1>Departments</h1>
          <p class="subtitle">Offices and colleges that borrow equipment.</p>
        </div>
        <div class="actions">
          <button type="button" class="btn primary" (click)="edit(null)"><app-icon name="plus" /> Add department</button>
        </div>
      </header>

      <div class="card">
        <div class="table-wrap">
          <table class="data">
            <thead><tr><th style="width: 140px">Code</th><th>Name</th><th></th></tr></thead>
            <tbody>
              @for (d of departments(); track d.id) {
                <tr>
                  <td><span class="tag">{{ d.code }}</span></td>
                  <td class="primary-text">{{ d.name }}</td>
                  <td class="actions-cell">
                    <button type="button" class="btn ghost sm" (click)="edit(d)"><app-icon name="edit" /> Edit</button>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="3"><div class="empty">No departments yet.</div></td></tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <app-modal [title]="editingId() ? 'Edit department' : 'Add department'" [open]="open()" (closed)="open.set(false)">
      <form (ngSubmit)="save()">
        <div class="modal-body" style="display: grid; gap: 14px">
          @if (error()) {
            <div class="alert error">{{ error() }}</div>
          }
          <div class="field">
            <label for="d-code">Code</label>
            <input id="d-code" class="input mono" name="code" [(ngModel)]="form.code" maxlength="20" placeholder="CTE" required />
          </div>
          <div class="field">
            <label for="d-name">Name</label>
            <input id="d-name" class="input" name="name" [(ngModel)]="form.name" maxlength="120"
                   placeholder="College of Teacher Education" required />
          </div>
        </div>
        <div class="modal-footer">
          <button type="button" class="btn" (click)="open.set(false)">Cancel</button>
          <button type="submit" class="btn primary" [disabled]="saving()">Save</button>
        </div>
      </form>
    </app-modal>
  `,
})
export class DepartmentsPage {
  private readonly api = inject(DepartmentApi);
  private readonly toast = inject(ToastService);

  protected readonly departments = signal<Department[]>([]);
  protected readonly open = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly editingId = signal<number | null>(null);
  protected form = { code: '', name: '' };

  ngOnInit(): void {
    this.load();
  }

  protected edit(d: Department | null): void {
    this.editingId.set(d?.id ?? null);
    this.form = { code: d?.code ?? '', name: d?.name ?? '' };
    this.error.set(null);
    this.open.set(true);
  }

  protected save(): void {
    if (!this.form.code.trim() || !this.form.name.trim()) {
      this.error.set('Code and name are both required.');
      return;
    }
    const id = this.editingId();
    const request = id ? this.api.update(id, this.form) : this.api.create(this.form);

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.open.set(false);
        this.toast.success(`${saved.name} saved.`);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }

  private load(): void {
    this.api.list().subscribe((list) => this.departments.set(list));
  }
}
