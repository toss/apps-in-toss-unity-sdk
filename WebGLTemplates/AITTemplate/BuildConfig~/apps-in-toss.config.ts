import { defineConfig } from '@apps-in-toss/web-framework/config';

//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
// web-framework 3.x 빌드 설정 (ait build CLI가 cosmiconfig로 이 파일을 탐색).
// 2.x granite build는 이 파일을 무시하고 granite.config.ts만 사용하므로
// 두 파일을 함께 emit해도 stable(2.x) 빌드에는 영향이 없다.
// 웹 번들은 vite build --outDir dist/web 결과물(= dist/web)을 패키징한다.
const sdkConfig = {
  appName: '%AIT_APP_NAME%',
  brand: {
    primaryColor: '%AIT_PRIMARY_COLOR%',
  },
  permissions: %AIT_PERMISSIONS%,
  webView: {
    allowsInlineMediaPlayback: %AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%,
    mediaPlaybackRequiresUserAction: %AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%,
  },
  webBundleDir: 'dist/web',
};
//// SDK_GENERATED_END ////

//// USER_CONFIG_START ////
const userConfig = {
  // 여기에 사용자 커스텀 설정을 추가하세요
};
//// USER_CONFIG_END ////

// SDK_GENERATED가 관리하는 필드(appName/brand/permissions/webBundleDir/webView 플래그)는
// AIT Configuration 창에서 설정합니다. userConfig에서 이 필드들을 덮어쓰면 2.x의
// brand.displayName/icon(3.x에서 toss 개발자센터로 이동)이나 미치환 %AIT_*% 플레이스홀더가
// bundle.json으로 새어 들어가는 마이그레이션 결함이 있었습니다. 그래서 SDK 관리 필드는 항상
// SDK 값이 이기도록 병합하고, userConfig에는 3.x 신규 필드 추가(navigationBar 등)와
// webView 옵션 확장(bounces 등)만 허용합니다.
const _userConfig = userConfig as Record<string, any>;
export default defineConfig({
  ..._userConfig,
  ...sdkConfig,
  webView: { ..._userConfig.webView, ...sdkConfig.webView },
});
