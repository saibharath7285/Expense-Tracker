import { Routes } from '@angular/router';
import { ListComponent } from './pages/expenses/list/list';

export const routes: Routes = [
  { path: '', redirectTo: 'expenses', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./pages/dashboard/dashboard').then((component) => component.DashboardComponent),
  },
  { path: 'expenses', component: ListComponent },
  {
    path: 'expenses/new',
    loadComponent: () => import('./pages/expenses/details/details').then((m) => m.Details),
    data: { mode: 'create' },
  },
  {
    path: 'expenses/:id/view',
    loadComponent: () => import('./pages/expenses/details/details').then((m) => m.Details),
    data: { mode: 'view' },
  },
  {
    path: 'expenses/:id/edit',
    loadComponent: () => import('./pages/expenses/details/details').then((m) => m.Details),
    data: { mode: 'edit' },
  },
  {
    path: 'assistant',
    loadComponent: () =>
      import('./pages/assistant/assistant').then((component) => component.AssistantComponent),
  },
  { path: '**', redirectTo: 'expenses' },
];
