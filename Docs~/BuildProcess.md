# 빌드 프로세스 상세 가이드

이 문서는 Apps in Toss Unity SDK의 전체 빌드 파이프라인을 상세히 설명합니다.

---

## 목차

1. [빌드 파이프라인 개요](#빌드-파이프라인-개요)
2. [Phase 0: 초기화 (Build Initialization)](#phase-0-초기화)
3. [Phase 1: WebGL 빌드](#phase-1-webgl-빌드)
4. [Phase 2: 패키징 (Transform + Package)](#phase-2-패키징)
5. [파일 시그니처 탐지 및 검증](#파일-시그니처-탐지-및-검증)
6. [플레이스홀더 치환 시스템](#플레이스홀더-치환-시스템)
7. [로딩 화면 시스템](#로딩-화면-시스템)
8. [템플릿 관리 (마커 기반 병합)](#템플릿-관리)
9. [Node.js / pnpm 관리](#nodejs--pnpm-관리)
10. [에러 코드 및 메시지](#에러-코드-및-메시지)
11. [사용자 경고 및 다이얼로그 조건](#사용자-경고-및-다이얼로그-조건)
12. [서버 라이프사이클](#서버-라이프사이클)
13. [에러 리포팅](#에러-리포팅)
14. [환경 변수 오버라이드](#환경-변수-오버라이드)
15. [개선 가능 영역](#개선-가능-영역)

---

## 빌드 파이프라인 개요

빌드는 3개의 주요 단계로 구성됩니다:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Entry Points                                 │
│  Menu: Build & Package  │  Build Window  │  Server Start/Restart    │
│  AppsInTossMenu.cs      │  AppsInTossBuildWindow.cs                 │
└─────────────┬───────────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  AITConvertCore.DoExport(buildWebGL, doPackaging, cleanBuild,      │
│                          profile, profileName)                      │
│  또는 DoExportAsync(...)                                           │
└─────────────┬───────────────────────────────────────────────────────┘
              │
     ┌────────┴────────────────────────────────┐
     ▼                                         ▼
┌──────────────────────┐          ┌────────────────────────────────┐
│  Phase 1: WebGL 빌드 │          │  Phase 2: 패키징               │
│  BuildWebGL()         │          │  GenerateMiniAppPackage()      │
│                       │          │  → AITPackageBuilder            │
│  - Init()             │          │    .PackageWebGLBuild()        │
│  - BuildPipeline      │          │                                │
│  - .ait-build-info    │          │  2a. BuildConfig 복사          │
│                       │          │  2b. WebGL→public 복사         │
│  산출물: webgl/       │          │  2c. 플레이스홀더 치환         │
│                       │          │  2d. 로딩 화면 삽입            │
│                       │          │  2e. pnpm install              │
│                       │          │  2f. granite build             │
│                       │          │                                │
│                       │          │  산출물: ait-build/dist/       │
└──────────────────────┘          └────────────────────────────────┘
```

### 호출 매트릭스

| 진입점 | buildWebGL | doPackaging | cleanBuild |
|--------|-----------|------------|-----------|
| `Build & Package` | `true` | `true` | `false` |
| `Build & Package (clean)` | `true` | `true` | `true` |
| `Publish` (재빌드) | `true` | `true` | `true` |
| `Dev Server Start` | `true` | `true` | `false` |
| `Prod Server Start` | `true` | `true` | `false` |
| `Restart Server` | `true` | `true` | `false` |
| `Restart (server-only)` | — | — | — |
| ~~`Build (deprecated)`~~ | 경고 → `Build & Package` 유도 |
| ~~`Package (deprecated)`~~ | 경고 → `Build & Package` 유도 |

> **참고**: `Restart (server-only)`는 `DoExport`를 호출하지 않고 granite 프로세스만 재시작합니다.

---

## Phase 0: 초기화

### 템플릿 동기화 (`AITTemplateManager.EnsureWebGLTemplatesExist`)

빌드 전에 SDK의 WebGL 템플릿을 프로젝트로 복사합니다.

**SDK 템플릿 검색 순서:**
1. `Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/`
2. `Packages/com.appsintoss.miniapp/WebGLTemplates/`
3. Assembly 경로 기반 (`typeof(AITConvertCore).Assembly.Location` 상위)

**동작:**
- 프로젝트에 `Assets/WebGLTemplates/AITTemplate/`이 없으면 → 전체 복사
- 있으면 → 마커 기반 업데이트 (사용자 커스텀 영역 보존, [상세](#템플릿-관리) 참조)

### 빌드 설정 (`AITBuildInitializer.Init`)

Unity PlayerSettings를 자동 구성합니다:

| 설정 | 값 | 비고 |
|------|---|------|
| WebGL Template | `PROJECT:AITTemplate` | 하드코딩 |
| Linker Target | `Wasm` | 하드코딩 |
| Scripting Backend | `IL2CPP` | 하드코딩 |
| Memory Size | 256~1536MB | Unity 버전별 기본값 (사용자 오버라이드 가능) |
| Compression | `Disabled` | 기본값 (Apps in Toss 요구사항) |
| Threading | `false` | 기본값 (모바일 브라우저 호환성) |
| Data Caching | `false` | 기본값 |
| `nameFilesAsHashes` | `false` | Unity 2021.x에서 강제 비활성화 + 경고 |
| Engine Code Stripping | 사용자 설정 | — |
| Managed Stripping | `High` | 기본값 |
| IL2CPP Config | 사용자 설정 | — |

**버전별 기본 메모리:**
- Unity 2021.3: 256MB
- Unity 2022.3: 512MB
- Unity 6 (2023.3+): 1024MB
- Unity 2024.2+: 1536MB

### Config 검증

빌드 전 `AITEditorScriptObject` (config)를 검증합니다:

- `config.appName`이 비어있으면 → `INVALID_APP_CONFIG` 에러
- `config.iconUrl`이 비어있으면 → `INVALID_APP_CONFIG` 에러 (필수 필드)

---

## Phase 1: WebGL 빌드

`AITConvertCore.BuildWebGL()`

### 실행 흐름

```
1. AITBuildInitializer.Init(profile)
   ├── PlayerSettings 자동 구성
   ├── 환경 변수 오버라이드 적용
   └── 빌드 프로필 로그 출력

2. cleanBuild인 경우:
   └── webgl/ 디렉토리 삭제

3. BuildPipeline.BuildPlayer()
   ├── scenes: EditorBuildSettings.scenes (체크된 것만)
   ├── locationPathName: "{projectPath}/webgl"
   ├── target: WebGL
   └── options: BuildOptions.None (cleanBuild면 BuildOptions.CleanBuildCache 추가)

4. BuildReport 검사
   ├── 성공 → .ait-build-info.json 작성
   └── 실패 → AITErrorReporter.SetBuildReport(report) + 에러 반환

5. 빌드 마커 작성: webgl/.ait-build-info.json
```

### 빌드 마커 (`.ait-build-info.json`)

성공적인 WebGL 빌드 후 `webgl/.ait-build-info.json`에 메타데이터를 기록합니다:

```json
{
    "sdkVersion": "1.7.0",
    "buildTime": "2024-03-01T12:00:00.0000000Z",
    "compressionFormat": 0,
    "profileName": "Production",
    "unityVersion": "6000.2.15f1"
}
```

| 필드 | 설명 |
|------|------|
| `sdkVersion` | SDK 패키지 버전 |
| `buildTime` | UTC ISO 8601 빌드 시각 |
| `compressionFormat` | `PlayerSettings.WebGL.compressionFormat` int 값 (0=Disabled) |
| `profileName` | "Development" 또는 "Production" |
| `unityVersion` | `Application.unityVersion` |

`ReadBuildMarker(webglPath)` 메서드로 마커를 읽어 `AITBuildInfo`를 반환합니다. 파일이 없거나 파싱 실패 시 `null`을 반환합니다.

**마커 활용처:**
- **빌드 캐시 검증** (`ShouldForceCleanBuild`): Unity 버전 불일치, 마커 없음 → 자동 clean build
- **압축 포맷 탐지** (`CopyWebGLToPublic`): `compressionFormat` 값으로 정확한 확장자 탐지

### 빌드 캐시 유효성 검증

`ShouldForceCleanBuild(outputPath, cleanBuild)`는 WebGL 빌드 전에 기존 빌드 캐시를 검증합니다:

```
1. cleanBuild=true → 무조건 clean build
2. webgl/ 폴더 없음 → 새 빌드 (clean 불필요)
3. 빌드 마커 없음 → clean build (이전 SDK 버전 또는 손상)
4. Unity 버전 불일치 → clean build (빌드 결과물 호환 보장)
5. Build/*.loader.js 없음 → clean build (필수 파일 누락)
6. 모두 통과 → 증분 빌드
```

### 증분 빌드 (Incremental Build)

Unity의 `BuildPipeline`은 기본적으로 증분 빌드를 수행합니다:
- `webgl/` 폴더가 존재하면 변경된 에셋만 재빌드
- `cleanBuild=true`이면 `webgl/` 삭제 후 전체 빌드 (`BuildOptions.CleanBuildCache`)

---

## Phase 2: 패키징

`AITPackageBuilder.PackageWebGLBuild()` (동기) 또는 `PackageWebGLBuildAsync()` (비동기)

동기/비동기 경로는 `PreparePackaging()`으로 공통 준비 로직을 공유합니다.

### 전체 흐름

```
PreparePackaging() ← 동기/비동기 공통
├── Node.js/pnpm 설치 대기
├── ait-build/ 디렉토리 생성
├── CopyBuildConfigFromTemplate()
│   ├── SDK BuildConfig/ → ait-build/ 복사
│   │   ├── package.json (dependencies 머지)
│   │   ├── vite.config.ts (호스트/포트 치환)
│   │   ├── granite.config.ts (앱 메타데이터 치환)
│   │   └── tsconfig.json (compilerOptions 머지)
│   └── pnpm-lock.yaml 복사
├── CopyWebGLToPublic()
│   ├── webgl/Build/ 검증 (압축 포맷별 파일 탐지)
│   ├── Build → ait-build/public/Build/ 복사
│   ├── TemplateData → ait-build/public/TemplateData/ 복사
│   ├── Runtime → ait-build/public/Runtime/ 복사
│   ├── index.html 플레이스홀더 치환 → ait-build/index.html
│   ├── 로딩 화면 삽입
│   ├── 브릿지 JS 플레이스홀더 치환
│   └── 플레이스홀더 검증
├── pnpm 경로 확인
└── ValidateNodeModulesIntegrity()

동기 경로: RunPnpmInstallSync() → RunGraniteBuildSync()
비동기 경로: RunPnpmInstallAsync() → RunGraniteBuildAsync()

pnpm install (PnpmInstallStages 배열 기반 3단계 재시도)
├── 1차: pnpm install --frozen-lockfile
├── 2차: pnpm install --no-frozen-lockfile
└── 3차: node_modules 삭제 + pnpm install --no-frozen-lockfile

granite build
├── 1차: pnpm run build
└── 실패 시: CleanNodeModules → install → build 재시도

산출물: ait-build/dist/
```

### 2a. BuildConfig 복사 (`CopyBuildConfigFromTemplate`)

SDK의 `WebGLTemplates/AITTemplate/BuildConfig~/` 내 파일들을 `ait-build/`로 복사합니다.

**복사 대상:**
- `package.json` — 그대로 복사
- `tsconfig.json` — 그대로 복사
- `vite.config.ts` — `%AIT_VITE_HOST%`, `%AIT_VITE_PORT%` 치환
- `granite.config.ts` — 12개 플레이스홀더 치환 ([상세](#graniteconfgts-치환))
- `pnpm-lock.yaml` — 있으면 복사

### 2b. WebGL→public 복사 (`CopyWebGLToPublic`)

`webgl/` 빌드 산출물을 `ait-build/` 구조로 재배치합니다.

```
webgl/
├── Build/
│   ├── webgl.loader.js          → ait-build/public/Build/
│   ├── webgl.data               → ait-build/public/Build/
│   ├── webgl.framework.js       → ait-build/public/Build/
│   └── webgl.wasm               → ait-build/public/Build/
├── TemplateData/                → ait-build/public/TemplateData/
├── Runtime/                     → ait-build/public/Runtime/
└── index.html                   → ait-build/index.html (치환 후)
```

### 2c~2d. 플레이스홀더 치환 + 로딩 화면

[플레이스홀더 치환 시스템](#플레이스홀더-치환-시스템) 및 [로딩 화면 시스템](#로딩-화면-시스템) 참조.

### 2e. pnpm install (3단계 재시도 전략)

```
┌──────────────────────────────────────┐
│  ValidateNodeModulesIntegrity()      │
│  web-framework 버전 불일치?          │
│  → node_modules 삭제 후 재설치       │
└─────────────┬────────────────────────┘
              │
              ▼
┌──────────────────────────────────────┐
│  1차: pnpm install --frozen-lockfile │  ← 가장 빠름 (lockfile 변경 없음)
│  성공? → 완료                        │
│  실패? ↓                             │
├──────────────────────────────────────┤
│  2차: pnpm install                   │  ← lockfile 업데이트 허용
│       --no-frozen-lockfile           │
│  성공? → 완료                        │
│  실패? ↓                             │
├──────────────────────────────────────┤
│  3차: CleanNodeModules()             │  ← node_modules + .npm-cache 삭제
│       + pnpm install                 │
│         --no-frozen-lockfile         │
│  성공? → 완료                        │
│  실패? → FAIL_NPM_BUILD 에러        │
└──────────────────────────────────────┘
```

**`ValidateNodeModulesIntegrity()` 검증 로직:**
1. `node_modules/`가 없으면 → 유효 (새로 설치됨)
2. `node_modules/.pnpm/` 디렉토리가 없으면 → 무효 (stale modules)
3. `package.json`에서 `@apps-in-toss/web-framework` 버전 추출
4. `node_modules/.pnpm/@apps-in-toss+web-framework@{version}*/` 디렉토리 존재 확인
   - 버전 불일치 시 → `Debug.LogWarning` + 무효 처리
   - 해당 패키지 없음 → `Debug.LogWarning` + 무효 처리

### 2f. granite build

```bash
pnpm run build   # → granite build 실행
```

**granite build 실패 시 재시도:**
1. `CleanNodeModules()` → `pnpm install --no-frozen-lockfile` → `pnpm run build`
2. 재실패 → `FAIL_NPM_BUILD` 에러 반환

**산출물:** `ait-build/dist/` (배포 가능한 최종 패키지)

---

## 파일 시그니처 탐지 및 검증

`AITBuildValidator` 클래스가 WebGL 빌드 산출물의 존재 및 무결성을 검증합니다.

### `GetFilePatterns(compressionFormat)`

빌드 마커의 `compressionFormat` 값에 따라 정확한 파일 검색 패턴을 반환합니다.

| compressionFormat | 의미 | data 패턴 | framework 패턴 | wasm 패턴 |
|---|---|---|---|---|
| `0` | Disabled | `*.data` | `*.framework.js` | `*.wasm` |
| `1` | Gzip | `*.data.gz` | `*.framework.js.gz` | `*.wasm.gz` |
| `2` | Brotli | `*.data.br` | `*.framework.js.br` | `*.wasm.br` |
| `-1` (기본) | 폴백 | `*.data*` | `*.framework.js*` | `*.wasm*` |

> **참고**: loader.js는 압축 설정과 무관하게 항상 `*.loader.js` 패턴으로 검색됩니다.

**탐지 흐름:**
1. 빌드 마커에서 `compressionFormat` 읽기 (없으면 `-1`)
2. 정확한 확장자 패턴으로 파일 탐지
3. 정확한 패턴으로 못 찾으면 와일드카드(`-1`)로 폴백

### `FindFileInBuild(buildPath, pattern, isRequired)`

WebGL 빌드 폴더에서 glob 패턴으로 필요한 파일을 찾습니다.

**기본 검색 패턴 (폴백):**

| 패턴 | 필수 | 설명 | 예시 파일명 |
|------|------|------|------------|
| `*.loader.js` | Yes | Unity WebGL 로더 | `webgl.loader.js` |
| `*.data*` | Yes | 게임 데이터 | `webgl.data`, `webgl.data.gz`, `webgl.data.br` |
| `*.framework.js*` | Yes | Unity 프레임워크 | `webgl.framework.js`, `webgl.framework.js.gz` |
| `*.wasm*` | Yes | WebAssembly 바이너리 | `webgl.wasm`, `webgl.wasm.gz` |
| `*.symbols.json*` | No | 디버그 심볼 | `webgl.symbols.json` |

### 다중 매칭 경고

한 패턴에 여러 파일이 매칭되면:

```
⚠️ [AIT] Build 폴더에서 *.data* 패턴으로 여러 파일이 발견되었습니다.
  - webgl.data (2024-03-01 12:00:00)
  - webgl.data.gz (2024-03-01 11:50:00)
가장 최근 파일을 사용합니다: webgl.data
💡 Clean Build를 권장합니다: AIT > Clean 후 다시 빌드하세요.
```

- `Debug.LogWarning` 출력
- 최신 파일(LastWriteTime 기준) 선택
- "이전 빌드 잔여물일 수 있습니다" 메시지 포함

### 필수 파일 누락 에러

필수 파일(`isRequired=true`)이 없으면:

```
❌ [AIT] 필수 WebGL 빌드 파일을 찾을 수 없습니다: *.loader.js
Build 폴더 내 파일 목록:
  - webgl.data
  - webgl.framework.js
  - webgl.wasm
💡 필수 파일이 없으면 'createUnityInstance is not defined' 오류가 발생합니다.
```

- `Debug.LogError` 출력
- 빌드 폴더 내 실제 파일 목록 표시
- 빈 폴더면 "Build 폴더가 비어 있습니다!" 표시
- 반환값: 빈 문자열 → `REQUIRED_FILE_MISSING` 에러로 이어짐

### `ValidatePlaceholderSubstitution(content, filePath)`

index.html 내 미치환 플레이스홀더를 탐지합니다.

**탐지 방법:** 정규식 `%[A-Z_]+%`

**치명적 플레이스홀더** (에러 + 빌드 실패):
- `%UNITY_WEBGL_LOADER_URL%`
- `%UNITY_WEBGL_DATA_URL%`
- `%UNITY_WEBGL_FRAMEWORK_URL%`
- `%UNITY_WEBGL_CODE_URL%`

**비치명적 플레이스홀더** (경고만):
- 그 외 `%...%` 패턴

**빈 경로 패턴 탐지** (치명적):
```html
src="Build/"     ← loader.js 누락 의미
"Build/"         ← data 파일 누락 의미
Build/",         ← 구분자 뒤 빈 파일명
```

### `PrintBuildReport(buildProjectPath, distPath)`

빌드 완료 후 리포트를 출력합니다:

```
[AIT] === 빌드 리포트 ===
✓ loader.js: webgl.loader.js
✓ data: webgl.data
✓ framework: webgl.framework.js
✓ wasm: webgl.wasm
✗ symbols: (없음)
```

---

## 플레이스홀더 치환 시스템

### index.html 치환

`AITPackageBuilder.CopyWebGLToPublic()` 에서 수행됩니다.

#### Unity 표준 플레이스홀더

| 플레이스홀더 | 치환 값 | 소스 |
|-------------|---------|------|
| `%UNITY_WEB_NAME%` | 프로젝트명 | `PlayerSettings.productName` |
| `%UNITY_WIDTH%` | 화면 너비 | `PlayerSettings.defaultWebScreenWidth` |
| `%UNITY_HEIGHT%` | 화면 높이 | `PlayerSettings.defaultWebScreenHeight` |
| `%UNITY_COMPANY_NAME%` | 회사명 | `PlayerSettings.companyName` |
| `%UNITY_PRODUCT_NAME%` | 제품명 | `PlayerSettings.productName` |
| `%UNITY_PRODUCT_VERSION%` | 버전 | `PlayerSettings.bundleVersion` |

#### Unity WebGL URL 플레이스홀더 (치명적)

이 플레이스홀더가 치환되지 않으면 빌드 실패합니다:

| 플레이스홀더 | 치환 값 |
|-------------|---------|
| `%UNITY_WEBGL_LOADER_URL%` | `Build/{loaderFile}` |
| `%UNITY_WEBGL_DATA_URL%` | `Build/{dataFile}` |
| `%UNITY_WEBGL_FRAMEWORK_URL%` | `Build/{frameworkFile}` |
| `%UNITY_WEBGL_CODE_URL%` | `Build/{wasmFile}` |
| `%UNITY_WEBGL_SYMBOLS_URL%` | `Build/{symbolsFile}` (또는 빈 문자열) |

#### 레거시 파일명 플레이스홀더 (하위 호환)

| 플레이스홀더 | 치환 값 |
|-------------|---------|
| `%UNITY_WEBGL_LOADER_FILENAME%` | `{loaderFile}` (경로 없이) |
| `%UNITY_WEBGL_DATA_FILENAME%` | `{dataFile}` |
| `%UNITY_WEBGL_FRAMEWORK_FILENAME%` | `{frameworkFile}` |
| `%UNITY_WEBGL_CODE_FILENAME%` | `{wasmFile}` |
| `%UNITY_WEBGL_SYMBOLS_FILENAME%` | `{symbolsFile}` |

#### AIT 커스텀 플레이스홀더

| 플레이스홀더 | 치환 값 | 설명 |
|-------------|---------|------|
| `%AIT_IS_PRODUCTION%` | `"true"` / `"false"` | `profile.enableMockBridge == false`이면 `"true"` |
| `%AIT_ENABLE_DEBUG_CONSOLE%` | `"true"` / `"false"` | 디버그 콘솔 활성화 |
| `%AIT_DEVICE_PIXEL_RATIO%` | 숫자 | 디바이스 픽셀 비율 |
| `%AIT_ICON_URL%` | URL 문자열 | 앱 아이콘 URL |
| `%AIT_DISPLAY_NAME%` | 문자열 | 앱 표시 이름 |
| `%AIT_PRIMARY_COLOR%` | 색상 코드 | 브랜드 색상 (기본: `#3182f6`) |
| `%AIT_PRELOAD_TAGS%` | HTML 태그 | `<link rel="preload">` 태그 ([상세](#preload-태그-생성)) |
| `%AIT_LOADING_SCREEN%` | HTML 문자열 | `loading.html` 전체 내용 |

### Preload 태그 생성

`GeneratePreloadTags(dataFile, wasmFile, frameworkFile)` 에서 생성됩니다.

```html
<link rel="preload" href="Build/webgl.data" as="fetch">
<link rel="preload" href="Build/webgl.wasm" as="fetch">
```

**중요: framework.js는 preload하지 않습니다.**

> Unity 로더가 framework.js를 `<script>` 태그로 로드하는 경우 `as="fetch"` preload와
> 캐시 키가 불일치하여 이중 다운로드가 발생할 수 있습니다.
> 이는 메모리 압박을 증가시켜 간헐적 초기화 실패(ASM_CONSTS 오류)의 확률을 높일 수 있습니다.

### granite.config.ts 치환

`AITPackageBuilder.UpdateGraniteConfig()` 에서 수행됩니다.

| 플레이스홀더 | 치환 값 | 소스 |
|-------------|---------|------|
| `%AIT_APP_NAME%` | 앱 ID | `config.appName` |
| `%AIT_DISPLAY_NAME%` | 표시 이름 | `config.displayName` |
| `%AIT_PRIMARY_COLOR%` | 브랜드 색상 | `config.primaryColor` |
| `%AIT_ICON_URL%` | 아이콘 URL | `config.iconUrl` |
| `%AIT_BRIDGE_COLOR_MODE%` | 색상 모드 | `config.GetBridgeColorModeString()` |
| `%AIT_WEBVIEW_TYPE%` | 웹뷰 타입 | `config.GetWebViewTypeString()` |
| `%AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%` | `true`/`false` | 인라인 미디어 재생 허용 |
| `%AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%` | `true`/`false` | 미디어 재생 사용자 동작 필요 |
| `%AIT_VITE_HOST%` | 호스트 | `config.viteHost` |
| `%AIT_VITE_PORT%` | 포트 | `config.vitePort` |
| `%AIT_PERMISSIONS%` | JSON 문자열 | `config.GetPermissionsJson()` |
| `%AIT_OUTDIR%` | 출력 디렉토리 | `config.outdir` |

### vite.config.ts 치환

| 플레이스홀더 | 치환 값 |
|-------------|---------|
| `%AIT_VITE_HOST%` | `config.viteHost` |
| `%AIT_VITE_PORT%` | `config.vitePort` |

### appsintoss-unity-bridge.js 치환

Runtime 폴더의 브릿지 JS 파일에서:

| 플레이스홀더 | 치환 값 |
|-------------|---------|
| `%AIT_IS_PRODUCTION%` | index.html과 동일한 로직 |

---

## 로딩 화면 시스템

### 로딩 화면 파일 위치

| 경로 | 역할 |
|------|------|
| `WebGLTemplates/AITTemplate/loading.html` | SDK 기본 템플릿 (원본) |
| `Assets/AppsInToss/loading.html` | 프로젝트별 커스텀 로딩 화면 |

### 자동 생성 (`AITPackageInitializer`)

`[InitializeOnLoad]` 속성으로 에디터 시작 시 자동 실행:
1. `Assets/AppsInToss/loading.html`이 없으면 SDK 템플릿을 복사
2. SDK 템플릿 검색 순서:
   - `Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/loading.html`
   - `Packages/com.appsintoss.miniapp/WebGLTemplates/AITTemplate/loading.html`
   - Assembly 경로 기반

### 빌드 시 삽입

`CopyWebGLToPublic()` 에서 `index.html`의 `%AIT_LOADING_SCREEN%` 플레이스홀더를 치환:

```
1. Assets/AppsInToss/loading.html 존재?
   → Yes: 프로젝트 커스텀 로딩 화면 사용
   → No: SDK 기본 템플릿 폴백

2. SDK 템플릿도 없으면?
   → Debug.LogWarning("로딩 화면 파일을 찾을 수 없습니다. 빈 로딩 화면이 사용됩니다.")
   → 빈 문자열로 치환
```

### 로딩 화면 JavaScript API

`loading.html`은 `window.AITLoading` 객체를 통해 Unity 로딩 상태와 연동됩니다:

| 메서드 | 설명 |
|--------|------|
| `onReady(callback)` | 로딩 화면 준비 완료 시 |
| `onProgress(callback)` | 로딩 진행률 업데이트 (0~1) |
| `onComplete(callback)` | 로딩 완료 시 |
| `onError(callback)` | 에러 발생 시 |
| `hide()` | 로딩 화면 숨김 |

### 로딩 화면 초기화 메뉴

`AIT > Reset Loading Screen`:
- 확인 다이얼로그: "로딩 화면을 기본 템플릿으로 초기화하시겠습니까?"
- SDK 템플릿을 `Assets/AppsInToss/loading.html`로 복사
- `AssetDatabase.Refresh()` 호출

---

## 템플릿 관리

`AITTemplateManager` 클래스가 SDK 템플릿과 프로젝트 커스텀 영역을 마커 기반으로 병합합니다.

### 마커 시스템

**TypeScript 설정 파일용 (vite.config.ts, granite.config.ts):**
```typescript
//// SDK_GENERATED_START
// ... SDK가 관리하는 코드 (SDK 업데이트 시 자동 갱신) ...
//// SDK_GENERATED_END ////

//// USER_CONFIG_START
// ... 사용자 커스텀 코드 (SDK 업데이트 시 보존) ...
//// USER_CONFIG_END ////
```

**HTML용 (index.html):**
```html
<!-- USER_HEAD_START
     사용자가 <head>에 추가한 커스텀 태그 -->
<!-- USER_HEAD_END -->

<!-- USER_BODY_END_START
     사용자가 </body> 직전에 추가한 커스텀 코드 -->
<!-- USER_BODY_END_END -->
```

### 업데이트 동작

```
SDK 업데이트 시:
  ├── index.html:
  │   ├── 마커 없음 (이전 버전) → SDK 템플릿으로 교체 + 경고
  │   └── 마커 있음 → USER_HEAD, USER_BODY_END 영역 보존, 나머지 갱신
  ├── vite.config.ts, granite.config.ts:
  │   └── USER_CONFIG 영역 보존, SDK_GENERATED 영역 갱신
  ├── Runtime/ → 항상 SDK 버전으로 덮어쓰기 (브릿지 코드)
  └── TemplateData/ → 항상 SDK 버전으로 덮어쓰기
```

### 이전 버전 업그레이드 경고

마커가 없는 이전 버전 index.html 발견 시:
```
[AIT] 템플릿 업데이트: 이전 버전 템플릿을 새 마커 기반 템플릿으로 교체합니다.
⚠️ 기존 index.html에 커스텀 수정이 있었다면 수동으로 USER_* 마커 영역에 재적용하세요.
```

---

## Node.js / pnpm 관리

### 내장 Node.js (`AITNodeJSDownloader`)

SDK는 시스템 설치와 무관하게 자체 Node.js를 사용합니다.

**버전:**
- Node.js: v24.13.0
- pnpm: 10.28.0

**설치 경로:** `~/.ait-unity-sdk/nodejs/v24.13.0/{platform}/`

**다운로드 미러 (폴백 순서):**
1. `https://nodejs.org/dist/` (공식)
2. `https://cdn.npmmirror.com/binaries/node/` (중국 미러)
3. `https://repo.huaweicloud.com/nodejs/` (Huawei 미러)

### 다운로드 프로세스

```
1. 설치 경로 확인 → 이미 있으면 스킵
2. 미러 1 시도:
   ├── .tar.gz 다운로드 (macOS/Linux) 또는 .zip (Windows)
   ├── SHA256 체크섬 검증 ← 실패 시 다운로드 파일 삭제 + 다음 미러
   └── 압축 해제 → 임시 폴더
3. 미러 2/3 폴백 (동일 프로세스)
4. 임시 폴더 → 최종 경로로 원자적 이동
5. pnpm 설치: corepack enable + corepack prepare pnpm@10.28.0
```

**SHA256 체크섬**: 플랫폼별 해시가 `AITNodeJSDownloader.cs`에 하드코딩되어 있음

### pnpm 패키지 매니저 (`AITPackageManagerHelper`)

Node.js와 pnpm 실행 경로 해석 및 프로세스 관리를 담당합니다.

---

## 에러 코드 및 메시지

### `AITConvertCore.AITExportError` enum

| 코드 | 값 | 의미 | 사용자 메시지 요약 |
|------|---|------|-------------------|
| `SUCCEED` | 0 | 성공 | — |
| `NODE_NOT_FOUND` | 1 | Node.js 없음 | nodejs.org에서 설치 후 에디터 재시작 |
| `BUILD_WEBGL_FAILED` | 2 | WebGL 빌드 실패 | 콘솔 에러 확인, WebGL Build Support 설치 확인 |
| `INVALID_APP_CONFIG` | 3 | 앱 설정 오류 | 아이콘 URL(필수), 앱 ID 확인 |
| `NETWORK_ERROR` | 4 | 네트워크 오류 | 인터넷 연결, npm 레지스트리, 방화벽/프록시 확인 |
| `CANCELLED` | 5 | 사용자 취소 | 사용자가 빌드 취소함 |
| `FAIL_NPM_BUILD` | 6 | pnpm 빌드 실패 | 콘솔 확인, ait-build에서 pnpm install, Node.js 재설치 |
| `WEBGL_BUILD_INCOMPLETE` | 7 | WebGL 산출물 불완전 (레거시) | AIT > Clean, Clean Build, WebGL 템플릿 재생성 |
| `BUILD_FOLDER_MISSING` | 10 | Build 폴더 없음 | Build & Package 실행 안내 |
| `REQUIRED_FILE_MISSING` | 11 | 필수 파일 누락 | Clean Build 안내 |
| `INDEX_HTML_MISSING` | 12 | index.html 없음 | WebGL Templates 재생성 안내 |
| `PLACEHOLDER_SUBSTITUTION_FAILED` | 13 | 플레이스홀더 미치환 | Clean Build 안내 |

### 에러 발생 → 사용자 다이얼로그 흐름

```
빌드 에러 발생
  ↓
ShowComplexDialog("빌드 실패", errorMessage, ...)
  ├── "확인" → 종료
  └── "Issue 신고" → AITErrorReporter.OpenIssueInBrowser()
                     → GitHub Issues에 자동 채워진 이슈 URL 오픈
```

---

## 사용자 경고 및 다이얼로그 조건

### 에러 다이얼로그 (`EditorUtility.DisplayDialog`)

| 조건 | 제목 | 내용 |
|------|------|------|
| SDK 로딩 템플릿 없음 | "오류" | SDK 로딩 화면 템플릿을 찾을 수 없습니다 |
| Build 폴더 없음 | "오류" | WebGL 빌드 폴더를 찾을 수 없습니다 |
| 앱 이름 미설정 | "오류" | 앱 이름이 설정되지 않았습니다 |
| 배포 키 미설정 | "오류" | 배포 키가 설정되지 않았습니다 |
| pnpm 설치 실패 | "빌드 실패" | pnpm 설치에 실패했습니다 |
| 빌드 취소 | "취소됨" | 빌드가 취소되었습니다 |
| 빌드 성공 | "성공" | 빌드 및 패키징이 완료되었습니다 |
| Clean 완료 | "완료" | Clean이 완료되었습니다 |
| 배포 타임아웃 | "타임아웃" | 배포 시간 초과 |
| 포트 충돌 | "포트 충돌" | 해당 포트가 이미 사용 중입니다 |

### 확인 다이얼로그 (`ShowConfirmDialog`)

| 조건 | 동작 |
|------|------|
| `AIT/Build` (deprecated) 메뉴 클릭 | "'Build' 기능은 제거되었습니다" → Build & Package 유도 |
| `AIT/Package` (deprecated) 메뉴 클릭 | "'Package' 기능은 제거되었습니다" → Build & Package 유도 |
| `AIT/Clean` 메뉴 클릭 | "webgl/, ait-build/ 폴더를 삭제하시겠습니까?" |
| 배포 확인 | "앱 이름: X, 버전: Y — 배포하시겠습니까?" |
| 서버 전환 | "Production 서버를 정지하고 Dev 서버를 시작하시겠습니까?" |
| 설정 초기화 | "설정을 초기화하시겠습니까?" |
| 로딩 화면 초기화 | "로딩 화면을 기본 템플릿으로 초기화하시겠습니까?" |

### 3-Way 다이얼로그 (`ShowComplexDialog`)

| 조건 | 옵션 |
|------|------|
| 빌드 실패 | "확인" / "Issue 신고" |
| Publish 진입점 | "다시 빌드 후 배포" / "취소" / "기존 빌드로 배포" |
| 배포 실패 | "확인" / "Issue 신고" |

### 콘솔 경고 (`Debug.LogWarning`)

| 조건 | 메시지 요약 |
|------|------------|
| Unity 2021.x + `nameFilesAsHashes=true` | 자동 비활성화됨 |
| Build 폴더에 다중 파일 매칭 | "이전 빌드 잔여물" + Clean Build 권장 |
| 비치명적 플레이스홀더 미치환 | 해당 플레이스홀더 이름 표시 |
| `pnpm install --frozen-lockfile` 실패 | "lockfile 없이 재시도" |
| web-framework 버전 불일치 | 기대 vs 실제 버전 표시 |
| `node_modules/.pnpm` 없음 | "stale modules" |
| 이전 버전 템플릿 업그레이드 | "커스텀 수정 수동 재적용" 안내 |
| 로딩 화면 파일 없음 | "빈 로딩 화면이 사용됩니다" |
| 빌드 마커 작성 실패 | 경고만 (빌드는 계속) |
| 빌드 마커 없음/Unity 버전 불일치 | 자동 clean build |

### 콘솔 에러 (`Debug.LogError`)

| 조건 | 결과 |
|------|------|
| `webgl/Build/` 폴더 없음 | `BUILD_FOLDER_MISSING` |
| 필수 WebGL 파일 누락 | `REQUIRED_FILE_MISSING` |
| `index.html` 없음 | `INDEX_HTML_MISSING` |
| 치명적 플레이스홀더 미치환 | `PLACEHOLDER_SUBSTITUTION_FAILED` |
| 빈 경로 패턴 탐지 | `PLACEHOLDER_SUBSTITUTION_FAILED` |
| pnpm install 최종 실패 | 빌드 중단 |
| SDK BuildConfig 폴더 없음 | 빌드 중단 |
| SDK WebGLTemplates 폴더 없음 | 빌드 중단 |

---

## 서버 라이프사이클

### 통합 서버 API

`ServerType` enum (`Dev`, `Prod`)을 매개변수로 받는 통합 메서드로 서버를 관리합니다:

| 메서드 | 설명 |
|--------|------|
| `StartServer(type)` | 빌드 + 서버 시작 |
| `StopServer(type)` | 서버 프로세스 종료 |
| `RestartServer(type, serverOnly)` | `serverOnly=false`: 빌드+서버, `true`: 서버만 재시작 |
| `ValidateAndSwitchServer(type)` | 반대 서버가 실행 중이면 전환 확인 다이얼로그 |

### 메뉴 구조

```
AIT/Dev Server/
├── Start           → StartServer(Dev) → DoExport(dev) + granite dev
├── Stop            → StopServer(Dev)
├── Restart Server  → RestartServer(Dev, serverOnly: false)
└── Restart (server-only) → RestartServer(Dev, serverOnly: true)

AIT/Production Server/
├── Start           → StartServer(Prod) → DoExport(prod) + granite dev
├── Stop            → StopServer(Prod)
├── Restart Server  → RestartServer(Prod, serverOnly: false)
└── Restart (server-only) → RestartServer(Prod, serverOnly: true)
```

### 상호 배제

Dev와 Production 서버는 동시에 실행할 수 없습니다:
- `StartServer(type)` 호출 시 반대 서버가 실행 중이면 → `ValidateAndSwitchServer()` 호출
- 사용자가 전환 승인 → 기존 서버 종료 후 새 서버 시작
- 사용자가 취소 → 아무 동작 없음

### 포트 충돌 탐지

서버 시작 시 대상 포트가 이미 사용 중이면:
- "포트 충돌" 다이얼로그 표시
- 사용자가 수동으로 포트를 변경하거나 프로세스를 종료해야 함

---

## 에러 리포팅

### `AITErrorReporter`

`[InitializeOnLoad]`로 에디터 시작 시 `Application.logMessageReceived`에 구독하여 모든 콘솔 로그를 캡처합니다.

**순환 버퍼:**
| 버퍼 | 최대 크기 | 캡처 대상 |
|------|----------|----------|
| `errorLogs` | 50개 | `LogType.Error`, `LogType.Exception` |
| `warningLogs` | 30개 | `LogType.Warning` |
| `infoLogs` | 20개 | `LogType.Log`, `LogType.Assert` |

### GitHub Issue 자동 생성

`OpenIssueInBrowser(errorCode, profileName)`:

**포함 정보:**
- 이슈 제목: `[빌드 에러] {errorCode}`
- SDK 버전, Unity 버전, OS
- 프로필 이름, 에러 코드 + 메시지
- 앱 설정 (appName, displayName 등)
- BuildReport 에러 (있는 경우)
- 최근 콘솔 로그 (에러 → 경고 → 정보 순)

**URL 길이 제한:** 2000자 초과 시 단계적 절삭 (infoLogs → warningLogs → errorLogs 순으로 제거)

---

## 환경 변수 오버라이드

`AITBuildInitializer.ApplyEnvironmentVariableOverrides(profile)`:

| 환경 변수 | 값 | 동작 |
|----------|---|------|
| `AIT_DEBUG_CONSOLE` | `true`/`false` | 디버그 콘솔 강제 활성화/비활성화 |
| `AIT_COMPRESSION_FORMAT` | `-1` (Auto), `0` (Disabled), `1` (Gzip), `2` (Brotli) | 압축 포맷 오버라이드 |

잘못된 값은 `Debug.LogWarning`으로 경고하고 무시됩니다.

---

## 개선 이력

### 1. ✅ 빌드 마커 활용

`ReadBuildMarker()` 메서드로 `.ait-build-info.json`을 읽어 활용합니다:
- 빌드 캐시 검증: Unity 버전 불일치 시 자동 clean build
- 압축 포맷 탐지: `compressionFormat` 값으로 정확한 파일 확장자 탐지

### 2. ✅ Dev/Prod 서버 코드 통합

`ServerType` enum과 `StartServer(type)`, `StopServer(type)`, `RestartServer(type, serverOnly)` 등
통합 메서드로 서버 코드 중복을 제거했습니다.

### 3. ✅ 동기/비동기 패키징 코드 통합

`PreparePackaging()`으로 공통 준비 로직을 추출하고, `PackageContext` 클래스로 상태를 캡슐화했습니다.
동기 경로(`RunPnpmInstallSync`, `RunGraniteBuildSync`)와 비동기 경로(`RunPnpmInstallAsync`, `RunGraniteBuildAsync`)가
같은 `PreparePackaging()`을 공유합니다.

### 4. ✅ pnpm 재시도 로직 단순화

`PnpmInstallStages` 배열로 3단계 재시도 정책을 데이터화했습니다:
- 동기: `foreach` 루프로 순회
- 비동기: 재귀 패턴으로 3-depth 중첩 콜백을 1-depth로 평탄화

### 5. ✅ 압축 포맷별 파일 탐지 개선

`AITBuildValidator.GetFilePatterns(compressionFormat)`가 빌드 마커의 압축 포맷에 따라
정확한 확장자(`.gz`, `.br`, 없음)로 파일을 탐지합니다. 정확한 패턴으로 못 찾으면 와일드카드로 폴백합니다.

### 6. ✅ 에러 코드 세분화

`WEBGL_BUILD_INCOMPLETE`를 4개의 구체적 에러 코드로 세분화했습니다:
- `BUILD_FOLDER_MISSING` (10): Build 폴더 없음
- `REQUIRED_FILE_MISSING` (11): 필수 파일 누락
- `INDEX_HTML_MISSING` (12): index.html 없음
- `PLACEHOLDER_SUBSTITUTION_FAILED` (13): 플레이스홀더 미치환

### 7. ✅ 빌드 캐시 유효성 검증

`ShouldForceCleanBuild()`가 WebGL 빌드 전에 기존 빌드 캐시를 검증합니다:
- 빌드 마커 없음 → 자동 clean build
- Unity 버전 불일치 → 자동 clean build
- Build 폴더/loader.js 없음 → 자동 clean build
