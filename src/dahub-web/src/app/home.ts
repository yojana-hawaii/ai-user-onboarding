import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { signInUrl } from './auth';
import { AppSession } from './permissions';

@Component({
  selector: 'app-home',
  template: `
    <main>
      <p>KALIHI-PALAMA HEALTH CENTER</p>
      <h1>DaHub</h1>
      <p>Employee lifecycle management for Kalihi-Palama Health Center.</p>
      @if (displayName()) {
        <p>Signed in as {{ displayName() }}.</p>
        <nav aria-label="Employee workflows">
          @for (item of navigation; track item.path) {
            @if (permissions().includes(item.permission)) {
              <a [href]="item.path">{{ item.label }}</a>
            }
          }
        </nav>
        @if (requestToken()) {
          <form method="post" action="/api/auth/logout">
            <input type="hidden" name="__RequestVerificationToken" [value]="requestToken()" />
            <button type="submit">Sign out</button>
          </form>
        }
      } @else {
        <a [href]="loginUrl">Sign in with Okta</a>
      }
      @if (message()) {
        <p role="status">{{ message() }}</p>
      }
      <p>Employee workflows are being developed.</p>
    </main>
  `,
})
export class Home {
  private readonly http = inject(HttpClient);
  readonly displayName = signal('');
  readonly permissions = signal<string[]>([]);
  readonly navigation = [
    { path: '/requests', label: 'Requests', permission: 'requests.view.own' },
    { path: '/provisioning', label: 'Provisioning', permission: 'provisioning.execute' },
    { path: '/audit', label: 'Audit', permission: 'audit.search' },
  ];
  readonly requestToken = signal('');
  readonly message = signal(
    new URLSearchParams(location.search).has('authError')
      ? 'Sign-in failed. Contact your administrator to verify your access.'
      : '',
  );
  readonly loginUrl = signInUrl();

  constructor() {
    this.http.get<AppSession>('/api/auth/session').subscribe({
      next: (session) => {
        this.displayName.set(session.displayName);
        this.permissions.set(session.permissions ?? []);
        this.http.get<{ requestToken: string }>('/api/auth/antiforgery').subscribe({
          next: (token) => this.requestToken.set(token.requestToken),
          error: () => this.message.set('Unable to prepare sign-out. Please reload the page.'),
        });
      },
      error: (error: HttpErrorResponse) => {
        if (error.status !== 401)
          this.message.set('Unable to check your session. Please try again.');
      },
    });
  }
}
