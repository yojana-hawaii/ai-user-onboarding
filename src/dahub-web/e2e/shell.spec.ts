import { expect, test } from '@playwright/test';

test.describe.configure({ timeout: 60_000 });

test('the shared shell stays visible across feature routes and fits a desktop viewport', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.route('**/api/auth/session', (route) =>
    route.fulfill({
      json: {
        appUserId: 1,
        displayName: 'Synthetic Admin',
        permissions: [
          'requests.submit',
          'requests.view.own',
          'provisioning.execute',
          'audit.search',
          'users.manage',
          'templates.manage',
        ],
      },
    }),
  );
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'synthetic-proof' } }),
  );
  await page.goto('/');
  const nav = page.getByRole('navigation', { name: 'Employee workflows' });
  const header = page.getByRole('banner');
  for (const label of ['Onboarding', 'Offboarding', 'Audit', 'Administration']) {
    const link = nav.getByRole('link', { name: label, exact: true });
    await link.click();
    await expect(page.getByRole('heading', { name: label, exact: true })).toBeVisible();
    await expect(link).toHaveAttribute('aria-current', 'page');
    await expect(header.getByRole('button', { name: 'Sign out' })).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
  }
});

test('an expired session returns to the original route after simulated sign-in', async ({
  page,
}) => {
  const originalRoute = '/admin/organization-seeds?from=bookmark';
  let expired = false;
  await page.route('**/api/auth/session', (route) =>
    route.fulfill({
      json: {
        appUserId: 1,
        displayName: 'Synthetic Admin',
        permissions: ['users.manage', 'templates.manage'],
      },
    }),
  );
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill(expired ? { status: 401 } : { json: { requestToken: 'synthetic-proof' } }),
  );
  await page.route('**/api/auth/login?*', async (route) => {
    const returnUrl = new URL(route.request().url()).searchParams.get('returnUrl');
    expect(returnUrl).toBe(originalRoute);
    expired = false;
    // Use a document navigation for consistent simulated provider return in all three engines.
    await route.fulfill({
      contentType: 'text/html',
      body:
        '<!doctype html><title>Simulated sign-in</title><script>location.replace(' +
        JSON.stringify(originalRoute) +
        ')</script>',
    });
  });

  await page.goto(originalRoute);
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
  await page.getByLabel('Organization package').setInputFiles({
    name: 'synthetic-seeds.json',
    mimeType: 'application/json',
    buffer: Buffer.from('{"schemaVersion":1}'),
  });
  expired = true;
  const signIn = page.waitForRequest(
    (request) => new URL(request.url()).pathname === '/api/auth/login',
  );
  await page.getByRole('button', { name: 'Preview package' }).click();
  await signIn;
  await expect(page).toHaveURL(/\/admin\/organization-seeds\?from=bookmark$/);
  await expect(page.getByRole('heading', { name: 'Organization seed data' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Preview package' })).toBeDisabled();
});
