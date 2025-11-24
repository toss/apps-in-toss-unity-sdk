import { defineConfig } from '@apps-in-toss/web-framework/config';

export default defineConfig({
  appName: '%AIT_APP_NAME%',
  brand: {
    displayName: '%AIT_DISPLAY_NAME%',
    primaryColor: '%AIT_PRIMARY_COLOR%',
    icon: '%AIT_ICON_URL%',
    bridgeColorMode: 'basic',
  },
  web: {
    host: 'localhost',
    port: %AIT_LOCAL_PORT%,
    commands: {
      dev: 'vite --host',
      build: 'vite build',
    },
  },
  permissions: [],
  outdir: 'dist',
});
