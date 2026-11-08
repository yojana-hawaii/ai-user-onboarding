import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { Home } from './home';

describe('Home', () => {
  it('identifies the application and its pending sign-in feature', async () => {
    await TestBed.configureTestingModule({ imports: [Home] }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent).toBe('DaHub');
    expect(element.textContent).toContain('Sign-in and employee workflows are being developed.');
  });
});
