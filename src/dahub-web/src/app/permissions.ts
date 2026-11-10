import { inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';

export interface AppSession {
  appUserId: number;
  displayName: string;
  permissions: string[];
}

export const permissionGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  return inject(HttpClient)
    .get<AppSession>('/api/auth/session')
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
