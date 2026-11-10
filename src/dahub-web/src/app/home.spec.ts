import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Home } from './home';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

describe('Home', () => {
  it('offers direct Okta sign-in for an anonymous visitor', async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/session')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent).toBe('DaHub');
    expect(element.querySelector('a')?.textContent).toContain('Sign in with Okta');
  });

  it('shows the local display name and a CSRF-protected sign-out form', () => {
    TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideHttpClient(), provideHttpClientTesting()],
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
});
