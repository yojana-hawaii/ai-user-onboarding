import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { render, within } from '@testing-library/angular';
import { Home } from './home';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

describe('Home', () => {
  it('offers direct Okta sign-in for an anonymous visitor', async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/session')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('DaHub');
    expect(element.querySelector('a[href^="/api/auth/login"]')?.textContent).toContain(
      'Sign in with Okta',
    );
  });

  it('shows the local display name and a CSRF-protected sign-out form', () => {
    TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(Home);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/session').flush({ appUserId: 1, displayName: 'Synthetic User' });
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-csrf-proof' });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Signed in as Synthetic User');
    expect(element.querySelector('form')?.getAttribute('method')).toBe('post');
    expect(element.querySelector('input')?.value).toBe('synthetic-csrf-proof');
    http.verify();
  });

  it.each([
    ['HR', ['requests.view.own'], ['Requests']],
    [
      'IT',
      ['requests.view.own', 'provisioning.execute', 'audit.search'],
      ['Requests', 'Provisioning', 'Audit'],
    ],
    ['Manager', ['requests.view.own', 'approvals.action'], ['Requests']],
    ['Executive', ['dashboard.view'], []],
    [
      'Admin',
      ['requests.view.own', 'provisioning.execute', 'audit.search', 'users.manage'],
      ['Requests', 'Provisioning', 'Audit'],
    ],
    ['Compliance', ['audit.search', 'audit.export'], ['Audit']],
    ['Unknown', [], []],
    ['UrgentOperator', ['provisioning.urgent'], []],
  ])('filters the shell for %s local grants', async (displayName, permissions, expected) => {
    const { fixture, getByRole } = await render(Home, {
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
      autoDetectChanges: false,
    });
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/session').flush({ appUserId: 1, displayName, permissions });
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    fixture.detectChanges();
    expect(
      within(getByRole('navigation', { name: 'Employee workflows' }))
        .queryAllByRole('link')
        .map((a) => a.textContent?.trim()),
    ).toEqual(expected);
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
    http.verify();
  });

  it('reports session discovery errors without offering protected navigation', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(Home);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/session').flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Unable to check your session');
    expect(fixture.nativeElement.querySelector('nav a')).toBeNull();
    http.verify();
  });

  it('keeps sign-out unavailable when antiforgery discovery fails', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(Home);
    const http = TestBed.inject(HttpTestingController);
    http
      .expectOne('/api/auth/session')
      .flush({ appUserId: 1, displayName: 'Synthetic User', permissions: [] });
    http.expectOne('/api/auth/antiforgery').flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Unable to prepare sign-out');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    http.verify();
  });
});
