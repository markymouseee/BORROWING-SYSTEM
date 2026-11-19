import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { EquipmentApi } from '../../core/api';
import { ConfirmService } from '../../core/confirm.service';
import { errorMessage } from '../../core/errors';
import { Equipment, EquipmentCondition } from '../../core/models';
import { ModalComponent } from '../../shared/modal.component';

@Component({
  selector: 'app-equipment-form-dialog',
  imports: [ReactiveFormsModule, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal
      [title]="equipment() ? 'Edit ' + equipment()!.assetTag : 'Add equipment'"
      [subtitle]="equipment() ? 'Changes apply immediately.' : 'Register one physical unit with its own asset tag.'"
      [open]="open()"
      (closed)="closed.emit()"
    >
      <form [formGroup]="form" (ngSubmit)="save()" novalidate>
        <div class="modal-body">
          @if (error()) {
            <div class="alert error" style="margin-bottom: 14px">{{ error() }}</div>
          }
          <div class="form-grid">
            <div class="field">
              <label for="eq-tag">Asset tag</label>
              <input id="eq-tag" class="input mono" formControlName="assetTag" placeholder="PRJ-003" />
              <span class="hint">The label stuck on the unit.</span>
            </div>
            <div class="field">
              <label for="eq-cat">Category</label>
              <input id="eq-cat" class="input" formControlName="category" list="eq-categories" placeholder="Projector" />
              <datalist id="eq-categories">
                @for (c of categories(); track c) {
                  <option [value]="c"></option>
                }
              </datalist>
            </div>
            <div class="field span-2">
              <label for="eq-name">Name</label>
              <input id="eq-name" class="input" formControlName="name" placeholder="LCD projector" />
            </div>
            <div class="field span-2">
              <label for="eq-desc">Description <span class="muted">(optional)</span></label>
              <textarea id="eq-desc" class="input" formControlName="description"
                        placeholder="Brand, model, accessories included…"></textarea>
            </div>
            <div class="field">
              <label for="eq-cond">Condition</label>
              <select id="eq-cond" class="input" formControlName="condition">
                <option value="Good">Good</option>
                <option value="NeedsRepair">Needs repair</option>
                <option value="Missing">Missing</option>
              </select>
            </div>
            @if (equipment()) {
              <div class="field">
                <label for="eq-active">In inventory</label>
                <select id="eq-active" class="input" formControlName="isActive">
                  <option [ngValue]="true">Active</option>
                  <option [ngValue]="false">Retired</option>
                </select>
              </div>
            }
          </div>
        </div>
        <div class="modal-footer">
          <button type="button" class="btn" (click)="closed.emit()">Cancel</button>
          <button type="submit" class="btn primary" [disabled]="saving()">
            {{ equipment() ? 'Save changes' : 'Add equipment' }}
          </button>
        </div>
      </form>
    </app-modal>
  `,
})
export class EquipmentFormDialog {
  private readonly api = inject(EquipmentApi);
  private readonly confirm = inject(ConfirmService);

  readonly open = input(false);
  readonly equipment = input<Equipment | null>(null);
  readonly categories = input<string[]>([]);
  readonly closed = output<void>();
  readonly saved = output<Equipment>();

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    assetTag: ['', [Validators.required, Validators.maxLength(30)]],
    name: ['', [Validators.required, Validators.maxLength(120)]],
    category: ['', [Validators.required, Validators.maxLength(60)]],
    description: [''],
    condition: ['Good' as EquipmentCondition],
    isActive: [true],
  });

  constructor() {
    // Reset the form each time the dialog opens.
    effect(() => {
      if (!this.open()) return;
      const e = this.equipment();
      this.error.set(null);
      this.form.reset({
        assetTag: e?.assetTag ?? '',
        name: e?.name ?? '',
        category: e?.category ?? '',
        description: e?.description ?? '',
        condition: e?.condition ?? 'Good',
        isActive: e?.isActive ?? true,
      });
    });
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Asset tag, name and category are required.');
      return;
    }

    const value = this.form.getRawValue();
    if (this.equipment()?.isActive && !value.isActive) {
      const ok = await this.confirm.ask({
        title: `Retire ${value.assetTag}?`,
        message: 'It will be hidden from the reservation list. Existing bookings for it cannot be released.',
        confirmText: 'Retire unit',
        tone: 'danger',
      });
      if (!ok) return;
    }

    const body = { ...value, description: value.description || null };
    const existing = this.equipment();
    const request = existing ? this.api.update(existing.id, body) : this.api.create(body);

    this.saving.set(true);
    request.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.saved.emit(saved);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }
}
