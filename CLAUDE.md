# CLAUDE.md

이 파일은 Claude Code (claude.ai/code)가 이 저장소의 코드를 다룰 때 참고하는 가이드입니다.

## ⚠️ 필수 규칙

### Git 커밋 가이드라인
- **모든 커밋 메시지는 반드시 한국어로 작성**
- 커밋 메시지 형식: `<타입>: <설명>`
  - 타입 예시: 기능, 수정, 개선, 문서, 리팩토링, 테스트, 빌드
- 예시:
  - ✅ `기능: 사용자 인증 API 추가`
  - ✅ `수정: WebGL 빌드 오류 해결`
  - ❌ `feat: Add user authentication API` (영어 - 허용 안됨)

### 문서 생성 정책
- **사용자의 명시적 허락 없이 *.md 파일을 생성하거나 수정하지 말 것**
- 해당 파일:
  - README.md, CHANGELOG.md, CONTRIBUTING.md
  - PRD.md, USER_GUIDE.md, API.md
  - 기타 모든 마크다운 문서 파일
- **예외**: 명시적으로 지시받은 경우 CLAUDE.md 수정 가능
- **이유**: AI가 생성한 문서는 기업 저장소에 부적절하게 보일 수 있음

### 저장소 소유권
- 이 저장소는 **기업 소유 비공개 저장소** (오픈소스 아님)
- LICENSE 파일이나 오픈소스 라이선스 정보 추가 금지
- package.json "license" 필드 추가 금지

### 자동 생성 코드 정책
- **`Runtime/SDK/` 디렉토리의 파일을 직접 수정하지 말 것**
- `Runtime/SDK/`의 모든 파일은 `sdk-runtime-generator~/`에서 자동 생성됨
- 버그 수정이나 변경이 필요한 경우:
  1. `sdk-runtime-generator~/`의 생성기 코드 수정
  2. `pnpm run generate`로 SDK 파일 재생성
  3. `./run-local-tests.sh --all`로 변경사항 검증
- 생성된 파일을 직접 수정하면 다음 생성 시 덮어씌워짐

### 파일 위생
- 불필요한 파일 발견 시 **적극적으로 .gitignore에 추가**
- 무시해야 할 일반적인 파일:
  - Unity: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`
  - 빌드 산출물: `ait-build/`, `webgl/`, `dist/`, `node_modules/`
  - 로그: `*.log`, `*.log.meta`
  - IDE: `.idea/`, `.vscode/`, `*.swp`
- 커밋하면 안 되는 추적되지 않은 파일 발견 시 적절한 `.gitignore` 파일에 추가

## 자주 하는 실수 방지

### SDK 생성기 관련
- `Runtime/SDK/` 파일을 직접 수정하면 다음 `pnpm generate` 시 덮어씌워짐
- SDK API 변경이 필요하면 반드시 `sdk-runtime-generator~/` 내 코드를 수정할 것
- 생성기 수정 후 검증 순서: `pnpm generate` → `pnpm validate` → `pnpm test`

### GitHub Actions 관련
- `gh workflow run` 명령어 사용 불가 (GraphQL 차단) — REST API 사용 필수
- 워크플로우 트리거 시 반드시 아래 워크플로우 ID 참조 테이블 확인
- PR 번호 사용 시 `target_ref`에 숫자만 입력 (# 접두사 불필요)

### 테스트 관련
- E2E 테스트 전 빌드가 필요함: `./run-local-tests.sh --all` (빌드+테스트) vs `--e2e` (테스트만)
- Playwright 테스트는 `Tests~/E2E/tests/` 디렉토리에서 실행
- Level 0 테스트(EditMode)는 빌드 없이 ~10초만에 실행 가능

## 빠른 참조: 주요 명령어

| 작업 | 명령어 |
|------|--------|
| SDK 재생성 | `cd sdk-runtime-generator~ && pnpm generate` |
| SDK 검증 | `cd sdk-runtime-generator~ && pnpm validate` |
| SDK 테스트 | `cd sdk-runtime-generator~ && pnpm test` |
| 전체 로컬 테스트 | `./run-local-tests.sh --all` |
| 빠른 검증만 | `./run-local-tests.sh --validate` |
| E2E만 | `./run-local-tests.sh --e2e` |
| CI 트리거 (E2E) | 아래 GitHub Actions 섹션 참조 |

## 개요

**Apps in Toss Unity SDK** - Unity/Tuanjie 엔진 게임 프로젝트를 Apps in Toss 플랫폼의 미니앱으로 변환하고 배포할 수 있게 해주는 Unity 패키지입니다. SDK 제공 기능:

- Apps in Toss 플랫폼에 최적화된 WebGL 빌드 자동화
- 커스텀 Build & Deploy 윈도우를 통한 Unity Editor 통합
- 플랫폼별 브릿지 코드가 포함된 커스텀 WebGL 템플릿 (AITTemplate)
- granite 빌드 시스템을 사용한 Vite 기반 빌드 파이프라인
- 플랫폼 기능을 위한 종합 C# API (결제, 사용자 인증, 기기 API 등)

## 프로젝트 구조

```
apps-in-toss-unity-sdk/
├── Runtime/                          # 런타임 SDK 코드
│   └── SDK/                          # 자동 생성 SDK API 파일 (카테고리별 partial class)
│       ├── AIT.cs                   # 메인 partial class 선언
│       ├── AIT.Authentication.cs    # 인증 API (AppLogin, GetIsTossLoginIntegratedService)
│       ├── AIT.Payment.cs           # 결제 API (CheckoutPayment)
│       ├── AIT.SystemInfo.cs        # 시스템 정보 API (GetDeviceId, GetLocale 등)
│       ├── AIT.Location.cs          # 위치 API (GetCurrentLocation, StartUpdateLocation)
│       ├── AIT.Permission.cs        # 권한 API (GetPermission, RequestPermission 등)
│       ├── AIT.GameCenter.cs        # 게임센터 API
│       ├── AIT.Share.cs             # 공유 API
│       ├── AIT.Media.cs             # 미디어 API
│       ├── AIT.Clipboard.cs         # 클립보드 API
│       ├── AIT.Device.cs            # 디바이스 API
│       ├── AIT.Navigation.cs        # 네비게이션 API
│       ├── AIT.Events.cs            # 이벤트 API
│       ├── AIT.Certificate.cs       # 인증서 API
│       ├── AIT.Visibility.cs        # 가시성 API
│       ├── AIT.Types.cs             # 타입 정의 (Options, Result 클래스)
│       ├── AITCore.cs               # 인프라 코드 (jslib 브릿지, 예외 처리)
│       └── Plugins/
│           └── AITBridge.jslib      # JavaScript 브릿지
├── Editor/                           # Unity Editor 스크립트
│   ├── AppsInTossBuildWindow.cs     # 메인 빌드 & 배포 UI 윈도우
│   ├── AITConvertCore.cs            # 핵심 빌드 파이프라인 로직
│   ├── AITNodeJSDownloader.cs       # 내장 Node.js 설치기
│   └── AITEditorScriptObject.cs     # 설정 ScriptableObject
├── WebGLTemplates/                   # Unity WebGL 템플릿
│   └── AITTemplate/                  # Unity 2021.3+ 템플릿
│       ├── index.html               # 플레이스홀더가 포함된 템플릿 HTML
│       ├── Runtime/                 # 플랫폼 브릿지 JavaScript
│       │   └── appsintoss-unity-bridge.js
│       └── BuildConfig/             # Vite 빌드 설정
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

## 빌드 파이프라인 아키텍처

SDK는 **2단계 빌드 시스템**을 사용합니다:

### 1단계: Unity WebGL 빌드
1. `AITConvertCore.Init()`이 Unity PlayerSettings 구성:
   - WebGL 템플릿을 `PROJECT:AITTemplate`으로 설정
   - 압축 비활성화 (Apps in Toss 요구사항)
   - 모바일 브라우저용 메모리 및 스레딩 최적화
2. `BuildWebGL()`이 Unity의 BuildPipeline을 `webgl/` 폴더로 실행
3. 커스텀 템플릿이 적용된 표준 Unity WebGL 출력 생성

### 2단계: Granite 빌드 (패키징)
1. `PackageWebGLBuild()`가 `ait-build/` 디렉토리 생성
2. `WebGLTemplates/AITTemplate/BuildConfig/`에서 **BuildConfig 복사**:
   - `package.json`, `vite.config.ts`, `tsconfig.json` (치환 없음)
   - `granite.config.ts` (앱 메타데이터 플레이스홀더 치환 적용)
3. **WebGL 빌드를 적절한 구조로 복사**:
   - `index.html` → 프로젝트 루트 (Unity 플레이스홀더 치환 적용)
   - `Build/`, `TemplateData/`, `Runtime/` → `public/` 폴더
4. **npm install 실행** (`node_modules/`가 없는 경우에만)
5. **`npm run build` 실행** (`granite build` 실행):
   - `ait-build/dist/`에 최종 배포 가능 패키지 생성

### 플레이스홀더 치환

빌드 중 두 가지 유형의 플레이스홀더가 치환됩니다:

**index.html (Unity 플레이스홀더):**
- `%UNITY_WEB_NAME%`, `%UNITY_COMPANY_NAME%` 등 → Unity PlayerSettings 값
- `%UNITY_WEBGL_LOADER_FILENAME%` 등 → 실제 WebGL 빌드 파일명
- `%AIT_IS_PRODUCTION%` → 설정에 따라 "true" 또는 "false"

**granite.config.ts (Apps in Toss 플레이스홀더):**
- `%AIT_APP_NAME%` → 설정의 앱 ID
- `%AIT_DISPLAY_NAME%` → 설정의 표시 이름
- `%AIT_PRIMARY_COLOR%` → 설정의 브랜드 색상
- `%AIT_ICON_URL%` → 앱 아이콘 URL (필수 필드)
- `%AIT_LOCAL_PORT%` → 개발 서버 포트

## 주요 명령어

### 개발
SDK는 Unity 패키지입니다 - 전통적인 빌드/테스트 명령어가 없습니다. 개발은 Unity Editor 내에서 진행:

1. **Unity에서 열기**: Package Manager를 통해 Unity 프로젝트에 패키지 로드
2. **빌드 윈도우 접근**: Unity 메뉴 `Apps in Toss > Build & Deploy Window`
3. **빌드 실행**: 윈도우에서 "🚀 Build & Package" 클릭

### 빌드 파이프라인 테스트
Unity 없이 빌드 파이프라인 테스트:

```bash
# 빌드 출력으로 이동
cd ait-build/

# 의존성 설치 (최초 1회만)
npm install

# 개발 서버 실행
npm run dev

# 프로덕션 빌드
npm run build

# Apps in Toss에 배포
npm run deploy
```

### Unity 버전 요구사항

**우선 지원 버전:**
- Unity 6000.3.0f1 (Unity 6.3)
- Unity 6000.2.14f1 (Unity 6 LTS)
- Unity 6000.0.63f1 (Unity 6)
- Unity 2022.3.62f3 (LTS)
- Unity 2021.3.45f2 (LTS - 최소 지원 버전)

모든 개발, 테스트, QA는 이 5개 버전을 우선으로 진행해야 합니다.

- Tuanjie Engine 지원

**중요**: SDK는 Unity 2021.3 이상 모든 버전을 지원해야 합니다.

## 주요 구현 세부사항

### WebGL 템플릿 시스템
Unity WebGL 템플릿은 `WebGLTemplates/AITTemplate/`에 있습니다. 템플릿은 자동으로:
1. 최초 사용 시 Unity 프로젝트의 `Assets/WebGLTemplates/`로 복사
2. PlayerSettings에서 `PROJECT:AITTemplate`으로 선택
3. WebGL 빌드 시 Apps in Toss 브릿지 코드 주입에 사용

### 빌드 설정 자동 구성
`AITConvertCore.Init()`이 최적 설정을 자동 구성:
- **압축**: 비활성화 (Apps in Toss 요구사항)
- **스레딩**: 비활성화 (모바일 브라우저 호환성)
- **메모리**: Unity 2022.3은 512MB, Unity 6는 1024MB
- **데이터 캐싱**: 비활성화
- **링커 타겟**: Wasm

### 내장 Node.js 시스템
SDK는 자동 다운로드 기능이 있는 내장 Node.js 사용:
1. 내장 Node.js를 강제 사용 (시스템 설치 무시)
2. `~/.ait-unity-sdk/nodejs/v{VERSION}/{platform}/`에 자동 다운로드
3. SHA256 체크섬 검증 후 설치

**다운로드 소스 (폴백 순서):**
1. https://nodejs.org (공식)
2. https://cdn.npmmirror.com
3. https://repo.huaweicloud.com

**Node.js 버전**: v24.13.0 (체크섬은 `Editor/AITNodeJSDownloader.cs`에 있음)

### 설정 저장소
설정은 `Assets/AppsInToss/Editor/AITConfig.asset` (ScriptableObject)에 저장:
- 앱 메타데이터 (이름, 버전, 설명, 아이콘 URL)
- 빌드 설정 (프로덕션 모드, 최적화 플래그)
- 브랜딩 (기본 색상, 아이콘 URL)
- 배포 키 (`ait deploy`용)

**중요**: 아이콘 URL (`config.iconUrl`)은 빌드에 필수입니다.

## SDK 런타임 생성기

`@apps-in-toss/web-framework`의 TypeScript 정의에서 C# SDK 코드를 자동 생성합니다.

### 워크플로우
```bash
cd sdk-runtime-generator~
pnpm install
pnpm generate   # TypeScript → C# + JavaScript 브릿지 (~1.5초)
pnpm format     # CSharpier 포맷팅 (선택, dotnet 필요)
pnpm validate   # Mono mcs로 컴파일 검사
pnpm test       # 유닛 테스트 실행
```

### 타입 매핑
| TypeScript | C# |
|------------|-----|
| `string` | `string` |
| `number` | `double` |
| `boolean` | `bool` |
| `Promise<T>` | `Task<T>` (async/await 패턴) |
| `T \| U` | Discriminated union class |

### API 사용 패턴
SDK API는 async/await 패턴을 사용하며, 에러 발생 시 `AITException`을 throw합니다:

```csharp
try
{
    string deviceId = await AIT.GetDeviceId();
    PlatformOS os = await AIT.GetPlatformOS();
}
catch (AITException ex)
{
    Debug.LogError($"API 호출 실패: {ex.Message} (code: {ex.Code})");
}
```

## 테스트 전략

### E2E 테스트 구조
SDK는 5개 Unity 버전에 대해 E2E 테스트를 실행합니다:

```
Tests~/E2E/
├── SampleUnityProject-6000.3/    # Unity 6000.3 테스트 프로젝트
├── SampleUnityProject-6000.2/    # Unity 6000.2 테스트 프로젝트
├── SampleUnityProject-6000.0/    # Unity 6000.0 테스트 프로젝트
├── SampleUnityProject-2022.3/    # Unity 2022.3 테스트 프로젝트
├── SampleUnityProject-2021.3/    # Unity 2021.3 테스트 프로젝트
├── SharedScripts/                # 공유 테스트 스크립트 (UPM 패키지)
│   ├── Runtime/
│   │   ├── InteractiveAPITester.cs   # 대화형 API 테스터 (WebGL UI)
│   │   ├── RuntimeAPITester.cs       # 자동 API 테스트 러너
│   │   ├── APIParameterInspector.cs  # API 리플렉션 유틸리티
│   │   └── E2EBootstrapper.cs        # 런타임 컴포넌트 초기화
│   ├── Editor/
│   │   ├── E2EBuildRunner.cs         # CLI 빌드 자동화
│   │   ├── BuildOutputValidator.cs   # 빌드 산출물 검증 (Level 1)
│   │   └── EditModeTests/            # EditMode 테스트 (Level 0)
│   └── Plugins/
│       └── E2ETestBridge.jslib       # WebGL JS↔C# 브릿지
└── tests/
    ├── e2e-full-pipeline.test.js     # Playwright E2E 테스트 (5 tests)
    └── playwright.config.ts
```

### 테스트 모드
- **E2E 모드** (`?e2e=true`): 자동 벤치마크 + API 테스트 실행
- **Interactive 모드** (기본): 대화형 API 테스터 UI 표시

### 테스트 실행

**로컬 테스트 러너:**
```bash
./run-local-tests.sh --help     # 옵션 보기
./run-local-tests.sh --validate # 빠른 검증만
./run-local-tests.sh --all      # 전체 Unity 빌드 + E2E 테스트
./run-local-tests.sh --e2e      # E2E 테스트만 (기존 빌드 필요)
```

**수동 Playwright 실행:**
```bash
cd Tests~/E2E/tests
npm install
npx playwright install chromium
npm test
```

### CI/CD 통합

`.github/workflows/test-e2e.yml` 실행 내용 (3-Level 테스트 구조):
1. EditMode 테스트 (Level 0: C# 순수 로직 검증, 빌드 없이 ~10초)
2. Unity WebGL 빌드 + C# 빌드 산출물 검증 (Level 1)
3. Playwright E2E 테스트 (Level 2: 5개 테스트)
4. 벤치마크 결과 아티팩트로 업로드
5. 성능 메트릭이 포함된 Job summary

`test-level` 파라미터로 실행 범위 제어:
- `0`: EditMode만 (~10초)
- `1`: 빌드+검증, Playwright 스킵 (~8분)
- `2`: 전체 실행 (~14분, 기본값)

## GitHub Actions 워크플로우

### 워크플로우 목록

| 워크플로우 | 트리거 | 용도 |
|-----------|--------|------|
| **E2E Tests** | 수동, workflow_call | Unity WebGL 빌드 + Playwright E2E 테스트 |
| **Preview** | 수동 | PR 브랜치를 빌드하여 미리보기 배포 |
| **Validate** | push, PR | SDK Generator 유닛 테스트, Unity .meta 파일 검사 |
| **Lint** | push, PR | Unity .meta 파일 누락 검사 |
| **Release** | 수동, push(main), workflow_call | npm 패키지 릴리즈 |
| **SDK Update** | 수동, 스케줄(평일 9시) | @apps-in-toss/web-framework 버전 동기화 |
| **Bulk Release** | 수동 | 여러 버전 일괄 릴리즈 |
| **Unity Build** | workflow_call 전용 | 다른 워크플로우에서 호출하는 빌드 모듈 |

### 워크플로우 트리거 방법

**⚠️ 중요: gh CLI의 GraphQL API가 차단되어 있으므로 REST API 사용 필수**

#### E2E Tests
```bash
# PR 번호로 트리거 (권장 - PR 코멘트에 결과 자동 게시)
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123"
  }
}
EOF

# 브랜치로 트리거
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<EOF
{
  "ref": "feature-branch"
}
EOF

# Library 캐시 정리 옵션
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "clean_library": "true"
  }
}
EOF
```

#### Preview
```bash
# 단일 타겟
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216269700/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "targets": "macos-6000.2"
  }
}
EOF

# ✅ 여러 타겟 동시 빌드 (쉼표로 구분)
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216269700/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "targets": "macos-6000.3,macos-6000.2,macos-6000.0,macos-2022.3,macos-2021.3"
  }
}
EOF
```

**지원 타겟 형식**: `{os}-{unity-version}` (예: `macos-6000.2`, `windows-2021.3`)

#### Release
```bash
# 특정 버전 릴리즈
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934317/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0"
  }
}
EOF
```

#### SDK Update
```bash
# 특정 버전으로 업데이트
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0"
  }
}
EOF

# 누락된 모든 버전 자동 감지 및 업데이트
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main"
}
EOF

# 강제 업데이트 (이미 같은 버전이 있어도)
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0",
    "force": "true"
  }
}
EOF
```

#### Bulk Release
```bash
# 특정 버전들 일괄 릴리즈
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/222574658/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "versions": "1.5.0,1.6.0,1.7.0",
    "max_parallel": "2"
  }
}
EOF

# 모든 release/v* 태그 대상 릴리즈
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/222574658/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main"
}
EOF
```

#### Validate / Lint
자동 트리거 (push, PR)만 지원. 수동 트리거 불가.

### 워크플로우 상태 확인

```bash
# 최근 실행 목록
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs \
  --jq '.workflow_runs[:10] | .[] | "\(.id) \(.name) \(.status) \(.conclusion)"'

# 특정 실행 상태 확인
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs/RUN_ID \
  --jq '"\(.name): \(.status) / \(.conclusion)"'

# 실행 중인 워크플로우만
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs \
  --jq '.workflow_runs[] | select(.status == "in_progress" or .status == "queued") | "\(.id) \(.name) \(.status)"'
```

### 워크플로우 ID 참조

| 워크플로우 | ID |
|-----------|-----|
| E2E Tests | 216286654 |
| Preview | 216269700 |
| Release | 214934317 |
| SDK Update | 214934319 |
| Bulk Release | 222574658 |
| Unity Build | 216269701 |
| Validate | 216278800 |
| Lint | 214934316 |

### 주의사항

1. **PR 번호 사용 권장**: `target_ref`에 PR 번호를 사용하면 결과가 PR 코멘트로 자동 게시됨
2. **Preview 다중 타겟**: 여러 Unity 버전을 빌드할 때 쉼표로 구분하여 한 번에 트리거 (N번 호출 금지)
3. **concurrency 그룹**: 같은 PR에 대해 동시 실행 시 이전 실행이 취소될 수 있음
4. **GraphQL 차단**: `gh workflow run` 명령 사용 불가, REST API 사용 필수
