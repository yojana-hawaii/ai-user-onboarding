import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Route, Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, expect, it, vi } from 'vitest';
import { routes } from './app.routes';

function setup() {
  TestBed.configureTestingModule({
    providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
  });
  return { http: TestBed.inject(HttpTestingController), router: TestBed.inject(Router) };
}

async function respondToSession(http: HttpTestingController, permissions: string[]) {
  await vi.waitFor(() => {
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'Synthetic User', permissions });
  });
}

function replyToSignOutPreparation(http: HttpTestingController) {
  http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
}

describe('Permission routes', () => {
  it('sends an anonymous visitor to Okta sign-in with the original path and query', async () => {
    const { http, router } = setup();
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/onboarding?tab=drafts');
    await vi.waitFor(() => {
      http.expectOne('/api/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    });
    await navigation;
    expect(router.url).toContain('/sign-in?');
    expect(harness.routeNativeElement?.textContent).toContain('Sign in required');
    expect(harness.routeNativeElement?.querySelector('a')?.getAttribute('href')).toBe(
      '/api/auth/login?returnUrl=%2Fonboarding%3Ftab%3Ddrafts',
    );
    http.verify();
  });

  it('shows forbidden when HR navigates directly to provisioning', async () => {
    const { http, router } = setup();
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/provisioning');
    await respondToSession(http, ['requests.submit']);
    await navigation;
    replyToSignOutPreparation(http);

    expect(router.url).toBe('/forbidden');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Access denied');
    http.verify();
  });

  it('allows a current grant and fails closed on session discovery errors', async () => {
    const { http, router } = setup();
    const harness = await RouterTestingHarness.create();
    let navigation = harness.navigateByUrl('/provisioning');
    await respondToSession(http, ['provisioning.execute']);
    await navigation;
    replyToSignOutPreparation(http);
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Provisioning');

    navigation = harness.navigateByUrl('/audit');
    await vi.waitFor(() => {
      http.expectOne('/api/auth/session').flush(null, { status: 503, statusText: 'Unavailable' });
    });
    await navigation;
    expect(router.url).toBe('/forbidden');
    expect(harness.routeNativeElement?.querySelector('nav')).toBeNull();
    http.verify();
  });

  it('renders the 404 view without changing the unknown URL', async () => {
    const { http, router } = setup();
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/unknown?from=bookmark');
    http.expectOne('/api/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    expect(router.url).toBe('/unknown?from=bookmark');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Page not found');
    http.verify();
  });

  it('loads each feature route only when visited with permission', async () => {
    const { http, router } = setup();
    const features = [
      ['onboarding', 'Onboarding'],
      ['offboarding', 'Offboarding'],
      ['audit', 'Audit'],
      ['admin', 'Administration'],
    ];
    const shellChildren = router.config.find((route) => route.children)?.children ?? [];
    const loaders = features.map(([path]) =>
      vi.spyOn(shellChildren.find((route) => route.path === path) as Route, 'loadChildren'),
    );

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    const permissions = ['requests.submit', 'audit.search', 'users.manage'];
    await respondToSession(http, permissions);
    harness.detectChanges();
    replyToSignOutPreparation(http);
    for (const loader of loaders) expect(loader).not.toHaveBeenCalled();

    for (const [index, [path, heading]] of features.entries()) {
      const navigation = harness.navigateByUrl('/' + path);
      await respondToSession(http, permissions);
      await navigation;
      expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe(heading);
      for (const [loaderIndex, loader] of loaders.entries()) {
        expect(loader).toHaveBeenCalledTimes(loaderIndex <= index ? 1 : 0);
      }
    }
    http.verify();
  });

  it('shows a skeleton while a feature bundle is loading', async () => {
    const { http, router } = setup();
    const feature = router.config
      .find((route) => route.children)
      ?.children?.find((route) => route.path === 'onboarding') as Route;
    let release!: () => void;
    const waiting = new Promise<void>((resolve) => {
      release = resolve;
    });
    vi.spyOn(feature, 'loadChildren').mockImplementation(async () => {
      await waiting;
      return (await import('./onboarding/onboarding.routes')).onboardingRoutes;
    });

    const harness = await RouterTestingHarness.create();
    const permissions = ['requests.submit', 'requests.view.own'];
    let navigation = harness.navigateByUrl('/requests');
    await respondToSession(http, permissions);
    await navigation;
    replyToSignOutPreparation(http);

    navigation = harness.navigateByUrl('/onboarding');
    await respondToSession(http, permissions);
    harness.detectChanges();
    const main = harness.routeNativeElement?.querySelector('main');
    expect(main?.getAttribute('aria-busy')).toBe('true');
    expect(main?.querySelector('.loading-skeleton')?.textContent).toContain('Loading page');
    expect(main?.querySelector('[hidden] h1')?.textContent).toBe('Requests');

    release();
    await navigation;
    expect(main?.getAttribute('aria-busy')).toBe('false');
    expect(main?.querySelector('.loading-skeleton')).toBeNull();
    expect(main?.querySelector('h1')?.textContent).toBe('Onboarding');
    http.verify();
  });
});
