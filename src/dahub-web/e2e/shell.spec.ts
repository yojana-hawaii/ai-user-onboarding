import { expect, test } from '@playwright/test';

// Browser UI contracts use synthetic sessions; live Okta and API authorization are separate checks.
const roles = [
  { name: 'HR', permissions: ['requests.view.own'], links: ['Requests'] },
  {
    name: 'IT',
    permissions: ['requests.view.own', 'provisioning.execute', 'audit.search'],
    links: ['Requests', 'Provisioning', 'Audit'],
  },
  { name: 'Manager', permissions: ['requests.view.own', 'approvals.action'], links: ['Requests'] },
  { name: 'Executive', permissions: ['dashboard.view'], links: [] },
  {
    name: 'Admin',
    permissions: ['requests.view.own', 'provisioning.execute', 'audit.search', 'users.manage'],
    links: ['Requests', 'Provisioning', 'Audit'],
  },
  { name: 'Compliance', permissions: ['audit.search', 'audit.export'], links: ['Audit'] },
];

for (const role of roles) {
  test(`${role.name} navigation uses local permissions and fits desktop/mobile`, async ({
    page,
  }) => {
    await page.route('**/api/auth/session', (route) =>
      route.fulfill({
        json: {
          appUserId: 1,
          displayName: `Synthetic ${role.name}`,
          permissions: role.permissions,
        },
      }),
    );
    await page.route('**/api/auth/antiforgery', (route) =>
      route.fulfill({ json: { requestToken: 'synthetic-proof' } }),
    );
    await page.goto('/');
    await expect(page.getByText(`Signed in as Synthetic ${role.name}.`)).toBeVisible();
    const nav = page.getByRole('navigation', { name: 'Employee workflows' });
    await expect(nav.getByRole('link')).toHaveText(role.links);
    for (const width of [1280, 375]) {
      await page.setViewportSize({ width, height: 800 });
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
      if (role.name === 'IT') {
        await page.screenshot({
          path: `/reports/shell-${width}-${test.info().project.name}.png`,
          fullPage: true,
        });
      }
    }
    if (role.links.length) {
      await nav.getByRole('link', { name: role.links[0], exact: true }).click();
      await expect(page.getByRole('heading', { name: role.links[0], exact: true })).toBeVisible();
      await expect(nav.getByRole('link', { name: role.links[0], exact: true })).toHaveAttribute(
        'aria-current',
        'page',
      );
      await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
    } else {
      await expect(nav).toContainText('No workflow pages are available');
    }
  });
}

test('loading state ends after a revoked grant redirects to forbidden', async ({ page }) => {
  let pending = false;
  let release: () => void = () => undefined;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/auth/session', async (route) => {
    if (pending) await waiting;
    await route.fulfill({
      json: {
        appUserId: 1,
        displayName: 'Synthetic IT',
        permissions: pending ? [] : ['provisioning.execute'],
      },
    });
  });
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'synthetic-proof' } }),
  );
  await page.goto('/');
  const link = page.getByRole('link', { name: 'Provisioning', exact: true });
  await expect(link).toBeVisible();
  pending = true;
  await link.click();
  await expect(page.getByRole('status')).toContainText('Loading page');
  await expect(page.getByRole('main')).toHaveAttribute('aria-busy', 'true');
  release();
  await expect(page.getByRole('heading', { name: 'Access denied' })).toBeVisible();
  await expect(page.getByRole('main')).toHaveAttribute('aria-busy', 'false');
  await expect(page.getByRole('navigation').getByRole('link')).toHaveCount(0);
});

test('anonymous deep link returns to its query after simulated sign-in', async ({ page }) => {
  let signedIn = false;
  await page.route('**/api/auth/session', (route) =>
    route.fulfill(
      signedIn
        ? {
            json: {
              appUserId: 1,
              displayName: 'Synthetic IT',
              permissions: ['provisioning.execute'],
            },
          }
        : { status: 401, json: {} },
    ),
  );
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'synthetic-proof' } }),
  );
  await page.route('**/api/auth/login?*', async (route) => {
    const returnUrl = new URL(route.request().url()).searchParams.get('returnUrl');
    expect(returnUrl).toBe('/provisioning?tab=pending');
    signedIn = true;
    // WebKit's route mock cannot fulfill redirects; simulate the provider return with a document.
    await route.fulfill({
      contentType: 'text/html',
      body:
        '<!doctype html><title>Simulated sign-in</title><script>location.replace(' +
        JSON.stringify(returnUrl) +
        ')</script>',
    });
  });
  await page.goto('/provisioning?tab=pending');
  await expect(page.getByRole('heading', { name: 'Sign in required' })).toBeVisible();
  await page.getByRole('link', { name: 'Sign in with Okta' }).click();
  await expect(page).toHaveURL(/\/provisioning\?tab=pending$/);
  await expect(page.getByRole('heading', { name: 'Provisioning', exact: true })).toBeVisible();
});
