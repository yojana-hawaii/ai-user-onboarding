import { inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, switchMap, throwError } from 'rxjs';

export function signInUrl(): string {
  return '/api/auth/login?returnUrl=' + encodeURIComponent(location.pathname + location.search);
}

export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const http = inject(HttpClient);
  // Credentials and antiforgery proof belong only to same-origin application requests.
  if (!request.url.startsWith('/api/')) return next(request);
  const response = ['GET', 'HEAD', 'OPTIONS'].includes(request.method)
    ? next(request)
    : http
        .get<{ requestToken: string }>('/api/auth/antiforgery')
        .pipe(
          switchMap(({ requestToken }) =>
            next(request.clone({ setHeaders: { 'X-XSRF-TOKEN': requestToken } })),
          ),
        );
  return response.pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url !== '/api/auth/session'
      ) {
        location.assign(signInUrl());
      }
      return throwError(() => error);
    }),
  );
};
