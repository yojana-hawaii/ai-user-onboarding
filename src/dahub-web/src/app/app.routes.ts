import { Routes } from '@angular/router';
import { permissionGuard } from './permissions';

export const routes: Routes = [
  {
    path: 'sign-in',
    loadComponent: () => import('./access-page').then((module) => module.SignInPage),
  },
  {
    path: '',
    loadComponent: () => import('./home').then((module) => module.Home),
    children: [
      {
        path: '',
        pathMatch: 'full',
        title: 'DaHub',
        loadComponent: () => import('./overview').then((module) => module.Overview),
      },
      {
        path: 'forbidden',
        title: 'Access denied | DaHub',
        loadComponent: () => import('./access-page').then((module) => module.AccessPage),
        data: {
          title: 'Access denied',
          message: 'You do not have permission to access this page.',
        },
      },
      ...[
        { path: 'requests', title: 'Requests', permissions: ['requests.view.own'] },
        { path: 'provisioning', title: 'Provisioning', permissions: ['provisioning.execute'] },
        { path: 'audit', title: 'Audit', permissions: ['audit.search'] },
      ].map((page) => ({
        path: page.path,
        title: page.title + ' | DaHub',
        canActivate: [permissionGuard],
        runGuardsAndResolvers: 'always' as const,
        loadComponent: () => import('./access-page').then((module) => module.AccessPage),
        data: { ...page, message: 'This workflow is being developed.' },
      })),
      {
        path: '**',
        title: 'Page not found | DaHub',
        loadComponent: () => import('./access-page').then((module) => module.AccessPage),
        data: { title: 'Page not found', message: 'The page you requested could not be found.' },
      },
    ],
  },
];
