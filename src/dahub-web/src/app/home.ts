import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationSkipped,
  NavigationStart,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { signInUrl } from './auth';
import { SessionState } from './permissions';

@Component({
  selector: 'app-home',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <a class="skip-link" href="#main-content">Skip to content</a>
    <header class="app-header">
      <div>
        <p class="organization">KALIHI-PALAMA HEALTH CENTER</p>
        <a class="brand" routerLink="/">DaHub</a>
      </div>
      <div class="session-controls">
        @if (session()) {
          <span>Signed in as {{ session()!.displayName }}.</span>
          @if (requestToken()) {
            <form method="post" action="/api/auth/logout">
              <input type="hidden" name="__RequestVerificationToken" [value]="requestToken()" />
              <button type="submit">Sign out</button>
            </form>
          }
        } @else if (sessionState.loaded()) {
          <a [href]="loginUrl">Sign in with Okta</a>
        }
      </div>
    </header>
    <div class="app-layout">
      <nav aria-label="Employee workflows">
        @for (item of visibleNavigation(); track item.path) {
          <a [routerLink]="item.path" routerLinkActive="active" ariaCurrentWhenActive="page">{{
            item.label
          }}</a>
        }
        @if (session() && !visibleNavigation().length) {
          <p>No workflow pages are available for your current permissions.</p>
        }
      </nav>
      <main id="main-content" tabindex="-1" [attr.aria-busy]="loading()">
        @if (message()) {
          <p role="status">{{ message() }}</p>
        }
        @if (loading()) {
          <div class="loading-skeleton" role="status">
            <span>Loading page…</span>
            <div aria-hidden="true"></div>
            <div aria-hidden="true"></div>
          </div>
        }
        <div [hidden]="loading()"><router-outlet /></div>
      </main>
    </div>
  `,
})
export class Home {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  readonly sessionState = inject(SessionState);
  readonly session = this.sessionState.session;
  readonly loading = signal(this.router.currentNavigation() !== null);
  readonly navigation = [
    { path: '/requests', label: 'Requests', permission: 'requests.view.own' },
    { path: '/provisioning', label: 'Provisioning', permission: 'provisioning.execute' },
    { path: '/audit', label: 'Audit', permission: 'audit.search' },
  ];
  readonly visibleNavigation = computed(() =>
    this.navigation.filter((item) => this.session()?.permissions?.includes(item.permission)),
  );
  readonly requestToken = signal('');
  readonly message = signal(
    new URLSearchParams(location.search).has('authError')
      ? 'Sign-in failed. Contact your administrator to verify your access.'
      : '',
  );
  get loginUrl() {
    return signInUrl();
  }

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((event) => {
      if (event instanceof NavigationStart) this.loading.set(true);
      if (
        event instanceof NavigationEnd ||
        event instanceof NavigationCancel ||
        event instanceof NavigationError ||
        event instanceof NavigationSkipped
      )
        this.loading.set(false);
    });
    if (this.sessionState.loaded()) {
      if (this.session()) this.prepareSignOut();
    } else {
      this.sessionState
        .load()
        .pipe(takeUntilDestroyed())
        .subscribe({
          next: () => this.prepareSignOut(),
          error: (error: HttpErrorResponse) => {
            if (error.status !== 401)
              this.message.set('Unable to check your session. Please try again.');
          },
        });
    }
  }

  private prepareSignOut() {
    this.http
      .get<{ requestToken: string }>('/api/auth/antiforgery')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (token) => this.requestToken.set(token.requestToken),
        error: () => this.message.set('Unable to prepare sign-out. Please reload the page.'),
      });
  }
}
