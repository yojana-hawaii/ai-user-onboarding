import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { Home } from './home';

function setup() {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  const fixture = TestBed.createComponent(Home);
  const http = TestBed.inject(HttpTestingController);
  const element = fixture.nativeElement as HTMLElement;
  return { fixture, http, element };
}

describe('Application shell', () => {
  it('offers direct Okta sign-in for an anonymous visitor', () => {
    const { fixture, http, element } = setup();
    http.expectOne('/api/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(element.querySelector('a[href^="/api/auth/login"]')?.textContent).toContain(
      'Sign in with Okta',
    );
    expect(element.querySelector('nav')).toBeNull();
    http.verify();
  });

  it('shows the local display name and a CSRF-protected sign-out form', () => {
    const { fixture, http, element } = setup();
    http.expectOne('/api/auth/session').flush({
      appUserId: 1,
      displayName: 'Synthetic User',
      permissions: [],
    });
    fixture.detectChanges();
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-csrf-proof' });
    fixture.detectChanges();

    expect(element.textContent).toContain('Signed in as Synthetic User');
    expect(element.querySelector('form')?.getAttribute('method')).toBe('post');
    expect(element.querySelector('input')?.value).toBe('synthetic-csrf-proof');
    http.verify();
  });

  it.each([
    ['HR', ['requests.submit', 'requests.view.own'], ['Requests', 'Onboarding', 'Offboarding']],
    [
      'IT',
      ['requests.view.own', 'provisioning.execute', 'audit.search'],
      ['Requests', 'Provisioning', 'Audit'],
    ],
    [
      'Manager',
      ['requests.submit', 'requests.view.own', 'approvals.action'],
      ['Requests', 'Onboarding', 'Offboarding'],
    ],
    ['Executive', ['dashboard.view'], []],
    [
      'Admin',
      [
        'requests.submit',
        'requests.view.own',
        'provisioning.execute',
        'audit.search',
        'users.manage',
        'templates.manage',
      ],
      [
        'Requests',
        'Onboarding',
        'Offboarding',
        'Provisioning',
        'Audit',
        'Administration',
        'Organization seed data',
      ],
    ],
    ['Compliance', ['audit.search', 'audit.export'], ['Audit']],
  ])('shows only available links for %s', (displayName, permissions, expected) => {
    const { fixture, http, element } = setup();
    http.expectOne('/api/auth/session').flush({ appUserId: 1, displayName, permissions });
    fixture.detectChanges();
    http.expectOne('/api/auth/antiforgery').flush({ requestToken: 'synthetic-proof' });
    fixture.detectChanges();

    expect(
      Array.from(element.querySelectorAll('nav a'), (link) => link.textContent?.trim()),
    ).toEqual(expected);
    http.verify();
  });

  it('reports session discovery errors without offering protected navigation', () => {
    const { fixture, http, element } = setup();
    http.expectOne('/api/auth/session').flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(element.textContent).toContain('Unable to check your session');
    expect(element.querySelector('nav')).toBeNull();
    http.verify();
  });

  it('keeps sign-out unavailable when antiforgery discovery fails', () => {
    const { fixture, http, element } = setup();
    http.expectOne('/api/auth/session').flush({
      appUserId: 1,
      displayName: 'Synthetic User',
      permissions: [],
    });
    fixture.detectChanges();
    http.expectOne('/api/auth/antiforgery').flush(null, { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(element.textContent).toContain('Unable to prepare sign-out');
    expect(element.querySelector('form')).toBeNull();
    http.verify();
  });
});
