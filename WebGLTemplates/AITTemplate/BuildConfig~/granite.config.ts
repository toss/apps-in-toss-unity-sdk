import { defineConfig } from '@apps-in-toss/web-framework/config';

//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
const sdkConfig = {
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
    host: process.env.AIT_VITE_HOST || '%AIT_VITE_HOST%',
    port: parseInt(process.env.AIT_VITE_PORT || '%AIT_VITE_PORT%', 10),
    strictPort: false,
    commands: {
      dev: 'vite --host',
      build: 'vite build',
    },
  },
  permissions: %AIT_PERMISSIONS%,
  outdir: '%AIT_OUTDIR%',
};
//// SDK_GENERATED_END ////

//// USER_CONFIG_START ////
const userConfig = {
  // 여기에 사용자 커스텀 설정을 추가하세요
};
//// USER_CONFIG_END ////

export default defineConfig({ ...sdkConfig, ...userConfig });
