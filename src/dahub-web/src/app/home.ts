import { Component, computed, effect, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationSkipped,
  NavigationStart,
  Router,
  IsActiveMatchOptions,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { signInUrl } from './auth';
import { SessionState, hasPermissions, workflows } from './permissions';

@Component({
  selector: 'app-home',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <a class="skip-link" href="#main-content">Skip to content</a>
    <header class="app-header">
      <a class="brand" routerLink="/">DaHub</a>
      <div class="session-controls">
        @if (session(); as user) {
          <span>Signed in as {{ user.displayName }}.</span>
          @if (requestToken()) {
            <form method="post" action="/api/auth/logout">
              <input type="hidden" name="__RequestVerificationToken" [value]="requestToken()" />
              <button type="submit">Sign out</button>
            </form>
          }
        } @else if (session() === null) {
          <a [href]="loginUrl">Sign in with Okta</a>
        }
      </div>
    </header>
    <div class="app-layout" [class.with-sidebar]="visibleNavigation().length > 0">
      @if (visibleNavigation().length) {
        <nav aria-label="Employee workflows">
          @for (item of visibleNavigation(); track item.path) {
            <a
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="activeLinkOptions"
              ariaCurrentWhenActive="page"
              >{{ item.label }}</a
            >
          }
        </nav>
      }
      <main id="main-content" tabindex="-1" [attr.aria-busy]="loading()">
        @if (message()) {
          <p role="status">{{ message() }}</p>
        }
        @if (signOutUnavailable()) {
          <p role="status">Unable to prepare sign-out. Please reload the page.</p>
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
  private readonly router = inject(Router);
  private readonly sessionState = inject(SessionState);
  readonly session = this.sessionState.session;
  private readonly userId = computed(() => this.session()?.appUserId);
  readonly loading = signal(this.router.currentNavigation() !== null);
  readonly activeLinkOptions: IsActiveMatchOptions = {
    paths: 'exact',
    queryParams: 'ignored',
    matrixParams: 'ignored',
    fragment: 'ignored',
  };
  readonly visibleNavigation = computed(() => {
    const session = this.session();
    if (!session) return [];
    return Object.entries(workflows)
      .filter(([, item]) => hasPermissions(session, item.permissions))
      .map(([path, item]) => ({ path: '/' + path, label: item.label }));
  });
  readonly requestToken = signal('');
  readonly signOutUnavailable = signal(false);
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
      ) {
        this.loading.set(false);
      }
    });
    if (this.session() === undefined) {
      this.sessionState
        .load()
        .pipe(takeUntilDestroyed())
        .subscribe({
          error: (error: HttpErrorResponse) => {
            if (error.status !== 401 && !this.session()) {
              this.message.set('Unable to check your session. Please try again.');
            }
          },
        });
    }
    effect((onCleanup) => {
      this.requestToken.set('');
      this.signOutUnavailable.set(false);
      if (this.userId() === undefined) return;
      const request = this.http.get<{ requestToken: string }>('/api/auth/antiforgery').subscribe({
        next: (token) => this.requestToken.set(token.requestToken),
        error: () => this.signOutUnavailable.set(true),
      });
      onCleanup(() => request.unsubscribe());
    });
  }
}
