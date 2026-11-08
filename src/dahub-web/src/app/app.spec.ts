import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, expect, it } from 'vitest';
import { App } from './app';
import { routes } from './app.routes';

describe('Application routing', () => {
  it('renders the application outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });

  it('loads the home page and redirects unknown routes', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('DaHub');
    await harness.navigateByUrl('/missing');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('DaHub');
  });
});
