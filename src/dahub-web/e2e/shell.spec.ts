import { expect, test } from '@playwright/test';

test.describe.configure({ timeout: 60_000 });

// Synthetic local grants test UI behavior; they do not validate live Okta or API authorization.
const roles = [
  {
    name: 'HR',
    permissions: ['requests.submit', 'requests.view.own'],
    links: ['Requests', 'Onboarding', 'Offboarding'],
  },
  {
    name: 'IT',
    permissions: ['requests.view.own', 'provisioning.execute', 'audit.search'],
    links: ['Requests', 'Provisioning', 'Audit'],
  },
  {
    name: 'Manager',
    permissions: ['requests.submit', 'requests.view.own', 'approvals.action'],
    links: ['Requests', 'Onboarding', 'Offboarding'],
  },
  { name: 'Executive', permissions: ['dashboard.view'], links: [] },
  {
    name: 'Admin',
    permissions: [
      'requests.submit',
      'requests.view.own',
      'provisioning.execute',
      'audit.search',
      'users.manage',
      'templates.manage',
    ],
    links: [
      'Requests',
      'Onboarding',
      'Offboarding',
      'Provisioning',
      'Audit',
      'Administration',
      'Organization seed data',
    ],
  },
  { name: 'Compliance', permissions: ['audit.search', 'audit.export'], links: ['Audit'] },
];

for (const role of roles) {
  test(`${role.name} navigation follows local grants at desktop and mobile widths`, async ({
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
    const nav = page.getByRole('navigation', { name: 'Employee workflows' });
    await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
    for (const width of [1280, 375]) {
      await page.setViewportSize({ width, height: 800 });
      await expect(page.getByRole('banner')).toBeVisible();
      await expect(page.getByRole('main')).toBeVisible();
      if (role.links.length) {
        await expect(nav.getByRole('link')).toHaveText(role.links);
        for (const label of role.links)
          await expect(nav.getByRole('link', { name: label, exact: true })).toBeVisible();
        const navigation = await nav.boundingBox();
        const main = await page.getByRole('main').boundingBox();
        expect(navigation).not.toBeNull();
        expect(main).not.toBeNull();
        if (width === 375)
          expect(main!.y).toBeGreaterThanOrEqual(navigation!.y + navigation!.height - 1);
        else expect(main!.x).toBeGreaterThanOrEqual(navigation!.x + navigation!.width - 1);
      } else await expect(nav).toHaveCount(0);
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      ).toBe(true);
    }
    if (role.links.length) {
      const link = nav.getByRole('link', { name: role.links[0], exact: true });
      await link.click();
      await expect(page.getByRole('heading', { name: role.links[0], exact: true })).toBeVisible();
      await expect(link).toHaveAttribute('aria-current', 'page');
    }
  });
}

test('a revoked permission ends loading and removes protected navigation', async ({ page }) => {
  let revoked = false;
  let release!: () => void;
  const waiting = new Promise<void>((resolve) => {
    release = resolve;
  });
  await page.route('**/api/auth/session', async (route) => {
    if (revoked) await waiting;
    await route.fulfill({
      json: {
        appUserId: 1,
        displayName: 'Synthetic IT',
        permissions: revoked ? [] : ['provisioning.execute'],
      },
    });
  });
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'synthetic-proof' } }),
  );
  await page.goto('/');
  const link = page.getByRole('link', { name: 'Provisioning', exact: true });
  await expect(link).toBeVisible();
  revoked = true;
  try {
    await link.click();
    await expect(page.getByRole('main')).toHaveAttribute('aria-busy', 'true');
    await expect(page.locator('.loading-skeleton')).toBeVisible();
  } finally {
    release();
  }
  await expect(page.getByRole('heading', { name: 'Access denied' })).toBeVisible();
  await expect(page.getByRole('main')).toHaveAttribute('aria-busy', 'false');
  await expect(page.locator('.loading-skeleton')).toHaveCount(0);
  await expect(page.getByRole('navigation')).toHaveCount(0);
  await expect(page.getByRole('banner')).toBeVisible();
});

test('query-only history navigation rechecks an expired session and preserves the target', async ({
  page,
}) => {
  let expired = false;
  await page.route('**/api/auth/session', (route) =>
    route.fulfill(
      expired
        ? { status: 401, json: {} }
        : {
            json: {
              appUserId: 1,
              displayName: 'Synthetic Compliance',
              permissions: ['audit.search'],
            },
          },
    ),
  );
  await page.route('**/api/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'synthetic-proof' } }),
  );
  await page.goto('/audit?page=1');
  await expect(page.getByRole('heading', { name: 'Audit', exact: true })).toBeVisible();
  expired = true;
  // Same-document history navigation keeps the active route available for query-only reuse.
  await page.evaluate(() => {
    history.pushState({}, '', '/audit?page=2');
    dispatchEvent(new PopStateEvent('popstate'));
  });
  await expect(page.getByRole('heading', { name: 'Sign in required' })).toBeVisible();
  expect(new URL(page.url()).searchParams.get('returnUrl')).toBe('/audit?page=2');
  await expect(page.getByRole('link', { name: 'Sign in with Okta' })).toHaveAttribute(
    'href',
    '/api/auth/login?returnUrl=%2Faudit%3Fpage%3D2',
  );
});

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
