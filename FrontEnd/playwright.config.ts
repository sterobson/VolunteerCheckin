import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for visual regression testing
 * See https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './tests',

  /* Run tests in files in parallel */
  fullyParallel: true,

  /* Fail the build on CI if you accidentally left test.only in the source code */
  forbidOnly: !!process.env.CI,

  /* Retry on CI only */
  retries: process.env.CI ? 2 : 0,

  /* Parallel workers */
  workers: process.env.CI ? 1 : undefined,

  /* Reporter configuration */
  reporter: [
    ['html', { open: 'never' }],
    ['list']
  ],

  /* Shared settings for all projects */
  use: {
    /* Base URL for navigation */
    baseURL: 'http://localhost:5173',

    /* Collect trace when retrying the failed test */
    trace: 'on-first-retry',

    /* Screenshot on failure */
    screenshot: 'only-on-failure',
  },

  /* Configure snapshot settings for visual regression */
  expect: {
    toHaveScreenshot: {
      /* Allow slight differences (anti-aliasing, font rendering) */
      maxDiffPixelRatio: 0.01,

      /* Threshold for individual pixel comparison (0-1, lower = stricter) */
      threshold: 0.2,

      /* Animation handling - wait for animations to finish */
      animations: 'disabled',
    },
    toMatchSnapshot: {
      /* Threshold for snapshot comparison */
      threshold: 0.2,
    },
  },

  /* Configure projects - start with just Chromium for faster feedback */
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        /* Consistent viewport for screenshots */
        viewport: { width: 1280, height: 720 },
      },
    },

    /* Mobile viewport for responsive testing */
    {
      name: 'mobile',
      use: {
        ...devices['iPhone 13'],
      },
    },

    /* Uncomment to add more browsers */
    // {
    //   name: 'firefox',
    //   use: { ...devices['Desktop Firefox'] },
    // },
    // {
    //   name: 'webkit',
    //   use: { ...devices['Desktop Safari'] },
    // },
  ],

  /* Run local dev server before starting the tests */
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
  },
});
