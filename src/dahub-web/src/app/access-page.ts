import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden-page',
  imports: [RouterLink],
  template: `<h1>Access denied</h1>
    <p>You do not have permission to access this page.</p>
    <a routerLink="/">Return home</a>`,
})
export class ForbiddenPage {}

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  template: `<h1>Page not found</h1>
    <p>Check the address or return to the home page.</p>
    <a routerLink="/">Return home</a>`,
})
export class NotFoundPage {}

@Component({
  template: `<main class="sign-in-page">
    <h1>Sign in required</h1>
    <a [href]="loginUrl">Sign in with Okta</a>
  </main>`,
})
export class SignInPage {
  private readonly route = inject(ActivatedRoute);

  get loginUrl() {
    const target = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/';
    return '/api/auth/login?returnUrl=' + encodeURIComponent(target);
  }
}
