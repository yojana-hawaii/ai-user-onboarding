import { Routes } from '@angular/router';
import { workflows } from '../permissions';
import { WorkflowPlaceholder } from '../workflow-placeholder';

export const offboardingRoutes: Routes = [
  {
    path: '',
    title: 'Offboarding | DaHub',
    component: WorkflowPlaceholder,
    data: workflows.offboarding,
  },
];
