import { Routes } from '@angular/router';
import { workflows } from '../permissions';
import { WorkflowPlaceholder } from '../workflow-placeholder';

export const onboardingRoutes: Routes = [
  {
    path: '',
    title: 'Onboarding | DaHub',
    component: WorkflowPlaceholder,
    data: workflows.onboarding,
  },
];
