import { Routes } from '@angular/router';
import { permissionGuard } from './permissions';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home').then((module) => module.Home),
  },
  {
    path: 'sign-in',
    loadComponent: () => import('./access-page').then((module) => module.SignInPage),
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./access-page').then((module) => module.AccessPage),
    data: { title: 'Access denied', message: 'You do not have permission to access this page.' },
  },
  ...[
    { path: 'requests', title: 'Requests', permissions: ['requests.view.own'] },
    { path: 'provisioning', title: 'Provisioning', permissions: ['provisioning.execute'] },
    { path: 'audit', title: 'Audit', permissions: ['audit.search'] },
  ].map((page) => ({
    path: page.path,
    canActivate: [permissionGuard],
    loadComponent: () => import('./access-page').then((module) => module.AccessPage),
    data: { ...page, message: 'This workflow is being developed.' },
  })),
  { path: '**', redirectTo: '' },
];
