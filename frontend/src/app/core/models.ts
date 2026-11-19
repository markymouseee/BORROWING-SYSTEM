// Mirrors the API contracts in backend/src/BorrowingSystem.Api/Features/**/*Dtos.cs.
// Dates arrive as ISO-8601 UTC strings.

export type UserRole = 'Admin' | 'Faculty';

export type EquipmentStatus =
  | 'Available'
  | 'Reserved'
  | 'Borrowed'
  | 'UnderRepair'
  | 'Missing'
  | 'Retired';

export type EquipmentCondition = 'Good' | 'NeedsRepair' | 'Missing';

export type ReservationStatus =
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'Cancelled'
  | 'Borrowed'
  | 'Returned';

export type ReturnCondition = 'Good' | 'Damaged' | 'Missing';

export type ReservationAction = 'Approve' | 'Reject' | 'Cancel' | 'Release' | 'Receive';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: UserRole;
  departmentId: number | null;
  departmentName: string | null;
  isActive: boolean;
}

export interface LoginResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface Department {
  id: number;
  code: string;
  name: string;
}

export interface Booking {
  reservationId: number;
  eventName: string;
  departmentCode: string;
  startAt: string;
  endAt: string;
  status: ReservationStatus;
}

export interface Equipment {
  id: number;
  assetTag: string;
  name: string;
  category: string;
  description: string | null;
  condition: EquipmentCondition;
  isActive: boolean;
  status: EquipmentStatus;
  currentBooking: Booking | null;
}

export interface EquipmentDetail {
  equipment: Equipment;
  upcoming: Booking[];
  history: Booking[];
}

export interface Availability {
  equipment: Equipment;
  isAvailable: boolean;
  conflict: Booking | null;
}

export interface SaveEquipment {
  assetTag: string;
  name: string;
  category: string;
  description: string | null;
  condition: EquipmentCondition;
  isActive: boolean;
}

export interface ReservationSummary {
  id: number;
  code: string;
  eventName: string;
  venue: string | null;
  departmentCode: string;
  requestedByName: string;
  startAt: string;
  endAt: string;
  status: ReservationStatus;
  isOverdue: boolean;
  assetTags: string[];
  createdAt: string;
}

export interface ReservationItem {
  equipmentId: number;
  assetTag: string;
  name: string;
  category: string;
  returnCondition: ReturnCondition | null;
}

export interface ReservationDetail {
  id: number;
  code: string;
  eventName: string;
  venue: string | null;
  purpose: string | null;
  departmentId: number;
  departmentCode: string;
  departmentName: string;
  requestedById: number;
  requestedByName: string;
  startAt: string;
  endAt: string;
  status: ReservationStatus;
  isOverdue: boolean;
  createdAt: string;
  reviewedByName: string | null;
  reviewedAt: string | null;
  rejectionReason: string | null;
  releasedByName: string | null;
  borrowedAt: string | null;
  receivedByName: string | null;
  returnedAt: string | null;
  returnRemarks: string | null;
  items: ReservationItem[];
  allowedActions: ReservationAction[];
}

export interface CreateReservation {
  eventName: string;
  venue: string | null;
  purpose: string | null;
  departmentId: number | null;
  startAt: string;
  endAt: string;
  equipmentIds: number[];
}

export interface ReservationQuery {
  status?: ReservationStatus | null;
  overdueOnly?: boolean;
  mine?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ScheduleEntry {
  reservationId: number;
  eventName: string;
  departmentCode: string;
  requestedByName: string;
  startAt: string;
  endAt: string;
  status: ReservationStatus;
  itemCount: number;
}

export interface Dashboard {
  equipmentByStatus: Partial<Record<EquipmentStatus, number>>;
  totalEquipment: number;
  pendingApprovals: number;
  forRelease: ScheduleEntry[];
  dueForReturn: ScheduleEntry[];
  overdue: ScheduleEntry[];
}

export interface SaveUser {
  fullName: string;
  email: string;
  role: UserRole;
  departmentId: number | null;
  isActive?: boolean;
  password?: string;
}
