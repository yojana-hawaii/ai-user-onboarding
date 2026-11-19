import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  template: `<h1>{{ label }}</h1>
    <p>This workflow is being developed.</p>`,
})
export class WorkflowPlaceholder {
  readonly label = inject(ActivatedRoute).snapshot.data['label'] as string;
}
