import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, tap, throwError } from 'rxjs';

export interface AppSession {
  appUserId: number;
  displayName: string;
  permissions: string[];
}

@Injectable({ providedIn: 'root' })
export class SessionState {
  private readonly http = inject(HttpClient);
  private requestVersion = 0;
  private readonly current = signal<AppSession | null>(null);
  readonly session = this.current.asReadonly();
  readonly loaded = signal(false);

  load() {
    const version = ++this.requestVersion;
    return this.http.get<AppSession>('/api/auth/session').pipe(
      tap((session) => {
        if (version === this.requestVersion) {
          this.loaded.set(true);
          this.current.set(session);
        }
      }),
      catchError((error: unknown) => {
        if (version === this.requestVersion) {
          this.loaded.set(true);
          this.current.set(null);
        }
        return throwError(() => error);
      }),
    );
  }
}

export const permissionGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  return inject(SessionState)
    .load()
    .pipe(
      map((session) => {
        const required = route.data['permissions'] as string[];
        return required.every((key) => session.permissions.includes(key))
          ? true
          : router.createUrlTree(['/forbidden']);
      }),
      catchError((error: HttpErrorResponse) =>
        of(
          router.createUrlTree(
            [error.status === 401 ? '/sign-in' : '/forbidden'],
            error.status === 401 ? { queryParams: { returnUrl: state.url } } : {},
          ),
        ),
      ),
    );
};
