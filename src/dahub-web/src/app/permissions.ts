import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { CanActivateFn, CanMatchFn, Router } from '@angular/router';
import { catchError, map, of, tap } from 'rxjs';

export interface AppSession {
  appUserId: number;
  displayName: string;
  permissions: string[];
}

export const workflows = {
  requests: { label: 'Requests', permissions: ['requests.view.own'] },
  onboarding: { label: 'Onboarding', permissions: ['requests.submit'] },
  offboarding: { label: 'Offboarding', permissions: ['requests.submit'] },
  provisioning: { label: 'Provisioning', permissions: ['provisioning.execute'] },
  audit: { label: 'Audit', permissions: ['audit.search'] },
  admin: { label: 'Administration', permissions: ['users.manage'] },
  'admin/organization-seeds': {
    label: 'Organization seed data',
    permissions: ['templates.manage', 'users.manage'],
  },
};

export function hasPermissions(session: AppSession, required: readonly string[]) {
  return required.every((key) => session.permissions.includes(key));
}

@Injectable({ providedIn: 'root' })
export class SessionState {
  private readonly http = inject(HttpClient);
  private requestVersion = 0;
  private readonly current = signal<AppSession | null | undefined>(undefined);
  readonly session = this.current.asReadonly();

  load() {
    const version = ++this.requestVersion;
    const setCurrent = (session: AppSession | null) => {
      // A response from the shell's initial lookup must not replace a newer guard result.
      if (version === this.requestVersion) this.current.set(session);
    };
    return this.http
      .get<AppSession>('/api/auth/session')
      .pipe(tap<AppSession>({ next: setCurrent, error: () => setCurrent(null) }));
  }
}

function authorize(required: string[], returnUrl: string) {
  const router = inject(Router);
  return inject(SessionState)
    .load()
    .pipe(
      map((session) => hasPermissions(session, required) || router.createUrlTree(['/forbidden'])),
      catchError((error: HttpErrorResponse) =>
        of(
          error.status === 401
            ? router.createUrlTree(['/sign-in'], { queryParams: { returnUrl } })
            : router.createUrlTree(['/forbidden']),
        ),
      ),
    );
}

export const permissionGuard: CanActivateFn = (route, state) =>
  authorize(route.data['permissions'], state.url);

export const featurePermissionGuard: CanMatchFn = (route) => {
  const router = inject(Router);
  const navigation = router.currentNavigation();
  return authorize(
    route.data?.['permissions'],
    navigation ? router.serializeUrl(navigation.extractedUrl) : '/',
  );
};
