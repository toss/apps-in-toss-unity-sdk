# 프로젝트 구조

저장소에서 무엇이 어디에 있는지에 대한 지도입니다.

> **대상**: SDK 기여자. SDK를 사용하는 관점의 안내는 [시작하기](../GettingStarted.md)에 있습니다.

## 최상위

```text
apps-in-toss-unity-sdk/
├── Runtime/                  런타임 코드 (UPM 배포)
├── Editor/                   Unity Editor 코드 (UPM 배포)
├── WebGLTemplates/           WebGL 템플릿 (UPM 배포)
├── Documentation~/           문서
├── Tests~/                   E2E 및 EditMode 테스트
├── sdk-runtime-generator~/   SDK 코드 생성기
├── scripts~/                 로컬 테스트 러너가 쓰는 셸·노드 스크립트
├── .githooks/                pre-commit / pre-push
├── package.json              UPM 매니페스트
├── sdk-policy.json           최소 지원 버전 정책 (AITDeprecationChecker가 fetch)
└── run-local-tests.sh        로컬 검증 진입점
```

`~` 접미사가 붙은 디렉터리는 Unity가 임포트하지 않고 UPM 배포에서도 제외됩니다. `.meta`도 필요 없습니다.

## Runtime

```text
Runtime/
├── SDK/                      자동 생성 — 직접 수정 금지
│   ├── AIT.cs                partial class 선언
│   ├── AIT.<카테고리>.cs      카테고리별 API (Advertising, IAP, Storage 등 24개)
│   ├── AIT.Types.<카테고리>.cs 카테고리별 Options·Result 타입
│   ├── AITCore.cs            jslib 브릿지 인프라와 예외 처리
│   └── Plugins/              카테고리별 jslib 브릿지
├── Helpers/                  손으로 쓰는 런타임 보조 코드
│   ├── AIT.BannerAd.cs       배너 광고 래퍼
│   ├── AITBannerAdView.cs    RectTransform 영역 배너
│   ├── AIT.PerformanceLogger.cs  SDK 이벤트 로깅
│   ├── AIT.VisibilityHelper.cs   포그라운드·백그라운드 전환
│   ├── AITSentryReleaseResolver.cs
│   └── AITVersion.cs
└── Sentry/                   Sentry Unity SDK 연동 (선택 의존성)
    ├── AITSentryIntegration.cs   태그·컨텍스트 주입
    ├── AITSentryContextEnricher.cs
    ├── AITSentryAnalytics.cs     화면·노출·클릭 추적
    └── link.xml                  IL2CPP 스트리핑 방지
```

`Runtime/SDK/`는 전부 `sdk-runtime-generator~/`가 만들어 냅니다. 생성 규칙은 [SDK 런타임 생성기](sdk-generator.md)를 참고하세요.

## Editor

```text
Editor/
├── AITConvertCore.cs         빌드 파이프라인 진입점 (Init, DoExport)
├── AITPackageBuilder.cs      WebGL 산출물 → ait-build 변환
├── AITBuildInitializer.cs    PlayerSettings 자동 구성과 복원
├── AITBuildValidator.cs      빌드 전 설정 검증
├── AITBuildSession.cs        빌드 세션 상태
├── AITBuildSessionRecovery.cs  도메인 리로드 후 복원
├── AITConfigurationWindow.cs   설정 창 (AIT > Configuration)
├── AITEditorScriptObject.cs    설정 ScriptableObject와 기본값
├── AITNodeJSDownloader.cs      내장 Node.js 설치
├── AITNpmRunner.cs             패키지 매니저 실행
├── AITTemplateManager.cs       WebGL 템플릿 복사·병합
├── AITAutoUpdater.cs           업데이트 확인과 채널 판정
├── AITExportErrorCatalog.cs    빌드 에러 코드와 안내 문구
├── AppsInTossMenu.cs           AIT 메뉴 등록
├── ErrorTracker/               Sentry 기반 Editor 에러 추적
├── IssueReport/                이슈 리포트 창
├── Menu/                       메뉴 액션 (Local Debug, 배포, 포트 해석)
├── Package/                    granite·pnpm 실행과 설정 병합
│   ├── BuildConfigMerger.cs      플레이스홀더 치환과 사용자 파일 병합
│   ├── GraniteBuildRunner.cs     granite 빌드 실행
│   ├── PnpmInstallStateMarker.cs 설치 스킵 마커
│   └── WebGLBuildCopier.cs
└── Sentry/                     빌드 시 DSN 주입
```

## WebGLTemplates

```text
WebGLTemplates/AITTemplate/
├── index.html                플레이스홀더가 들어 있는 템플릿
├── loading.html              로딩 화면 템플릿
├── Runtime/
│   └── devconsole/                 인앱 디버그 콘솔
├── TemplateData/             스타일과 이미지
└── BuildConfig~/             Vite·granite 빌드 설정
    ├── granite.config.ts
    ├── apps-in-toss.config.ts
    ├── vite.config.ts
    ├── unity-bridge.ts
    └── package.json
```

빌드 시 무엇이 언제 복사·병합되는지는 [빌드 파이프라인](../BuildProcess.md)에 있습니다.

## Tests~

```text
Tests~/E2E/
├── SampleUnityProject-<버전>/  Unity 버전별 샘플 프로젝트 5개
├── SharedScripts/              샘플 프로젝트가 공유하는 UPM 패키지
│   ├── Runtime/                InteractiveAPITester, RuntimeAPITester 등
│   ├── Editor/                 E2EBuildRunner, BuildOutputValidator, EditModeTests
│   └── Plugins/                E2ETestBridge.jslib
└── tests/                      Playwright 테스트와 설정
```

## 관련 문서

- [SDK 런타임 생성기](sdk-generator.md) — `Runtime/SDK/`가 만들어지는 방식
- [테스트 전략](testing.md) — `Tests~/` 구조와 실행
- [빌드 파이프라인](../BuildProcess.md) — `Editor/`와 `WebGLTemplates/`가 실제로 하는 일
- [기여 가이드](../Contributing.md) — 개발 환경과 커밋 규칙
