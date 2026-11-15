import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  template: '<main><h1>{{ title }}</h1><p>{{ message }}</p><a href="/">Return home</a></main>',
})
export class AccessPage {
  private readonly data = inject(ActivatedRoute).snapshot.data;
  readonly title = this.data['title'] as string;
  readonly message = this.data['message'] as string;
}

@Component({
  template: '<main><h1>Sign in required</h1><a [href]="loginUrl">Sign in with Okta</a></main>',
})
export class SignInPage {
  private readonly target = inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl') ?? '/';
  readonly loginUrl = '/api/auth/login?returnUrl=' + encodeURIComponent(this.target);
}
