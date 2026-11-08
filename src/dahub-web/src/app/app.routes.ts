import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home').then((module) => module.Home),
  },
  { path: '**', redirectTo: '' },
];
