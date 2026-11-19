import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SILENT } from './errors';
import {
  Availability,
  CreateReservation,
  Dashboard,
  Department,
  Equipment,
  EquipmentDetail,
  EquipmentStatus,
  PagedResult,
  ReservationDetail,
  ReservationQuery,
  ReservationSummary,
  ReturnCondition,
  SaveEquipment,
  SaveUser,
  User,
} from './models';

/** For form submissions: the form shows the error inline, so skip the global toast. */
const inline = () => ({ context: new HttpContext().set(SILENT, true) });

/** Drops empty values so they don't end up as `?status=&search=` in the URL. */
function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}

@Injectable({ providedIn: 'root' })
export class EquipmentApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/equipment';

  list(query: { search?: string; category?: string; status?: EquipmentStatus | null; includeRetired?: boolean } = {}) {
    return this.http.get<Equipment[]>(this.url, { params: toParams(query) });
  }

  categories() {
    return this.http.get<string[]>(`${this.url}/categories`);
  }

  get(id: number) {
    return this.http.get<EquipmentDetail>(`${this.url}/${id}`);
  }

  availability(start: Date, end: Date) {
    return this.http.get<Availability[]>(`${this.url}/availability`, {
      params: toParams({ start: start.toISOString(), end: end.toISOString() }),
    });
  }

  create(body: SaveEquipment) {
    return this.http.post<Equipment>(this.url, body, inline());
  }

  update(id: number, body: SaveEquipment) {
    return this.http.put<Equipment>(`${this.url}/${id}`, body, inline());
  }
}

@Injectable({ providedIn: 'root' })
export class ReservationApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/reservations';

  list(query: ReservationQuery) {
    return this.http.get<PagedResult<ReservationSummary>>(this.url, { params: toParams(query) });
  }

  get(id: number) {
    return this.http.get<ReservationDetail>(`${this.url}/${id}`);
  }

  create(body: CreateReservation) {
    return this.http.post<ReservationDetail>(this.url, body, inline());
  }

  approve(id: number) {
    return this.action(id, 'approve');
  }

  reject(id: number, reason: string) {
    return this.action(id, 'reject', { reason });
  }

  cancel(id: number) {
    return this.action(id, 'cancel');
  }

  release(id: number) {
    return this.action(id, 'release');
  }

  receive(id: number, items: { equipmentId: number; condition: ReturnCondition }[], remarks: string | null) {
    return this.action(id, 'receive', { items, remarks });
  }

  private action(id: number, name: string, body: object = {}): Observable<ReservationDetail> {
    return this.http.post<ReservationDetail>(`${this.url}/${id}/${name}`, body);
  }
}

@Injectable({ providedIn: 'root' })
export class DepartmentApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/departments';

  list() {
    return this.http.get<Department[]>(this.url);
  }

  create(body: Omit<Department, 'id'>) {
    return this.http.post<Department>(this.url, body, inline());
  }

  update(id: number, body: Omit<Department, 'id'>) {
    return this.http.put<Department>(`${this.url}/${id}`, body, inline());
  }
}

@Injectable({ providedIn: 'root' })
export class UserApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/users';

  list() {
    return this.http.get<User[]>(this.url);
  }

  create(body: SaveUser) {
    return this.http.post<User>(this.url, body, inline());
  }

  update(id: number, body: SaveUser) {
    return this.http.put<User>(`${this.url}/${id}`, body, inline());
  }

  resetPassword(id: number, newPassword: string) {
    return this.http.post<void>(`${this.url}/${id}/reset-password`, { newPassword });
  }
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get() {
    return this.http.get<Dashboard>('/api/dashboard');
  }
}
