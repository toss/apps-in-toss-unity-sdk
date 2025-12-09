import { defineConfig } from '@apps-in-toss/web-framework/config';

export default defineConfig({
  appName: '%AIT_APP_NAME%',
  brand: {
    displayName: '%AIT_DISPLAY_NAME%',
    primaryColor: '%AIT_PRIMARY_COLOR%',
    icon: '%AIT_ICON_URL%',
    bridgeColorMode: '%AIT_BRIDGE_COLOR_MODE%',
  },
  webViewProps: {
    type: '%AIT_WEBVIEW_TYPE%',
  },
  web: {
    host: process.env.AIT_GRANITE_HOST || '%AIT_GRANITE_HOST%',
    port: parseInt(process.env.AIT_GRANITE_PORT || '%AIT_GRANITE_PORT%', 10),
    strictPort: false,
    commands: {
      dev: 'vite --host',
      build: 'vite build',
    },
  },
  permissions: %AIT_PERMISSIONS%,
  outdir: '%AIT_OUTDIR%',
});
