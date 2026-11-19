import { Routes } from '@angular/router';
import { workflows } from '../permissions';
import { WorkflowPlaceholder } from '../workflow-placeholder';

export const adminRoutes: Routes = [
  {
    path: '',
    title: 'Administration | DaHub',
    component: WorkflowPlaceholder,
    data: workflows.admin,
  },
];
