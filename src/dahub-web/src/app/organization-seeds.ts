import { Component, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';

interface SeedSummary {
  added: number;
  unchanged: number;
}
interface SeedPreview {
  summary: SeedSummary;
  previewToken: string;
}

@Component({
  selector: 'app-organization-seeds',
  template: `
    <section>
      <h1>Organization seed data</h1>
      <p>
        Upload a UTF-8 JSON catalog package. Existing records are preserved; conflicts need review.
      </p>
      <p>Packages must contain no credentials, employees, user accounts or patient information.</p>
      <label for="seed-file">Organization package (JSON, up to 512 KiB)</label>
      <input
        id="seed-file"
        type="file"
        accept=".json,application/json"
        [disabled]="busy()"
        (change)="selectFile($event)"
      />
      <button type="button" [disabled]="!content() || busy()" (click)="preview()">
        Preview package
      </button>
      @if (summary(); as result) {
        <p>{{ result.added }} additions; {{ result.unchanged }} unchanged records.</p>
      }
      @if (token()) {
        <button type="button" [disabled]="busy()" (click)="apply()">Apply additions</button>
      }
      <p role="status" aria-live="polite">{{ message() }}</p>
      <a href="/">Home</a>
    </section>
  `,
})
export class OrganizationSeeds {
  private readonly http = inject(HttpClient);
  readonly content = signal('');
  readonly busy = signal(false);
  readonly token = signal('');
  readonly summary = signal<SeedSummary | null>(null);
  readonly message = signal('');

  async selectFile(event: Event) {
    this.content.set('');
    this.token.set('');
    this.summary.set(null);
    this.message.set('');
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > 524288) {
      this.message.set('Choose a file no larger than 512 KiB.');
      return;
    }
    this.busy.set(true);
    try {
      // Strict decoding also rejects damaged files before they are re-encoded for upload.
      this.content.set(new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer()));
    } catch {
      this.message.set('Unable to read UTF-8 JSON. Choose the file again.');
    } finally {
      this.busy.set(false);
    }
  }

  preview() {
    this.token.set('');
    this.summary.set(null);
    this.message.set('Validating package…');
    this.busy.set(true);
    this.http
      .post<SeedPreview>('/api/admin/organization-seeds/preview', this.content(), {
        headers: { 'Content-Type': 'application/json' },
      })
      .subscribe({
        next: (result) => {
          this.summary.set(result.summary);
          this.token.set(result.previewToken);
          this.message.set('Review the counts, then apply within ten minutes.');
          this.busy.set(false);
        },
        error: (error: HttpErrorResponse) => this.failed(error),
      });
  }

  apply() {
    this.busy.set(true);
    this.message.set('Applying additions…');
    const token = this.token();
    this.token.set('');
    this.http
      .post<SeedSummary>('/api/admin/organization-seeds/apply', this.content(), {
        headers: { 'Content-Type': 'application/json', 'X-Seed-Preview': token },
      })
      .subscribe({
        next: (result) => {
          this.summary.set(result);
          this.content.set('');
          this.message.set('Import completed. No external accounts or access were changed.');
          this.busy.set(false);
        },
        error: (error: HttpErrorResponse) => this.failed(error),
      });
  }

  private failed(error: HttpErrorResponse) {
    this.busy.set(false);
    this.token.set('');
    this.message.set(
      error.status === 400
        ? 'Package validation failed. Check the schema, catalog keys and references.'
        : error.status === 409
          ? 'The preview or catalog changed. Preview the package again.'
          : 'Import unavailable. Check your permissions and connection, then preview again.',
    );
  }
}
