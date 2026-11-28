import { defineConfig } from '@playwright/test';
import 'dotenv/config';

export default defineConfig({
  testDir: './tests/risk',
  timeout: 60_000,
  retries: 0, // fraud/bot: fail thật phải thấy được
  workers: 1, // nhóm dùng chung IP/rate limit chạy tuần tự
  reporter: [['list'], ['html', { open: 'never' }], ['junit', { outputFile: 'test-results/risk-junit.xml' }]],
  use: { trace: 'retain-on-failure', screenshot: 'only-on-failure' },
});
