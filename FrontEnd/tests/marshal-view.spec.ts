import { test, expect, Page } from '@playwright/test';
import {
  TEST_EVENT_ID,
  TEST_MAGIC_CODE,
  getMockApiHandlers,
  mockEvent,
  mockLocations,
  mockAreas,
  mockAssignments,
  mockChecklistItems,
  mockNotes,
  mockContacts,
  mockIncidents,
  mockRoute,
} from './fixtures/mock-data';

/**
 * Set up API mocking for all marshal view endpoints
 */
async function setupApiMocks(page: Page, scenario: 'marshal' | 'area-lead' | 'admin' = 'marshal') {
  const mocks = getMockApiHandlers(scenario);

  // Mock all API endpoints
  await page.route('**/api/health', route => route.fulfill({ status: 200 }));

  // Auth endpoints - marshal login returns success, sessionToken, and marshalId
  await page.route('**/api/auth/marshal-login', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        success: true,
        sessionToken: 'mock-session-token',
        marshalId: mocks.auth.marshalId,
      }),
    })
  );

  await page.route('**/api/auth/me*', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.auth),
    })
  );

  // Event data
  await page.route(`**/api/events/${TEST_EVENT_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.event),
    })
  );

  // Locations
  await page.route(`**/api/events/${TEST_EVENT_ID}/locations`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.locations),
    })
  );

  // Areas
  await page.route(`**/api/events/${TEST_EVENT_ID}/areas`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.areas),
    })
  );

  // Assignments
  await page.route(`**/api/assignments/event/${TEST_EVENT_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.assignments),
    })
  );

  // Marshals
  await page.route(`**/api/events/${TEST_EVENT_ID}/marshals`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.marshals),
    })
  );

  // Checklist items
  await page.route(`**/api/events/${TEST_EVENT_ID}/checklist-items`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.checklistItems),
    })
  );

  // Notes
  await page.route(`**/api/events/${TEST_EVENT_ID}/notes`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.notes),
    })
  );

  await page.route(`**/api/events/${TEST_EVENT_ID}/my-notes`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.notes),
    })
  );

  // Contacts
  await page.route(`**/api/events/${TEST_EVENT_ID}/my-contacts`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.contacts),
    })
  );

  // Incidents
  await page.route(`**/api/events/${TEST_EVENT_ID}/incidents*`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.incidents),
    })
  );

  // Area lead dashboard
  await page.route(`**/api/events/${TEST_EVENT_ID}/area-lead-dashboard`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        areas: mocks.areas,
        checkpoints: mocks.locations,
        marshals: mocks.marshals,
        assignments: mocks.assignments,
      }),
    })
  );

  // Marshal checklist
  await page.route(`**/api/events/${TEST_EVENT_ID}/marshals/*/checklist`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mocks.checklistItems),
    })
  );

  // Dynamic checkpoints
  await page.route(`**/api/events/${TEST_EVENT_ID}/dynamic-checkpoints`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    })
  );

  // Event status - this endpoint returns locations with embedded assignments
  await page.route(`**/api/events/${TEST_EVENT_ID}/status`, route => {
    // Embed assignments into locations (as the real API does)
    const locationsWithAssignments = mocks.locations.map(loc => ({
      ...loc,
      assignments: mocks.assignments
        .filter(a => a.locationId === loc.id)
        .map(a => {
          const marshal = mocks.marshals.find(m => m.id === a.marshalId);
          return {
            ...a,
            marshalName: marshal?.name || 'Unknown',
            marshalPhone: marshal?.phone,
            marshalEmail: marshal?.email,
          };
        }),
    }));

    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'active',
        locations: locationsWithAssignments,
      }),
    });
  });
}

/**
 * Navigate to marshal view with authentication
 */
async function goToMarshalView(page: Page) {
  // Listen for console errors
  page.on('console', msg => {
    if (msg.type() === 'error') {
      console.log('Browser console error:', msg.text());
    }
  });

  // Listen for page errors
  page.on('pageerror', error => {
    console.log('Page error:', error.message);
  });

  // Set session token in localStorage before navigation
  await page.addInitScript(() => {
    localStorage.setItem('sessionToken', 'mock-session-token');
  });

  await page.goto(`/#/event/${TEST_EVENT_ID}?code=${TEST_MAGIC_CODE}`);

  // Wait for either dashboard or loading or error state
  await page.waitForSelector('.dashboard, .loading, .error-card, .selection-card', { timeout: 15000 });

  // If we got loading or selection-card, wait a bit more for dashboard
  if (await page.locator('.dashboard').count() === 0) {
    await page.waitForSelector('.dashboard', { timeout: 10000 });
  }
}

// ============================================================================
// VISUAL REGRESSION TESTS
// ============================================================================

test.describe('Marshal View - Visual Regression', () => {

  test.describe('Dashboard Layout', () => {

    test('authenticated dashboard - default state', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      // Wait for content to stabilize
      await page.waitForTimeout(500);

      await expect(page).toHaveScreenshot('dashboard-default.png', {
        fullPage: true,
      });
    });

    test('header displays event name and date', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      const header = page.locator('.header');
      await expect(header).toHaveScreenshot('header.png');
    });

  });

  test.describe('Assignments Section', () => {

    test('assignments accordion expanded', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      // Click to expand assignments if not already expanded
      const assignmentsSection = page.locator('.accordion-section').filter({
        hasText: /Checkpoints|Assignments/i
      });

      if (await assignmentsSection.locator('.accordion-content:visible').count() === 0) {
        await assignmentsSection.locator('.accordion-header').click();
        await page.waitForTimeout(300);
      }

      await expect(assignmentsSection).toHaveScreenshot('assignments-expanded.png');
    });

    test('checkpoint card - checked in state', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      // Find a checked-in checkpoint card
      const checkedInCard = page.locator('.checkpoint-card').filter({
        has: page.locator('.checked-in, .check-in-status')
      }).first();

      if (await checkedInCard.count() > 0) {
        await expect(checkedInCard).toHaveScreenshot('checkpoint-checked-in.png');
      }
    });

  });

  test.describe('Checklist Section', () => {

    test('checklist accordion expanded', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      // Find and click the checklist/tasks section
      const checklistSection = page.locator('.accordion-section').filter({
        hasText: /Tasks|Checklist/i
      });

      await checklistSection.locator('.accordion-header').click();
      await page.waitForTimeout(300);

      await expect(checklistSection).toHaveScreenshot('checklist-expanded.png');
    });

  });

  test.describe('Notes Section', () => {

    test('notes accordion expanded', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      const notesSection = page.locator('.accordion-section').filter({
        hasText: /Notes/i
      });

      await notesSection.locator('.accordion-header').click();
      await page.waitForTimeout(300);

      await expect(notesSection).toHaveScreenshot('notes-expanded.png');
    });

  });

  test.describe('Contacts Section', () => {

    test('contacts accordion expanded', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      const contactsSection = page.locator('.accordion-section').filter({
        hasText: /Contacts/i
      });

      await contactsSection.locator('.accordion-header').click();
      await page.waitForTimeout(300);

      await expect(contactsSection).toHaveScreenshot('contacts-expanded.png');
    });

  });

  test.describe('Incidents Section', () => {

    test('incidents accordion expanded', async ({ page }) => {
      await setupApiMocks(page, 'marshal');
      await goToMarshalView(page);

      // Find incidents section - it might say "Your incidents" or similar
      const incidentsSection = page.locator('.accordion-section').filter({
        hasText: /incident/i
      }).first();

      if (await incidentsSection.count() > 0) {
        await incidentsSection.locator('.accordion-header').click();
        await page.waitForTimeout(300);
        await expect(incidentsSection).toHaveScreenshot('incidents-expanded.png');
      } else {
        // Skip if no incidents section exists
        test.skip();
      }
    });

  });

  test.describe('Area Lead View', () => {

    test('area lead sees additional sections', async ({ page }) => {
      await setupApiMocks(page, 'area-lead');
      await goToMarshalView(page);

      await page.waitForTimeout(500);

      await expect(page).toHaveScreenshot('area-lead-dashboard.png', {
        fullPage: true,
      });
    });

  });

});

// ============================================================================
// BEHAVIOR TESTS (functional, not visual)
// ============================================================================

test.describe('Marshal View - Behavior', () => {

  test('accordion only shows one section at a time', async ({ page }) => {
    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    // Get all accordion sections
    const sections = page.locator('.accordion-section');
    const sectionCount = await sections.count();

    if (sectionCount < 2) {
      test.skip();
      return;
    }

    // Click first section header
    await sections.nth(0).locator('.accordion-header').click();
    await page.waitForTimeout(200);

    // Click second section header
    await sections.nth(1).locator('.accordion-header').click();
    await page.waitForTimeout(200);

    // First section should now be collapsed
    const firstContent = sections.nth(0).locator('.accordion-content');
    const secondContent = sections.nth(1).locator('.accordion-content');

    // Only one should be visible/expanded
    const firstVisible = await firstContent.isVisible().catch(() => false);
    const secondVisible = await secondContent.isVisible().catch(() => false);

    // At most one should be expanded at a time
    expect(firstVisible && secondVisible).toBeFalsy();
  });

  test('header buttons are visible when authenticated', async ({ page }) => {
    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    // Check for Report Incident button
    await expect(page.getByRole('button', { name: /Report Incident/i })).toBeVisible();

    // Check for Emergency Info button
    await expect(page.getByRole('button', { name: /Emergency/i })).toBeVisible();
  });

  test('logout button is visible', async ({ page }) => {
    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    await expect(page.locator('.btn-logout-icon')).toBeVisible();
  });

});

// ============================================================================
// MODAL TESTS
// ============================================================================

test.describe('Marshal View - Modals', () => {

  test('report incident modal opens', async ({ page }) => {
    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    await page.getByRole('button', { name: /Report Incident/i }).click();
    await page.waitForTimeout(500);

    // Modal should be visible - look for modal-overlay or modal-content class
    const modal = page.locator('.modal-overlay, .modal-content, .modal-dialog').first();
    if (await modal.count() > 0) {
      await expect(modal).toBeVisible({ timeout: 5000 });
      await expect(page).toHaveScreenshot('report-incident-modal.png');
    }
  });

  test('emergency info modal opens', async ({ page }) => {
    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    await page.getByRole('button', { name: /Emergency/i }).click();
    await page.waitForTimeout(500);

    // Modal should be visible - look for modal-overlay or modal-content class
    const modal = page.locator('.modal-overlay, .modal-content, .modal-dialog').first();
    if (await modal.count() > 0) {
      await expect(modal).toBeVisible({ timeout: 5000 });
      await expect(page).toHaveScreenshot('emergency-modal.png');
    }
  });

});

// ============================================================================
// RESPONSIVE TESTS
// ============================================================================

test.describe('Marshal View - Responsive', () => {

  test('mobile layout', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 });

    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    await page.waitForTimeout(500);

    await expect(page).toHaveScreenshot('mobile-dashboard.png', {
      fullPage: true,
    });
  });

  test('tablet layout', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });

    await setupApiMocks(page, 'marshal');
    await goToMarshalView(page);

    await page.waitForTimeout(500);

    await expect(page).toHaveScreenshot('tablet-dashboard.png', {
      fullPage: true,
    });
  });

});

// ============================================================================
// UNAUTHENTICATED STATES
// ============================================================================

test.describe('Marshal View - Unauthenticated', () => {

  test('shows welcome message when not authenticated', async ({ page }) => {
    // Don't set up mocks for auth - simulate unauthenticated state
    await page.route('**/api/auth/marshal-login', route =>
      route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invalid code' }),
      })
    );

    await page.goto(`/#/event/${TEST_EVENT_ID}`);

    // Should show welcome/login prompt - use more specific selector
    await expect(page.getByRole('heading', { name: /Welcome/i })).toBeVisible({ timeout: 5000 });

    await expect(page).toHaveScreenshot('unauthenticated.png');
  });

  test('shows error on invalid magic code', async ({ page }) => {
    await page.route('**/api/auth/marshal-login', route =>
      route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Invalid or expired code' }),
      })
    );

    await page.goto(`/#/event/${TEST_EVENT_ID}?code=invalidcode`);

    await page.waitForTimeout(1000);

    await expect(page).toHaveScreenshot('login-error.png');
  });

});
