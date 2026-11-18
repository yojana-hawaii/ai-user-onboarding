import { expect, test } from '@playwright/test';

test('home and URL-preserving 404 render correctly', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'DaHub', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Sign in with Okta' })).toBeVisible();
  await page.goto('/unknown-route');
  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
  await expect(page).toHaveURL(/\/unknown-route$/);
});

test.beforeAll(async ({ request }) => {
  test.setTimeout(180_000);
  await expect
    .poll(
      async () => {
        try {
          return (await request.get('/api/health/live')).status();
        } catch {
          return 0;
        }
      },
      { timeout: 180_000 },
    )
    .toBe(200);
});

test('browser reaches the real API through the Angular proxy', async ({ page }) => {
  await page.goto('/');
  const health = await page.evaluate(async () => {
    const response = await fetch('/api/health/live');
    return { status: response.status, body: await response.text() };
  });
  expect(health).toEqual({ status: 200, body: 'Healthy' });
});

// Browser UI coverage uses synthetic API responses; SQL/API authorization is tested separately.
test('organization upload previews before applying additions', async ({ page }) => {
  await page.route('**/api/auth/session', (route) =>
    route.fulfill({
      json: {
        appUserId: 1,
        displayName: 'Synthetic Admin',
        permissions: ['templates.manage', 'users.manage'],
      },
    }),
  );
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'synthetic-csrf-proof' } }),
  );
  await page.route('**/api/admin/organization-seeds/preview', (route) =>
    route.fulfill({
      json: { summary: { added: 1, unchanged: 0 }, previewToken: 'synthetic-preview' },
    }),
  );
  await page.route('**/api/admin/organization-seeds/apply', async (route) => {
    expect(route.request().headers()['x-seed-preview']).toBe('synthetic-preview');
    expect(route.request().headers()['x-xsrf-token']).toBe('synthetic-csrf-proof');
    await route.fulfill({ json: { added: 1, unchanged: 0 } });
  });
  await page.goto('/');
  await page.getByRole('link', { name: 'Organization seed data' }).click();
  await expect(page.getByRole('heading', { name: 'Organization seed data' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Preview package' })).toBeDisabled();
  await page.getByLabel('Organization package').setInputFiles({
    name: 'synthetic-seeds.json',
    mimeType: 'application/json',
    buffer: Buffer.from(
      '{"schemaVersion":1,"locations":[{"siteCode":"EXAMPLE","name":"Example"}]}',
    ),
  });
  await page.getByRole('button', { name: 'Preview package' }).click();
  await expect(page.getByText('1 additions; 0 unchanged records.')).toBeVisible();
  await page.getByRole('button', { name: 'Apply additions' }).click();
  await expect(page.getByRole('status')).toContainText('Import completed');
  await expect(page.getByRole('button', { name: 'Apply additions' })).toHaveCount(0);
});
