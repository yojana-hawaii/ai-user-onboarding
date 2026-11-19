import { Routes } from '@angular/router';
import { featurePermissionGuard, permissionGuard, workflows } from './permissions';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in | DaHub',
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
        loadComponent: () => import('./access-page').then((module) => module.ForbiddenPage),
      },
      ...(['requests', 'provisioning'] as const).map((path) => ({
        path,
        title: workflows[path].label + ' | DaHub',
        canActivate: [permissionGuard],
        runGuardsAndResolvers: 'always' as const,
        loadComponent: () =>
          import('./workflow-placeholder').then((module) => module.WorkflowPlaceholder),
        data: workflows[path],
      })),
      {
        path: 'onboarding',
        pathMatch: 'full',
        canMatch: [featurePermissionGuard],
        data: workflows.onboarding,
        loadChildren: () =>
          import('./onboarding/onboarding.routes').then((module) => module.onboardingRoutes),
      },
      {
        path: 'offboarding',
        pathMatch: 'full',
        canMatch: [featurePermissionGuard],
        data: workflows.offboarding,
        loadChildren: () =>
          import('./offboarding/offboarding.routes').then((module) => module.offboardingRoutes),
      },
      {
        path: 'audit',
        pathMatch: 'full',
        canMatch: [featurePermissionGuard],
        data: workflows.audit,
        loadChildren: () => import('./audit/audit.routes').then((module) => module.auditRoutes),
      },
      {
        path: 'admin/organization-seeds',
        title: 'Organization seed data | DaHub',
        canActivate: [permissionGuard],
        runGuardsAndResolvers: 'always',
        data: workflows['admin/organization-seeds'],
        loadComponent: () =>
          import('./organization-seeds').then((module) => module.OrganizationSeeds),
      },
      {
        path: 'admin',
        pathMatch: 'full',
        canMatch: [featurePermissionGuard],
        data: workflows.admin,
        loadChildren: () => import('./admin/admin.routes').then((module) => module.adminRoutes),
      },
      {
        path: '**',
        title: 'Page not found | DaHub',
        loadComponent: () => import('./access-page').then((module) => module.NotFoundPage),
      },
    ],
  },
];
