import { defineConfig } from 'vitest/config';
import path from 'path';

export default defineConfig({
  test: {
    globals: true,
    environment: 'node',
    testTimeout: 30000, // 30초 (컴파일 테스트는 시간이 오래 걸릴 수 있음)
    include: ['**/*.test.ts'],
    exclude: ['node_modules', 'dist', 'build'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      include: ['../../src/**'],
      exclude: [
        'node_modules',
        'dist',
        '**/*.test.ts',
        '**/*.spec.ts',
      ],
    },
  },
  resolve: {
    alias: {
      '@generator': path.resolve(__dirname, '../../src'),
      '@helpers': path.resolve(__dirname, './helpers'),
      '@fixtures': path.resolve(__dirname, '../fixtures'),
    },
  },
});
