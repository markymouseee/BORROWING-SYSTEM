import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { IconComponent } from './icon.component';

export type PageItem = number | 'gap';

/**
 * Which page buttons to show. Always the first and last page, the current page and its
 * neighbours, and an ellipsis for every skipped run — so the bar never grows past 7 slots:
 *
 *   total 20, page 1   →  1 2 3 4 5 … 20
 *   total 20, page 10  →  1 … 9 10 11 … 20
 *   total 20, page 19  →  1 … 16 17 18 19 20
 */
export function buildPageItems(current: number, total: number): PageItem[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

  if (current <= 4) return [1, 2, 3, 4, 5, 'gap', total];
  if (current >= total - 3) return [1, 'gap', total - 4, total - 3, total - 2, total - 1, total];
  return [1, 'gap', current - 1, current, current + 1, 'gap', total];
}

/** Use the same helper to slice an in-memory list. */
export function paginate<T>(items: readonly T[], page: number, pageSize: number): T[] {
  return items.slice((page - 1) * pageSize, page * pageSize);
}

@Component({
  selector: 'app-pagination',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalCount() > 0) {
      <nav class="pagination" aria-label="Pagination">
        <span class="summary">
          Showing <b>{{ from() }}–{{ to() }}</b> of <b>{{ totalCount() }}</b> {{ noun() }}
        </span>

        @if (totalPages() > 1) {
          <div class="pages">
            <button type="button" class="arrow" [disabled]="page() <= 1" (click)="go(page() - 1)"
                    aria-label="Previous page">
              <app-icon name="chevronLeft" />
            </button>

            @for (item of items(); track $index) {
              @if (item === 'gap') {
                <span class="gap" aria-hidden="true">…</span>
              } @else {
                <button type="button" class="num" [class.current]="item === page()"
                        [attr.aria-current]="item === page() ? 'page' : null"
                        (click)="go(item)">{{ item }}</button>
              }
            }

            <button type="button" class="arrow" [disabled]="page() >= totalPages()" (click)="go(page() + 1)"
                    aria-label="Next page">
              <app-icon name="chevronRight" />
            </button>
          </div>
        }
      </nav>
    }
  `,
  styles: `
    .pagination {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      flex-wrap: wrap;
      padding: 10px 16px;
      border-top: 1px solid var(--line);
    }
    .summary { font-size: 13px; color: var(--muted); b { color: var(--ink); font-weight: 600; } }
    .pages { display: flex; align-items: center; gap: 4px; }
    button {
      display: grid;
      place-items: center;
      min-width: 32px;
      height: 32px;
      padding: 0 8px;
      border: 1px solid transparent;
      border-radius: 8px;
      background: none;
      font: inherit;
      font-size: 13px;
      font-weight: 500;
      color: var(--ink-2);
      cursor: pointer;
      transition: background 0.12s, color 0.12s, border-color 0.12s, transform 0.15s;
      app-icon { width: 16px; height: 16px; }
      &:hover:not(:disabled):not(.current) { background: var(--bg); border-color: var(--line); }
      &:disabled { opacity: 0.4; cursor: not-allowed; }
      &:focus-visible { outline: none; box-shadow: var(--focus); }
    }
    .arrow { border-color: var(--line); }
    .num.current {
      background: var(--green-700);
      color: #fff;
      box-shadow: 0 4px 10px -4px rgb(29 106 58 / 0.7);
      animation: pop 0.25s var(--ease-spring);
    }
    .gap { min-width: 24px; text-align: center; color: var(--faint); letter-spacing: 1px; user-select: none; }
    @keyframes pop { from { transform: scale(0.8); } }
  `,
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly noun = input('items');
  readonly pageChange = output<number>();

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  protected readonly items = computed(() => buildPageItems(this.page(), this.totalPages()));
  protected readonly from = computed(() => (this.page() - 1) * this.pageSize() + 1);
  protected readonly to = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  protected go(page: number): void {
    const target = Math.min(Math.max(1, page), this.totalPages());
    if (target !== this.page()) this.pageChange.emit(target);
  }
}
