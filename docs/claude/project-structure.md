# 프로젝트 구조

```
apps-in-toss-unity-sdk/
├── Runtime/                          # 런타임 SDK 코드
│   └── SDK/                          # 자동 생성 SDK API 파일 (카테고리별 partial class)
│       ├── AIT.cs                   # 메인 partial class 선언
│       ├── AIT.Advertising.cs       # 광고 API (GoogleAdMob, TossAds)
│       ├── AIT.Analytics.cs         # 분석 API
│       ├── AIT.AppEvents.cs         # 앱 이벤트 API
│       ├── AIT.Authentication.cs    # 인증 API (AppLogin, GetIsTossLoginIntegratedService)
│       ├── AIT.Certificate.cs       # 인증서 API
│       ├── AIT.Clipboard.cs         # 클립보드 API
│       ├── AIT.Device.cs            # 디바이스 API
│       ├── AIT.Environment.cs       # 환경 API
│       ├── AIT.Events.cs            # 이벤트 API
│       ├── AIT.GameCenter.cs        # 게임센터 API
│       ├── AIT.IAP.cs               # 인앱 결제 API
│       ├── AIT.Location.cs          # 위치 API (GetCurrentLocation, StartUpdateLocation)
│       ├── AIT.Media.cs             # 미디어 API
│       ├── AIT.Navigation.cs        # 네비게이션 API
│       ├── AIT.Other.cs             # 기타 API
│       ├── AIT.Partner.cs           # 파트너 API
│       ├── AIT.Payment.cs           # 결제 API (CheckoutPayment)
│       ├── AIT.Permission.cs        # 권한 API (GetPermission, RequestPermission 등)
│       ├── AIT.SafeArea.cs          # 세이프 에리어 API
│       ├── AIT.Screen.cs            # 화면 API
│       ├── AIT.Share.cs             # 공유 API
│       ├── AIT.Storage.cs           # 스토리지 API
│       ├── AIT.SystemInfo.cs        # 시스템 정보 API (GetDeviceId, GetLocale 등)
│       ├── AIT.Visibility.cs        # 가시성 API
│       ├── AIT.Types.cs             # 타입 정의 (Options, Result 클래스)
│       ├── AITCore.cs               # 인프라 코드 (jslib 브릿지, 예외 처리)
│       └── Plugins/                 # JavaScript 브릿지 (카테고리별 54개 jslib 파일)
├── Editor/                           # Unity Editor 스크립트
│   ├── AITConfigurationWindow.cs    # SDK 설정 윈도우 (메뉴: AIT/Configuration)
│   ├── AITConvertCore.cs            # 빌드 파이프라인 진입점 (DoExport, Init)
│   ├── AITPackageBuilder.cs         # 핵심 패키징 로직 (WebGL→ait-build 변환)
│   ├── AITBuildInitializer.cs       # Unity PlayerSettings 자동 구성
│   ├── AITNodeJSDownloader.cs       # 내장 Node.js 설치기
│   ├── AITNpmRunner.cs              # npm/pnpm 실행 유틸리티
│   ├── AITEditorScriptObject.cs     # 설정 ScriptableObject
│   ├── AppsInTossMenu.cs            # Unity 메뉴 등록 (AIT/)
│   └── ErrorTracker/                # Sentry 기반 에러 추적
├── WebGLTemplates/                   # Unity WebGL 템플릿
│   └── AITTemplate/                  # Unity 2021.3+ 템플릿
│       ├── index.html               # 플레이스홀더가 포함된 템플릿 HTML
│       ├── Runtime/                 # 플랫폼 브릿지 JavaScript
│       │   └── appsintoss-unity-bridge.js
│       └── BuildConfig~/            # Vite 빌드 설정
│           ├── package.json         # npm 의존성
│           ├── vite.config.ts       # Vite 설정
│           └── granite.config.ts    # Granite 빌드 설정
├── Tools~/                           # 개발 도구 (UPM에서 제외)
│   └── NodeJS/                      # 내장 Node.js (자동 다운로드)
├── Tests~/                           # 테스트 파일 (UPM에서 제외)
│   └── E2E/                         # E2E 테스트
│       ├── SampleUnityProject-6000.3/  # Unity 6000.3 테스트 프로젝트
│       ├── SampleUnityProject-6000.2/  # Unity 6000.2 테스트 프로젝트
│       ├── SampleUnityProject-6000.0/  # Unity 6000.0 테스트 프로젝트
│       ├── SampleUnityProject-2022.3/  # Unity 2022.3 테스트 프로젝트
│       ├── SampleUnityProject-2021.3/  # Unity 2021.3 테스트 프로젝트
│       ├── SharedScripts/              # 공유 테스트 스크립트 (UPM 패키지)
│       │   ├── Runtime/               # InteractiveAPITester, RuntimeAPITester 등
│       │   └── Editor/                # E2EBuildRunner
│       └── tests/                     # Playwright E2E 테스트
└── sdk-runtime-generator~/           # SDK 코드 생성기 (UPM에서 제외)
```

**참고**: `~` 접미사가 붙은 디렉토리는 Unity Package Manager 배포에서 제외됩니다.
