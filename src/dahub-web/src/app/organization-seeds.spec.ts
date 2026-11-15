import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, expect, it } from 'vitest';
import { OrganizationSeeds } from './organization-seeds';

function setup() {
  TestBed.configureTestingModule({
    imports: [OrganizationSeeds],
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  const fixture = TestBed.createComponent(OrganizationSeeds);
  return { fixture, page: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
}

function event(file?: { size: number; arrayBuffer: () => Promise<ArrayBuffer> }): Event {
  return { target: { files: file ? [file] : [] } } as unknown as Event;
}

describe('Organization seed upload', () => {
  it('previews the selected file and requires explicit apply with the preview token', async () => {
    const { fixture, page, http } = setup();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('button')?.disabled).toBe(true);
    const body = '{"schemaVersion":1}';
    await page.selectFile(
      event({ size: body.length, arrayBuffer: async () => new TextEncoder().encode(body).buffer }),
    );
    page.preview();
    expect(page.busy()).toBe(true);
    const preview = http.expectOne('/api/admin/organization-seeds/preview');
    expect(preview.request.body).toBe(body);
    expect(preview.request.headers.get('Content-Type')).toBe('application/json');
    preview.flush({ summary: { added: 2, unchanged: 1 }, previewToken: 'synthetic-proof' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      '2 additions; 1 unchanged',
    );
    expect(page.busy()).toBe(false);
    page.apply();
    expect(page.token()).toBe('');
    const apply = http.expectOne('/api/admin/organization-seeds/apply');
    expect(apply.request.headers.get('X-Seed-Preview')).toBe('synthetic-proof');
    expect(apply.request.body).toBe(body);
    apply.flush({ added: 2, unchanged: 1 });
    expect(page.message()).toContain('Import completed');
    expect(page.content()).toBe('');
    expect(page.busy()).toBe(false);
    http.verify();
  });

  it('invalidates an old preview when file selection changes and handles unreadable files', async () => {
    const { page, http } = setup();
    page.token.set('old');
    page.summary.set({ added: 1, unchanged: 0 });
    await page.selectFile(event());
    expect(page.token()).toBe('');
    expect(page.summary()).toBe(null);
    await page.selectFile(event({ size: 524289, arrayBuffer: async () => new ArrayBuffer(0) }));
    expect(page.message()).toContain('512 KiB');
    await page.selectFile(
      event({ size: 1, arrayBuffer: async () => new Uint8Array([255]).buffer }),
    );
    expect(page.message()).toContain('Unable to read');
    await page.selectFile(
      event({
        size: 1,
        arrayBuffer: async () => {
          throw new Error('Read failed');
        },
      }),
    );
    expect(page.content()).toBe('');
    expect(page.busy()).toBe(false);
    http.verify();
  });

  it.each([
    [400, 'validation failed'],
    [409, 'Preview the package again'],
    [403, 'Check your permissions'],
    [503, 'Check your permissions'],
  ])('clears apply permission after preview error %i', (status, message) => {
    const { page, http } = setup();
    page.content.set('{}');
    page.preview();
    http
      .expectOne('/api/admin/organization-seeds/preview')
      .flush({}, { status: Number(status), statusText: 'Error' });
    expect(page.token()).toBe('');
    expect(page.busy()).toBe(false);
    expect(page.message()).toContain(String(message));
    http.verify();
  });

  it('requires another preview after an apply error', () => {
    const { page, http } = setup();
    page.content.set('{}');
    page.token.set('proof');
    page.apply();
    http
      .expectOne('/api/admin/organization-seeds/apply')
      .flush({}, { status: 409, statusText: 'Conflict' });
    expect(page.token()).toBe('');
    expect(page.busy()).toBe(false);
    expect(page.content()).toBe('{}');
    http.verify();
  });
});
