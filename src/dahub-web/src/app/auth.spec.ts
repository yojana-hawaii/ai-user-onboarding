import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { sessionInterceptor, signInUrl } from './auth';

describe('Session interceptor', () => {
  let http: HttpClient;
  let requests: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
  });
  afterEach(() => {
    requests.verify();
    vi.unstubAllGlobals();
  });

  it('adds fresh CSRF proof to mutations without an OAuth bearer token', () => {
    http.post('/api/example', {}).subscribe();
    requests.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-csrf-proof' });
    const request = requests.expectOne('/api/example');
    expect(request.request.headers.get('X-XSRF-TOKEN')).toBe('synthetic-csrf-proof');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('sends no application proof to external services', () => {
    http.post('https://external.invalid/api/example', {}).subscribe();
    const request = requests.expectOne('https://external.invalid/api/example');
    expect(request.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    request.flush({});
  });

  it('redirects expired protected requests to Okta while preserving the route', () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/employees', search: '?page=2', assign });
    http.get('/api/example').subscribe({ error: () => undefined });
    requests.expectOne('/api/example').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(assign).toHaveBeenCalledWith('/api/auth/login?returnUrl=%2Femployees%3Fpage%3D2');
    expect(signInUrl()).toContain('returnUrl=');
  });

  it('allows anonymous session discovery and propagates ordinary server errors', () => {
    const assign = vi.fn();
    vi.stubGlobal('location', { pathname: '/', search: '', assign });
    http.get('/api/auth/session').subscribe({ error: () => undefined });
    requests
      .expectOne('/api/auth/session')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    http.get('/api/example').subscribe({ error: () => undefined });
    requests.expectOne('/api/example').flush(null, { status: 503, statusText: 'Unavailable' });
    expect(assign).not.toHaveBeenCalled();
  });
});
