import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';
import { Home } from './home';
import { SessionState } from './permissions';

function setup() {
  TestBed.configureTestingModule({
    providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
  });
  return TestBed.inject(HttpTestingController);
}

describe('Permission routes', () => {
  it('does not let an older session response restore revoked navigation grants', () => {
    const http = setup();
    const state = TestBed.inject(SessionState);
    state.load().subscribe();
    const older = http.expectOne('/api/auth/session');
    state.load().subscribe();
    http.expectOne('/api/auth/session').flush({ appUserId: 1, displayName: 'IT', permissions: [] });
    older.flush({ appUserId: 1, displayName: 'IT', permissions: ['provisioning.execute'] });
    expect(state.session()?.permissions).toEqual([]);
    state.load().subscribe({ error: () => undefined });
    const olderFailure = http.expectOne('/api/auth/session');
    state.load().subscribe();
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'IT', permissions: ['audit.search'] });
    olderFailure.flush(null, { status: 503, statusText: 'Unavailable' });
    expect(state.session()?.permissions).toEqual(['audit.search']);
    http.verify();
  });
  it('sends anonymous visitors to sign in with their requested route', async () => {
    const http = setup();
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/provisioning');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http.expectOne('/api/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    await navigation;
    expect(harness.routeNativeElement?.textContent).toContain('Sign in required');
    expect(harness.routeNativeElement?.querySelector('a')?.href).toContain(
      'returnUrl=%2Fprovisioning',
    );
    http.verify();
  });

  it('shows forbidden for direct URL access without the required permission', async () => {
    const http = setup();
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/provisioning');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'HR', permissions: ['requests.submit'] });
    await navigation;
    expect(harness.routeNativeElement?.textContent).toContain('Access denied');
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    http.verify();
  });

  it('allows the route with a current permission and fails closed on discovery errors', async () => {
    const http = setup();
    const harness = await RouterTestingHarness.create();
    let navigation = harness.navigateByUrl('/provisioning');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'IT', permissions: ['provisioning.execute'] });
    await navigation;
    expect(harness.routeNativeElement?.textContent).toContain('Provisioning');
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    navigation = harness.navigateByUrl('/audit');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http.expectOne('/api/auth/session').flush(null, { status: 503, statusText: 'Unavailable' });
    await navigation;
    expect(harness.routeNativeElement?.textContent).toContain('Access denied');
    http.verify();
  });

  it('filters HR navigation by local permissions', () => {
    const http = setup();
    const fixture = TestBed.createComponent(Home);
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'HR', permissions: ['requests.view.own'] });
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    expect(nav?.textContent).toContain('Requests');
    expect(nav?.textContent).not.toContain('Provisioning');
    expect(nav?.textContent).not.toContain('Audit');
    http.verify();
  });

  it('keeps the shell during navigation and removes revoked menu grants', async () => {
    const http = setup();
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    http.expectOne('/api/auth/session').flush({
      appUserId: 1,
      displayName: 'IT',
      permissions: ['requests.view.own', 'provisioning.execute'],
    });
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    harness.detectChanges();
    const header = harness.routeNativeElement?.querySelector('header');
    const navigation = harness.navigateByUrl('/provisioning?tab=pending');
    await new Promise((resolve) => setTimeout(resolve, 0));
    harness.detectChanges();
    expect(harness.routeNativeElement?.querySelector('.loading-skeleton')?.textContent).toContain(
      'Loading page',
    );
    expect(harness.routeNativeElement?.querySelector('main')?.getAttribute('aria-busy')).toBe(
      'true',
    );
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'IT', permissions: ['requests.view.own'] });
    await navigation;
    expect(harness.routeNativeElement?.querySelector('header')).toBe(header);
    expect(harness.routeNativeElement?.querySelector('nav')?.textContent).not.toContain(
      'Provisioning',
    );
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Access denied');
    expect(harness.routeNativeElement?.querySelector('.loading-skeleton')).toBeNull();
    http.verify();
  });

  it('updates the reused feature page and rechecks grants for query changes', async () => {
    const http = setup();
    const harness = await RouterTestingHarness.create();
    let navigation = harness.navigateByUrl('/requests');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http.expectOne('/api/auth/session').flush({
      appUserId: 1,
      displayName: 'IT',
      permissions: ['requests.view.own', 'audit.search'],
    });
    await navigation;
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    navigation = harness.navigateByUrl('/audit');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'IT', permissions: ['audit.search'] });
    await navigation;
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Audit');
    navigation = harness.navigateByUrl('/audit?page=2');
    await new Promise((resolve) => setTimeout(resolve, 0));
    http.expectOne('/api/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    await navigation;
    expect(harness.routeNativeElement?.textContent).toContain('Sign in required');
    expect(harness.routeNativeElement?.querySelector('a')?.getAttribute('href')).toContain(
      'returnUrl=%2Faudit%3Fpage%3D2',
    );
    http.verify();
  });
});
