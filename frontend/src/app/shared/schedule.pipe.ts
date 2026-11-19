import { Pipe, PipeTransform } from '@angular/core';

const dateFmt = new Intl.DateTimeFormat('en-PH', { month: 'short', day: 'numeric', year: 'numeric' });
const shortDateFmt = new Intl.DateTimeFormat('en-PH', { month: 'short', day: 'numeric' });
const timeFmt = new Intl.DateTimeFormat('en-PH', { hour: 'numeric', minute: '2-digit' });

function sameDay(a: Date, b: Date): boolean {
  return a.toDateString() === b.toDateString();
}

/**
 * Formats a booking window the way people say it:
 *   "Nov 12, 2026 · 8:00 AM – 12:00 PM"   (same day)
 *   "Nov 12, 8:00 AM – Nov 13, 5:00 PM"    (spans days)
 */
@Pipe({ name: 'schedule' })
export class SchedulePipe implements PipeTransform {
  transform(start: string | Date | null | undefined, end: string | Date | null | undefined): string {
    if (!start || !end) return '';
    const from = new Date(start);
    const to = new Date(end);

    if (sameDay(from, to)) {
      return `${dateFmt.format(from)} · ${timeFmt.format(from)} – ${timeFmt.format(to)}`;
    }
    return `${shortDateFmt.format(from)}, ${timeFmt.format(from)} – ${shortDateFmt.format(to)}, ${timeFmt.format(to)}`;
  }
}

/** "Nov 12, 2026, 8:00 AM" */
@Pipe({ name: 'when' })
export class WhenPipe implements PipeTransform {
  transform(value: string | Date | null | undefined): string {
    if (!value) return '';
    const date = new Date(value);
    return `${dateFmt.format(date)}, ${timeFmt.format(date)}`;
  }
}
