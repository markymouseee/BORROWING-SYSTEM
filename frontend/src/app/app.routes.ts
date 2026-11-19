import { Routes } from '@angular/router';
import { adminGuard, authGuard, guestGuard } from './core/guards';
import { ShellComponent } from './layout/shell.component';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        title: 'Dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'equipment',
        title: 'Equipment',
        loadComponent: () => import('./features/equipment/equipment-list.page').then((m) => m.EquipmentListPage),
      },
      {
        path: 'equipment/:id',
        title: 'Equipment',
        loadComponent: () => import('./features/equipment/equipment-detail.page').then((m) => m.EquipmentDetailPage),
      },
      {
        path: 'reservations',
        title: 'Reservations',
        loadComponent: () =>
          import('./features/reservations/reservation-list.page').then((m) => m.ReservationListPage),
      },
      {
        path: 'reservations/new',
        title: 'New reservation',
        loadComponent: () =>
          import('./features/reservations/new-reservation.page').then((m) => m.NewReservationPage),
      },
      {
        path: 'reservations/:id',
        title: 'Reservation',
        loadComponent: () =>
          import('./features/reservations/reservation-detail.page').then((m) => m.ReservationDetailPage),
      },
      {
        path: 'admin/departments',
        title: 'Departments',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/departments.page').then((m) => m.DepartmentsPage),
      },
      {
        path: 'admin/users',
        title: 'Users',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/users.page').then((m) => m.UsersPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
