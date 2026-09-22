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
    allowsInlineMediaPlayback: %AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%,
    mediaPlaybackRequiresUserAction: %AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%,
  },
  navigationBar: %AIT_NAVIGATION_BAR%,
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

// SDK_GENERATED가 관리하는 필드(appName/brand/permissions/outdir/webViewProps 플래그)는
// AIT Configuration 창에서 설정합니다. userConfig에서 이 필드들을 덮어쓰면 brand의
// 미치환 %AIT_*% 플레이스홀더(특히 마이그레이션 중 복사된 brand 블록)가 빌드 산출물로
// 새어 들어가는 결함이 있었습니다(apps-in-toss.config.ts와 동일 클래스). 그래서 SDK 관리
// 필드는 항상 SDK 값이 이기도록 병합하고, userConfig에는 신규 필드 추가와 webViewProps
// 옵션 확장만 허용합니다.
const _userConfig = userConfig as Record<string, any>;
export default defineConfig({
  ..._userConfig,
  ...sdkConfig,
  webViewProps: { ..._userConfig.webViewProps, ...sdkConfig.webViewProps },
  navigationBar: { ..._userConfig.navigationBar, ...sdkConfig.navigationBar },
});
