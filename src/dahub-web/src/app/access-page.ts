import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';

@Component({
  imports: [RouterLink],
  template:
    '<section><h1>{{ title() }}</h1><p>{{ message() }}</p><a routerLink="/">Return home</a></section>',
})
export class AccessPage {
  private readonly route = inject(ActivatedRoute);
  readonly title = toSignal(this.route.data.pipe(map((data) => data['title'] as string)));
  readonly message = toSignal(this.route.data.pipe(map((data) => data['message'] as string)));
}

@Component({
  template: '<main><h1>Sign in required</h1><a [href]="loginUrl">Sign in with Okta</a></main>',
})
export class SignInPage {
  private readonly target = inject(ActivatedRoute).snapshot.queryParamMap.get('returnUrl') ?? '/';
  readonly loginUrl = '/api/auth/login?returnUrl=' + encodeURIComponent(this.target);
}
