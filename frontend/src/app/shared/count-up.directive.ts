import { Directive, ElementRef, effect, inject, input } from '@angular/core';

/** Animates a number from its previous value to the new one: `<span [appCountUp]="15"></span>`. */
@Directive({ selector: '[appCountUp]' })
export class CountUpDirective {
  readonly appCountUp = input.required<number>();

  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private current = 0;
  private frame = 0;

  constructor() {
    effect(() => this.animateTo(this.appCountUp()));
  }

  private animateTo(target: number): void {
    cancelAnimationFrame(this.frame);
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const from = this.current;
    if (reduced || from === target) return this.render(target);

    const duration = 700;
    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - t, 3); // ease-out cubic
      this.render(Math.round(from + (target - from) * eased));
      if (t < 1) this.frame = requestAnimationFrame(step);
    };
    this.frame = requestAnimationFrame(step);
  }

  private render(value: number): void {
    this.current = value;
    this.el.textContent = String(value);
  }
}
