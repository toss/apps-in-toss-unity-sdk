import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    root: '.',
    include: ['tests/unit/**/*.test.ts'],
    testTimeout: 60000,
  },
});
