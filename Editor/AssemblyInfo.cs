using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AppsInTossSDKEditor.Tests")]
[assembly: InternalsVisibleTo("AppsInTossEditModeTests")]
// E2E 빌드 러너(테스트 인프라)가 AITLog.Error(sentryCapture:false)로 빌드 실패 로그를
// Sentry에 흘리지 않고 Console에만 남길 수 있도록 internal 접근 허용. E2E 실패는 CI exit
// code로 검출되므로 Sentry 캡처 대상이 아니다 (SDK-10P cascade 차단).
[assembly: InternalsVisibleTo("AppsInTossTestScripts.Editor")]
