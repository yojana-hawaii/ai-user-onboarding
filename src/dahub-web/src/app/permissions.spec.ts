import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, expect, it } from 'vitest';
import { routes } from './app.routes';
import { Home } from './home';

function setup() {
  TestBed.configureTestingModule({
    providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
  });
  return TestBed.inject(HttpTestingController);
}

describe('Permission routes', () => {
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
});
