import { HttpContextToken, HttpErrorResponse } from '@angular/common/http';

/** Set on a request when the calling component shows the error itself. */
export const SILENT = new HttpContextToken<boolean>(() => false);

/** Pulls a human-readable message out of an RFC 7807 problem response. */
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Cannot reach the server. Check your connection and try again.';
    const body = error.error;
    if (body?.errors) {
      const first = Object.values(body.errors as Record<string, string[]>)[0];
      if (first?.length) return first[0];
    }
    if (body?.detail) return body.detail;
    if (body?.title) return body.title;
  }
  return 'Something went wrong. Please try again.';
}
