import { expect, test } from '@playwright/test';

test('home page and fallback route render correctly', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'DaHub', exact: true })).toBeVisible();
  await expect(page.getByText('Sign-in and employee workflows are being developed.')).toBeVisible();
  await page.goto('/unknown-route');
  await expect(page.getByRole('heading', { name: 'DaHub', exact: true })).toBeVisible();
  await expect(page).toHaveURL(/\/$/);
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
