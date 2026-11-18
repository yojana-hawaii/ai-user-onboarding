import { Routes } from '@angular/router';
import { workflows } from '../permissions';
import { WorkflowPlaceholder } from '../workflow-placeholder';

export const auditRoutes: Routes = [
  {
    path: '',
    title: 'Audit | DaHub',
    component: WorkflowPlaceholder,
    data: workflows.audit,
  },
];
