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

export default defineConfig({ ...sdkConfig, ...userConfig });
