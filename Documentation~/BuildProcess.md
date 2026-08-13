# 빌드 파이프라인

Unity 프로젝트가 배포 가능한 `.ait` 패키지가 되기까지 SDK가 내부에서 무엇을 하는지 설명합니다.

> **대상**: SDK 기여자. SDK를 사용해 게임을 빌드하는 것이 목적이라면 [빌드 프로필](BuildProfiles.md)과 [빌드 커스터마이징](BuildCustomization.md)이 필요한 문서입니다.

## 2단계 파이프라인 구조

빌드는 Unity가 WebGL 산출물을 만드는 단계와, 그 산출물을 웹 프로젝트로 재배치해 granite로 패키징하는 단계로 나뉩니다.

```text
┌─────────────────────────────────────────────────────────────────────┐
│                        Entry Points                                 │
│  Menu: Build & Package  │  Build Window  │  Server Start/Restart    │
│  AppsInTossMenu.cs      │  AppsInTossBuildWindow.cs                 │
└─────────────┬───────────────────────────────────────────────────────┘
              │
              ▼
┌─────────────────────────────────────────────────────────────────────┐
│  AITConvertCore.DoExport(buildWebGL, doPackaging, cleanBuild,       │
│                          profile, profileName)                      │
│  또는 DoExportAsync(...)                                            │
└─────────────┬───────────────────────────────────────────────────────┘
              │
     ┌────────┴────────────────────────────────┐
     ▼                                         ▼
┌──────────────────────┐          ┌────────────────────────────────┐
│  Phase 1: WebGL 빌드 │          │  Phase 2: 패키징               │
│  BuildWebGL()        │          │  GenerateMiniAppPackage()      │
│                      │          │  → AITPackageBuilder           │
│  - Init()            │          │    .PackageWebGLBuild()        │
│  - BuildPipeline     │          │                                │
│  - .ait-build-info   │          │  2a. BuildConfig 복사          │
│                      │          │  2b. WebGL→public 복사         │
│  산출물: webgl/      │          │  2c. 플레이스홀더 치환         │
│                      │          │  2d. 로딩 화면 삽입            │
│                      │          │  2e. pnpm install              │
│                      │          │  2f. granite build             │
│                      │          │                                │
│                      │          │  산출물: ait-build/dist/       │
└──────────────────────┘          └────────────────────────────────┘
```

### 호출 매트릭스

| 진입점 | buildWebGL | doPackaging | cleanBuild |
|--------|-----------|------------|-----------|
| `Build & Package` | `true` | `true` | `false` |
| `Build & Package (clean)` | `true` | `true` | `true` |
| `Deploy (Test)` | `true` | `true` | `false` |
| `Deploy (Production)` | `true` | `true` | `true` |
| `Dev Server Start` | `true` | `true` | `false` |
| `Restart Server` | `true` | `true` | `false` |
| `Restart (server-only)` | — | — | — |

> **참고**: `Restart (server-only)`는 `DoExport`를 호출하지 않고 granite 프로세스만 재시작합니다.

## Phase 0 초기화

### 템플릿 동기화

`AITTemplateManager.EnsureWebGLTemplatesExist`가 빌드 전에 SDK의 WebGL 템플릿을 프로젝트로 복사합니다.

SDK 템플릿 검색 순서:

1. `Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/`
2. `Packages/com.appsintoss.miniapp/WebGLTemplates/`
3. Assembly 경로 기반 (`typeof(AITConvertCore).Assembly.Location` 상위)

프로젝트에 `Assets/WebGLTemplates/AITTemplate/`이 없으면 전체를 복사하고, 있으면 마커 기반으로 갱신해 사용자 커스텀 영역을 보존합니다. 아래 **템플릿 병합 시점** 절을 참고하세요.

### 빌드 설정

`AITBuildInitializer.Init`이 Unity PlayerSettings를 자동 구성합니다.

| 설정 | 값 | 비고 |
|------|---|------|
| WebGL Template | `PROJECT:AITTemplate` | 하드코딩 |
| Linker Target | `Wasm` | 하드코딩 |
| Scripting Backend | `IL2CPP` | 하드코딩 |
| Memory Size | 256~1536MB | Unity 버전별 기본값 (사용자 오버라이드 가능) |
| Compression | `Brotli` | 기본값. `decompressionFallback`이 켜져 있어 모든 Unity 버전에서 사용 가능 |
| Threading | `false` | 기본값 (모바일 브라우저 호환성) |
| Data Caching | `false` | 기본값 |
| `nameFilesAsHashes` | 사용자 설정 (기본 `true`) | Unity 2021.x에서만 강제로 `false` — `true`면 Bee 빌드 루프 버그 발생 |
| Engine Code Stripping | 사용자 설정 | — |
| Managed Stripping | `High` | 기본값 |
| IL2CPP Config | 사용자 설정 | — |

기본값의 단일 출처는 `AITEditorScriptObject`의 `GetDefault*` 정적 메서드입니다. Dev Server 프로필만 압축을 `Disabled`로 내립니다 — 프로필별 차이는 [빌드 프로필](BuildProfiles.md)에 정리되어 있습니다.

버전별 기본 메모리:

- Unity 2021.3: 256MB
- Unity 2022.3: 512MB
- Unity 6 (2023.3+): 1024MB
- Unity 2024.2+: 1536MB

프로필에 적용되는 환경 변수 오버라이드는 `AITBuildInitializer.ApplyEnvironmentVariableOverrides`가 처리합니다. 변수 목록과 값은 [빌드 프로필](BuildProfiles.md)이 정본입니다.

### Config 검증

`DoExport`는 시작 시 `UnityUtil.GetEditorConf()`로 설정 에셋을 읽고, 에셋 자체를 찾지 못하면 `INVALID_APP_CONFIG`를 반환합니다.

앱 ID나 아이콘 URL이 비어 있는 것은 빌드를 막지 않습니다. 앱 ID는 Configuration 창의 빌드 버튼을 비활성화하는 조건(`AITEditorScriptObject.IsAppNameValid`)일 뿐이고, 아이콘 URL은 입력됐을 때 형식만 검사합니다. 즉 빈 값 그대로 빌드하면 `%AIT_ICON_URL%` 등이 빈 문자열로 치환된 패키지가 만들어집니다.

## Phase 1 WebGL 빌드

`AITConvertCore.BuildWebGL()`

### 실행 흐름

```text
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

### 빌드 마커

성공적인 WebGL 빌드 후 `webgl/.ait-build-info.json`에 메타데이터를 기록합니다. 스키마는 `AITConvertCore.cs`의 `AITBuildInfo` 클래스입니다.

```json
{
    "sdkVersion": "1.7.0",
    "buildTime": "2024-03-01T12:00:00.0000000Z",
    "compressionFormat": 2,
    "decompressionFallback": true,
    "profileName": "Production",
    "unityVersion": "6000.2.15f1"
}
```

| 필드 | 설명 |
|------|------|
| `sdkVersion` | SDK 패키지 버전 |
| `buildTime` | UTC ISO 8601 빌드 시각 |
| `compressionFormat` | `PlayerSettings.WebGL.compressionFormat` int 값 (0=Disabled, 1=Gzip, 2=Brotli) |
| `decompressionFallback` | `PlayerSettings.WebGL.decompressionFallback`. 켜져 있으면 산출물 확장자가 `.unityweb`가 된다 |
| `profileName` | "Development" 또는 "Production" |
| `unityVersion` | `Application.unityVersion` |

`ReadBuildMarker(webglPath)`가 마커를 읽어 `AITBuildInfo`를 반환하고, 파일이 없거나 파싱에 실패하면 `null`을 반환합니다.

마커 활용처:

- **빌드 캐시 검증** (`ShouldForceCleanBuild`) — Unity 버전 불일치나 마커 없음이면 자동 clean build
- **압축 포맷 탐지** (`CopyWebGLToPublic`) — `compressionFormat`과 `decompressionFallback`으로 정확한 확장자 결정

### 빌드 캐시 유효성 검증

`ShouldForceCleanBuild(outputPath, cleanBuild)`가 WebGL 빌드 전에 기존 캐시를 검증합니다.

```text
1. cleanBuild=true → 무조건 clean build
2. webgl/ 폴더 없음 → 새 빌드 (clean 불필요)
3. 빌드 마커 없음 → clean build (이전 SDK 버전 또는 손상)
4. Unity 버전 불일치 → clean build (빌드 결과물 호환 보장)
5. Build/*.loader.js 없음 → clean build (필수 파일 누락)
6. 모두 통과 → 증분 빌드
```

Unity의 `BuildPipeline`은 기본적으로 증분 빌드를 수행하므로, `webgl/`이 남아 있으면 변경된 에셋만 다시 만듭니다. `cleanBuild=true`이면 `webgl/`을 삭제하고 `BuildOptions.CleanBuildCache`로 전체 빌드합니다.

## Phase 2 패키징

`AITPackageBuilder.PackageWebGLBuild()` (동기) 또는 `PackageWebGLBuildAsync()` (비동기). 두 경로는 `PreparePackaging()`으로 공통 준비 로직을 공유합니다.

```text
PreparePackaging() ← 동기/비동기 공통
├── Node.js/pnpm 설치 대기
├── ait-build/ 디렉토리 생성
├── CopyBuildConfigFromTemplate()
│   ├── SDK BuildConfig~/ → ait-build/ 복사
│   └── pnpm-lock.yaml 복사
├── CopyWebGLToPublic()
│   ├── webgl/Build/ 검증 (압축 포맷별 파일 탐지)
│   ├── Build → ait-build/public/Build/ 복사
│   ├── TemplateData → ait-build/public/TemplateData/ 복사
│   ├── Runtime → ait-build/public/Runtime/ 복사
│   ├── index.html 플레이스홀더 치환 → ait-build/index.html
│   ├── 로딩 화면 삽입
│   └── 플레이스홀더 검증
├── pnpm 경로 확인
└── ValidateNodeModulesIntegrity()

동기 경로: RunPnpmInstallSync() → RunGraniteBuildSync()
비동기 경로: RunPnpmInstallAsync() → RunGraniteBuildAsync()

산출물: ait-build/dist/
```

### BuildConfig 복사

`CopyBuildConfigFromTemplate`이 SDK의 `WebGLTemplates/AITTemplate/BuildConfig~/`를 `ait-build/`로 복사합니다.

| 파일 | 처리 |
|------|------|
| `package.json` | dependencies 머지 |
| `tsconfig.json` | compilerOptions 머지 |
| `vite.config.ts` | `%AIT_VITE_HOST%`, `%AIT_VITE_PORT%` 치환 |
| `granite.config.ts` | 13개 플레이스홀더 치환 |
| `apps-in-toss.config.ts` | 3.x 설정 파일. `granite.config.ts`와 같은 플레이스홀더 집합 |
| `pnpm-lock.yaml` | 있으면 복사 |

실제 머지 규칙은 `Package/BuildConfigMerger.cs`에 있습니다.

### WebGL을 public으로 복사

`CopyWebGLToPublic`이 `webgl/` 산출물을 `ait-build/` 구조로 재배치합니다.

```text
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

이 과정에서 플레이스홀더 치환과 로딩 화면 삽입이 함께 일어납니다. 치환 규칙은 아래 **플레이스홀더 치환** 절에 있습니다. 로딩 화면 자체의 동작과 커스터마이징은 [로딩 화면 커스터마이징](LoadingScreenCustomization.md)이 정본입니다.

### pnpm install

**설치 스킵 판정.** 매 빌드마다 install을 다시 도는 것을 피하기 위해, 성공한 install 직후 `Package/PnpmInstallStateMarker.cs`가 `package.json`·`pnpm-lock.yaml`의 내용 해시와 pnpm 버전을 `ait-build/node_modules/.ait-install-state.json`에 기록합니다. 다음 빌드에서 이 값이 전부 일치하고 `node_modules` 무결성 검증을 통과하면 install을 건너뜁니다.

마커를 `node_modules` **안에** 두는 이유는 `NodeModulesValidator.CleanNodeModules`가 `node_modules`를 통째로 지우면 마커도 함께 무효화되기 때문입니다 — 재시도 정책의 clean 단계와 자동으로 정합이 맞습니다. 마커가 없거나 파싱에 실패하는 등 판정이 불가능한 모든 경우는 fail-closed로 "스킵 불가" 처리합니다. 잘못된 스킵(빌드 실패)의 비용이 불필요한 재설치(시간 낭비)보다 크기 때문입니다.

킬스위치는 환경 변수 `AIT_DISABLE_INSTALL_SKIP`입니다. `1`/`true`면 스킵을 끄고, 해석할 수 없는 값이면 경고를 남긴 뒤 역시 스킵을 끕니다 — 오타로 킬스위치가 무력화되지 않도록 fail-safe로 동작합니다.

**3단계 재시도.** 스킵하지 않는 경우 `PnpmInstallStages` 배열에 정의된 순서로 진행합니다.

```text
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
│  실패? → FAIL_NPM_BUILD 에러         │
└──────────────────────────────────────┘
```

`ValidateNodeModulesIntegrity()`의 판정 순서:

1. `node_modules/`가 없으면 유효 (새로 설치됨)
2. `node_modules/.pnpm/` 디렉토리가 없으면 무효 (stale modules)
3. `package.json`에서 `@apps-in-toss/web-framework` 버전 추출
4. `node_modules/.pnpm/@apps-in-toss+web-framework@{version}*/` 존재 확인 — 버전 불일치나 패키지 없음이면 경고 후 무효 처리

### granite build

```bash
pnpm run build   # → granite build 실행
```

실패하면 `CleanNodeModules()` → `pnpm install --no-frozen-lockfile` → `pnpm run build`로 한 번 재시도하고, 그래도 실패하면 `FAIL_NPM_BUILD`를 반환합니다.

산출물은 `ait-build/dist/`이며, 여기에 `.ait` 파일이 없으면 `DIST_FOLDER_MISSING` 또는 `AIT_FILE_MISSING`으로 이어집니다.

## 파일 시그니처 탐지 및 검증

`AITBuildValidator`가 WebGL 산출물의 존재와 무결성을 검증합니다.

### 압축 포맷별 검색 패턴

`GetFilePatterns(compressionFormat, decompressionFallback)`이 빌드 마커 값에 따라 검색 패턴을 결정합니다.

| 조건 | data 패턴 | framework 패턴 | wasm 패턴 |
|---|---|---|---|
| `decompressionFallback = true` | `*.data.unityweb` | `*.framework.js.unityweb` | `*.wasm.unityweb` |
| `0` Disabled | `*.data` | `*.framework.js` | `*.wasm` |
| `1` Gzip | `*.data.gz` | `*.framework.js.gz` | `*.wasm.gz` |
| `2` Brotli | `*.data.br` | `*.framework.js.br` | `*.wasm.br` |
| 그 외 (폴백) | `*.data*` | `*.framework.js*` | `*.wasm*` |

`decompressionFallback`이 켜져 있으면 압축 포맷보다 우선합니다. loader는 압축 대상이 아니므로 항상 `*.loader.js`입니다.

### 파일 탐지

`FindFileInBuild(buildPath, pattern, isRequired)`가 glob 패턴으로 파일을 찾습니다. `*.data*` 같은 꼬리 와일드카드는 `*.data.meta`도 매칭하므로 `.meta`는 결과에서 제외합니다 — 제외하지 않으면 `LastWriteTime` 정렬에서 `.meta`가 최신으로 뽑혀 잘못된 파일명이 반환됩니다.

| 패턴 | 필수 | 설명 |
|------|------|------|
| `*.loader.js` | Yes | Unity WebGL 로더 |
| `*.data*` | Yes | 게임 데이터 |
| `*.framework.js*` | Yes | Unity 프레임워크 |
| `*.wasm*` | Yes | WebAssembly 바이너리 |
| `*.symbols.json*` | No | 디버그 심볼 |

**중복 매칭 시 자동 정리.** 한 패턴에 여러 파일이 매칭되면 `LastWriteTime` 내림차순(동률이면 파일명 내림차순)으로 정렬해 최신 하나만 남기고 나머지를 `.meta`와 함께 삭제합니다. 경고 로그만 남기는 방식은 매 빌드마다 반복 발생해 Sentry 노이즈가 쌓이기 때문에 삭제로 바뀌었습니다. 삭제에 실패한 파일이 있으면 Clean Build를 권고하는 정보 로그를 남깁니다.

**필수 파일 누락.** `isRequired=true`인 패턴을 못 찾으면 첫 줄만 Sentry로 보내고(패턴별로 fingerprint가 안정적으로 묶이도록) 나머지 진단 줄은 콘솔에만 남깁니다. 진단에는 검색 경로, 다음 문자열, 그리고 Build 폴더의 실제 파일 목록(비어 있으면 그 사실)이 포함됩니다.

```text
이 파일이 없으면 런타임에서 'createUnityInstance is not defined' 에러가 발생합니다.
```

반환값은 빈 문자열이고, 호출부에서 `REQUIRED_FILE_MISSING`으로 이어집니다.

### 플레이스홀더 치환 검증

`ValidatePlaceholderSubstitution(content, filePath)`이 정규식 `%[A-Z_]+%`로 미치환 플레이스홀더를 찾습니다.

치명적 (에러 + 빌드 실패):

- `%UNITY_WEBGL_LOADER_URL%`
- `%UNITY_WEBGL_DATA_URL%`
- `%UNITY_WEBGL_FRAMEWORK_URL%`
- `%UNITY_WEBGL_CODE_URL%`

그 외 `%...%` 패턴은 경고만 남깁니다. 다음과 같은 빈 경로 패턴도 치명적으로 취급합니다.

```html
src="Build/"     ← loader.js 누락 의미
"Build/"         ← data 파일 누락 의미
Build/",         ← 구분자 뒤 빈 파일명
```

`apps-in-toss.config.ts`의 경우 SDK_GENERATED 영역의 미치환은 하드 에러지만, USER_CONFIG 영역에 남은 SDK 플레이스홀더나 3.x에서 이동된 키는 경고에 그칩니다 — 병합 시 SDK 값이 우선하므로 빌드 결과는 정상입니다.

### 빌드 완료 리포트

`PrintBuildReport(buildProjectPath, distPath)`가 `ait-build/public/Build/`를 스캔해 필수 패턴 4개와 선택 패턴 1개의 존재 여부를 파일 크기와 함께 출력합니다. 필수 패턴이 없으면 `Debug.LogError`로 `[누락됨!]`을 표시하고, 선택 패턴은 있을 때만 표시합니다.

## 플레이스홀더 치환

### index.html

`AITPackageBuilder.CopyWebGLToPublic()`에서 수행됩니다.

**Unity 표준**

| 플레이스홀더 | 소스 |
|-------------|------|
| `%UNITY_WEB_NAME%` | `PlayerSettings.productName` |
| `%UNITY_WIDTH%` | `PlayerSettings.defaultWebScreenWidth` |
| `%UNITY_HEIGHT%` | `PlayerSettings.defaultWebScreenHeight` |
| `%UNITY_COMPANY_NAME%` | `PlayerSettings.companyName` |
| `%UNITY_PRODUCT_NAME%` | `PlayerSettings.productName` |
| `%UNITY_PRODUCT_VERSION%` | `PlayerSettings.bundleVersion` |

**Unity WebGL URL** — 치환되지 않으면 빌드가 실패합니다.

| 플레이스홀더 | 치환 값 |
|-------------|---------|
| `%UNITY_WEBGL_LOADER_URL%` | `Build/{loaderFile}` |
| `%UNITY_WEBGL_DATA_URL%` | `Build/{dataFile}` |
| `%UNITY_WEBGL_FRAMEWORK_URL%` | `Build/{frameworkFile}` |
| `%UNITY_WEBGL_CODE_URL%` | `Build/{wasmFile}` |
| `%UNITY_WEBGL_SYMBOLS_URL%` | `Build/{symbolsFile}` (또는 빈 문자열) |

**레거시 파일명** — 하위 호환용. 경로 없이 파일명만 치환됩니다.

| 플레이스홀더 | 치환 값 |
|-------------|---------|
| `%UNITY_WEBGL_LOADER_FILENAME%` | `{loaderFile}` |
| `%UNITY_WEBGL_DATA_FILENAME%` | `{dataFile}` |
| `%UNITY_WEBGL_FRAMEWORK_FILENAME%` | `{frameworkFile}` |
| `%UNITY_WEBGL_CODE_FILENAME%` | `{wasmFile}` |
| `%UNITY_WEBGL_SYMBOLS_FILENAME%` | `{symbolsFile}` |

**AIT 커스텀**

| 플레이스홀더 | 치환 값 | 설명 |
|-------------|---------|------|
| `%AIT_ENABLE_DEBUG_CONSOLE%` | `"true"` / `"false"` | 디버그 콘솔 활성화 |
| `%AIT_DEVICE_PIXEL_RATIO%` | 숫자 | 디바이스 픽셀 비율 |
| `%AIT_ICON_URL%` | URL 문자열 | 앱 아이콘 URL |
| `%AIT_DISPLAY_NAME%` | 문자열 | 앱 표시 이름 |
| `%AIT_PRIMARY_COLOR%` | 색상 코드 | 브랜드 색상 (기본: `#3182f6`) |
| `%AIT_PRELOAD_TAGS%` | HTML 태그 | `<link rel="preload">` 태그 |
| `%AIT_LOADING_SCREEN%` | HTML 문자열 | 로딩 화면 전체 내용. [로딩 화면 커스터마이징](LoadingScreenCustomization.md) 참고 |

### Preload 태그

`GeneratePreloadTags(dataFile, wasmFile, frameworkFile)`이 생성합니다.

```html
<link rel="preload" href="Build/webgl.data" as="fetch">
<link rel="preload" href="Build/webgl.wasm" as="fetch">
```

> **중요**: framework.js는 preload하지 않습니다. Unity 로더가 framework.js를 `<script>` 태그로 로드하면 `as="fetch"` preload와 캐시 키가 불일치해 이중 다운로드가 발생할 수 있습니다. 이는 메모리 압박을 키워 간헐적 초기화 실패(ASM_CONSTS 오류) 확률을 높입니다.

### granite.config.ts

`Package.BuildConfigMerger.UpdateGraniteConfig()`에서 13개를 치환합니다.

| 플레이스홀더 | 소스 |
|-------------|------|
| `%AIT_APP_NAME%` | `config.appName` |
| `%AIT_DISPLAY_NAME%` | `config.displayName` |
| `%AIT_PRIMARY_COLOR%` | `config.primaryColor` |
| `%AIT_ICON_URL%` | `config.iconUrl` |
| `%AIT_BRIDGE_COLOR_MODE%` | `config.GetBridgeColorModeString()` |
| `%AIT_WEBVIEW_TYPE%` | `config.GetWebViewTypeString()` |
| `%AIT_NAVIGATION_BAR%` | `config.GetNavigationBarJson()` |
| `%AIT_ALLOWS_INLINE_MEDIA_PLAYBACK%` | `config.allowsInlineMediaPlayback` |
| `%AIT_MEDIA_PLAYBACK_REQUIRES_USER_ACTION%` | `config.mediaPlaybackRequiresUserAction` |
| `%AIT_VITE_HOST%` | `config.viteHost` |
| `%AIT_VITE_PORT%` | `config.vitePort` |
| `%AIT_PERMISSIONS%` | `config.GetPermissionsJson()` |
| `%AIT_OUTDIR%` | `config.outdir` |

### vite.config.ts

`%AIT_VITE_HOST%` → `config.viteHost`, `%AIT_VITE_PORT%` → `config.vitePort`.

## 템플릿 병합 시점

`AITTemplateManager`가 SDK 템플릿과 프로젝트 커스텀 영역을 마커 기반으로 병합합니다. 마커 문법과 사용자가 편집할 수 있는 영역은 [빌드 커스터마이징](BuildCustomization.md)이 정본입니다. 여기서는 병합이 **언제, 무엇에** 일어나는지만 다룹니다.

병합은 Phase 0의 `EnsureWebGLTemplatesExist`에서, 즉 Unity WebGL 빌드가 시작되기 전에 일어납니다.

```text
SDK 업데이트 시:
  ├── index.html:
  │   ├── 마커 없음 (이전 버전) → SDK 템플릿으로 교체 + 경고
  │   └── 마커 있음 → USER_HEAD, USER_BODY_END 영역 보존, 나머지 갱신
  ├── vite.config.ts, granite.config.ts, apps-in-toss.config.ts:
  │   └── USER_CONFIG 영역 보존, SDK_GENERATED 영역 갱신
  ├── Runtime/ → 항상 SDK 버전으로 덮어쓰기 (디버그 콘솔 등)
  └── TemplateData/ → 항상 SDK 버전으로 덮어쓰기
```

마커가 없는 이전 버전 index.html을 발견하면 다음 경고를 남기고 SDK 템플릿으로 교체합니다.

```text
[AIT] 템플릿 업데이트: 이전 버전 템플릿을 새 마커 기반 템플릿으로 교체합니다.
⚠️ 기존 index.html에 커스텀 수정이 있었다면 수동으로 USER_* 마커 영역에 재적용하세요.
```

## Node.js 와 pnpm 관리

SDK는 시스템 설치와 무관하게 자체 Node.js를 내려받아 사용합니다. 버전의 단일 출처는 `AITNodeJSDownloader.cs`의 `NODE_VERSION`과 `AITPackageManagerHelper.cs`의 `PNPM_VERSION`입니다.

`PNPM_VERSION`은 `package.json`, `sdk-runtime-generator~/package.json`, `WebGLTemplates/AITTemplate/BuildConfig~/package.json` 세 곳의 `packageManager` 필드와 항상 같아야 합니다. 값이 갈라지면 클라이언트가 쓰는 pnpm과 lockfile을 갱신한 pnpm이 달라져 specifier drift가 생깁니다.

설치 경로는 `~/.ait-unity-sdk/nodejs/v{NODE_VERSION}/{platform}/`입니다.

다운로드 미러는 순서대로 폴백합니다.

1. `https://nodejs.org/dist/` (공식)
2. `https://cdn.npmmirror.com/binaries/node/`
3. `https://repo.huaweicloud.com/nodejs/`

```text
1. 설치 경로 확인 → 이미 있으면 스킵
2. 미러 1 시도:
   ├── .tar.gz 다운로드 (macOS/Linux) 또는 .zip (Windows)
   ├── SHA256 체크섬 검증 ← 실패 시 다운로드 파일 삭제 + 다음 미러
   └── 압축 해제 → 임시 폴더
3. 미러 2/3 폴백 (동일 프로세스)
4. 임시 폴더 → 최종 경로로 원자적 이동
5. pnpm 설치: corepack enable + corepack prepare
```

플랫폼별 SHA256 해시는 `AITNodeJSDownloader.cs`에 하드코딩되어 있습니다. Node.js와 pnpm 실행 경로 해석 및 프로세스 관리는 `AITPackageManagerHelper`가 담당합니다.

## 에러 코드

`AITConvertCore.AITExportError` enum입니다. 값 `7`은 이전 `WEBGL_BUILD_INCOMPLETE`였으나 `10`~`13`으로 세분화되면서 제거됐습니다.

| 코드 | 값 | 짧은 라벨 |
|------|---|-----------|
| `SUCCEED` | 0 | 성공 |
| `NODE_NOT_FOUND` | 1 | Node.js 없음 |
| `BUILD_WEBGL_FAILED` | 2 | WebGL 빌드 오류 |
| `INVALID_APP_CONFIG` | 3 | 앱 설정 오류 |
| `NETWORK_ERROR` | 4 | 네트워크 오류 |
| `CANCELLED` | 5 | 사용자 취소 |
| `FAIL_NPM_BUILD` | 6 | pnpm 빌드 오류 |
| `BUILD_FOLDER_MISSING` | 10 | Build 폴더 없음 |
| `REQUIRED_FILE_MISSING` | 11 | 필수 파일 누락 |
| `INDEX_HTML_MISSING` | 12 | index.html 없음 |
| `PLACEHOLDER_SUBSTITUTION_FAILED` | 13 | 플레이스홀더 미치환 |
| `DIST_FOLDER_MISSING` | 14 | dist 폴더 없음 |
| `AIT_FILE_MISSING` | 15 | .ait 파일 없음 |

사용자에게 보여줄 전체 메시지와 짧은 라벨은 모두 `AITExportErrorCatalog`가 소유합니다.

```text
빌드 에러 발생
  ↓
ShowComplexDialog("빌드 실패", errorMessage, ...)
  ├── "확인" → 종료
  └── "Issue 신고" → AITErrorReporter.OpenIssueInBrowser()
                     → GitHub Issues에 자동 채워진 이슈 URL 오픈
```

## 사용자 경고 및 다이얼로그 조건

### 에러 다이얼로그

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

### 확인 다이얼로그

| 조건 | 동작 |
|------|------|
| `AIT/Clean` | "webgl/, ait-build/ 폴더를 삭제하시겠습니까?" |
| 배포 확인 | "앱 이름: X, 버전: Y — 배포하시겠습니까?" (memo 자동 생성값 표시) |
| 설정 초기화 | "설정을 초기화하시겠습니까?" |
| 로딩 화면 초기화 | "로딩 화면을 기본 템플릿으로 초기화하시겠습니까?" |

### 3-Way 다이얼로그

| 조건 | 옵션 |
|------|------|
| 빌드 실패 | "확인" / "Issue 신고" |
| 배포 실패 | "확인" / "Issue 신고" |

### 콘솔 경고

| 조건 | 메시지 요약 |
|------|------------|
| 비치명적 플레이스홀더 미치환 | 해당 플레이스홀더 이름 표시 |
| USER_CONFIG에 SDK 관리 설정 잔존 | 제거 권고 (빌드는 정상) |
| `pnpm install --frozen-lockfile` 실패 | 다음 재시도 단계로 진행 |
| web-framework 버전 불일치 | 기대 vs 실제 버전 표시 |
| `node_modules/.pnpm` 없음 | stale modules |
| 이전 버전 템플릿 업그레이드 | 커스텀 수정 수동 재적용 안내 |
| 로딩 화면 파일 없음 | 빈 로딩 화면이 사용됨 |
| 빌드 마커 작성 실패 | 경고만 (빌드는 계속) |
| 빌드 마커 없음 / Unity 버전 불일치 | 자동 clean build |
| `AIT_DISABLE_INSTALL_SKIP` 값 해석 불가 | 스킵 비활성으로 처리 |

### 콘솔 에러

| 조건 | 결과 |
|------|------|
| `webgl/Build/` 폴더 없음 | `BUILD_FOLDER_MISSING` |
| 필수 WebGL 파일 누락 | `REQUIRED_FILE_MISSING` |
| `index.html` 없음 | `INDEX_HTML_MISSING` |
| 치명적 플레이스홀더 미치환 | `PLACEHOLDER_SUBSTITUTION_FAILED` |
| 빈 경로 패턴 탐지 | `PLACEHOLDER_SUBSTITUTION_FAILED` |
| granite build 후 dist 없음 | `DIST_FOLDER_MISSING` |
| dist에 `.ait` 없음 | `AIT_FILE_MISSING` |
| pnpm install 최종 실패 | 빌드 중단 |
| SDK BuildConfig 폴더 없음 | 빌드 중단 |
| SDK WebGLTemplates 폴더 없음 | 빌드 중단 |

## 서버 라이프사이클

로컬 서버는 Dev Server 하나뿐입니다 (과거 있었던 Production Server는 3.0.0부터 샌드박스 앱 연동이 불가능해지면서 제거됨 — 프로덕션 설정을 실기기에서 확인하려면 [시작하기의 Deploy (Test)](GettingStarted.md#실기기로-확인하기-deploy-test)를 사용).

| 메서드 | 설명 |
|--------|------|
| `StartServer()` | 빌드 + 서버 시작 |
| `StopServer()` | 서버 프로세스 종료 |
| `RestartServer(serverOnly)` | `serverOnly=false`면 빌드+서버, `true`면 서버만 재시작 |

```text
AIT/Dev Server/
├── Start Server              → StartServer() → DoExport(dev) + granite dev
├── Stop Server               → StopServer()
├── Restart Server            → RestartServer(serverOnly: false)
└── Restart Server (server-only) → RestartServer(serverOnly: true)
```

대상 포트가 이미 사용 중이면 "포트 충돌" 다이얼로그를 띄우고, 사용자가 포트를 바꾸거나 점유 프로세스를 종료해야 합니다.

## 에러 리포팅

`AITErrorReporter`가 `[InitializeOnLoad]`로 에디터 시작 시 `Application.logMessageReceived`를 구독해 모든 콘솔 로그를 순환 버퍼에 캡처합니다.

| 버퍼 | 최대 크기 | 캡처 대상 |
|------|----------|----------|
| `errorLogs` | 50개 | `LogType.Error`, `LogType.Exception` |
| `warningLogs` | 30개 | `LogType.Warning` |
| `infoLogs` | 20개 | `LogType.Log`, `LogType.Assert` |

`OpenIssueInBrowser(errorCode, profileName)`은 이 버퍼로 GitHub Issue URL을 자동 구성합니다. 제목은 `[빌드 에러] {errorCode}`이고, 본문에는 SDK/Unity/OS 버전, 프로필 이름, 에러 코드와 메시지, 앱 설정, `BuildReport` 에러(있는 경우), 최근 콘솔 로그가 담깁니다. URL이 2000자를 넘으면 `infoLogs` → `warningLogs` → `errorLogs` 순으로 단계적으로 잘라냅니다.

Sentry로 나가는 로그의 범위와 노이즈 억제 정책은 [Sentry 연동](SentryIntegration.md)에 있습니다.

## 관련 문서

- [빌드 프로필](BuildProfiles.md) — 프로필별 설정 차이, 환경 변수 오버라이드
- [빌드 커스터마이징](BuildCustomization.md) — 마커 영역 계약, 웹 진입점 편집
- [로딩 화면 커스터마이징](LoadingScreenCustomization.md) — 로딩 화면 교체와 `AITLoading` API
- [Sentry 연동](SentryIntegration.md) — 에러 수집과 컨텍스트 주입
- [문제 해결](Troubleshooting.md) — 빌드가 막혔을 때
