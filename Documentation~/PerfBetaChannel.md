# perf 베타 채널 (파일럿 전용)

이 문서는 **선택된 파일럿 제휴사**가 WebGL 콜드 로드 시간 단축을 목표로 하는 실험적 빌드 채널인 **perf 베타 채널**을 옵트인으로 미리 테스트하는 방법을 안내합니다.

> ⚠️ **주의 — perf 베타 채널은 production-ready가 아닙니다.**
> 이 채널은 콜드 로드 최적화 레버를 자사 게임에 적용·실측해 보려는 사전 협의된 파일럿 대상에게만 안내됩니다. 일반 서비스 배포에는 stable(`#release/vX.Y.Z`)을 사용하세요.

## 목차

- [stable과의 차이](#stable과의-차이)
- [옵트인 (설치)](#옵트인-설치)
- [자동 적용 레버](#자동-적용-레버)
- [직접 켜야 하는 레버 (opt-in)](#직접-켜야-하는-레버-opt-in)
- [설정 방법](#설정-방법)
- [측정](#측정)
- [새 베타로 업데이트](#새-베타로-업데이트)
- [stable로 복귀 (repin)](#stable로-복귀-repin)
- [알아둘 점](#알아둘-점)

---

## stable과의 차이

| 항목 | stable | perf 베타 채널 |
|------|--------|-----------|
| 핀 대상 | `#release/vX.Y.Z` (불변 태그) | `#beta-perf` (이동 브랜치, 항상 최신 perf 베타) |
| 번들 마킹 | 없음 | `.ait` 헤더 + 런타임 `window.AITLoading.buildVariant`에 `"perf"` 자동 주입 |
| 자동 업데이트 프롬프트 | 표시됨 | **표시 안 됨** (수동 관리) |
| GitHub Release 표시 | Latest | prerelease (`--latest=false`) |
| 권장 용도 | 서비스 배포 | 콜드 로드 최적화 파일럿 측정 |

perf 베타 채널은 `beta-perf` 브랜치 하나가 항상 최신 perf 베타를 가리키는 **이동 ref**입니다. 자동 업데이터(`AITAutoUpdater`)는 prerelease 채널에 자동 업데이트 프롬프트를 띄우지 않으므로, 새 스냅샷이 나오면 별도(슬랙/이슈)로 안내받고 **직접 갱신**합니다.

---

## 옵트인 (설치)

### 방법 1: Package Manager

1. Unity Editor에서 `Window` > `Package Manager` 열기
2. 왼쪽 상단 `+` 버튼 클릭
3. `Add package from git URL...` 선택
4. Git URL 입력:

```
https://github.com/toss/apps-in-toss-unity-sdk.git#beta-perf
```

### 방법 2: manifest.json 직접 수정

프로젝트의 `Packages/manifest.json`에서 의존성을 `#beta-perf`로 지정:

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#beta-perf"
  }
}
```

기존에 stable로 핀되어 있었다면 fragment만 `#release/vX.Y.Z` → `#beta-perf`로 바꾸면 됩니다.

> 특정 perf 베타 스냅샷에 고정하고 싶다면 이동 브랜치 `#beta-perf` 대신 스냅샷 태그(예: `#release/v2.10.4-beta.efca5a3`)를 사용하세요. perf 베타 릴리즈 목록은 [GitHub Releases](https://github.com/toss/apps-in-toss-unity-sdk/releases)에서 `prerelease` 표시로 확인할 수 있습니다.

---

## 자동 적용 레버

아래 레버는 설정 없이 빌드 시 자동으로 적용됩니다.

- **WebGL LTO 코드 최적화** — `webGLCodeOptimization = -1` → `DiskSizeLTO` 자동 적용
- **IL2CPP OptimizeSize** — `il2cppCodeGeneration = -1` → `OptimizeSize` 자동 적용 (Unity 6+)
- **WebAssembly 2023 타겟** — `wasm2023 = -1` → WASM2023 활성화 (Unity 6+)
- **Decompression Fallback 비활성화** — `decompressionFallback = -1` → JS Brotli 디컴프레서 번들 제외
- **wasm Content-Type 재포장 훅** — 서버 Content-Type 설정과 무관하게 WebAssembly 스트리밍 컴파일을 유지
- **Mip Stripping** — `mipStripping = -1` → 미사용 밉맵 레벨 스트립
- **Strip Unused Mesh Components** — `stripUnusedMeshComponents = -1` → 미사용 메시 컴포넌트 스트립
- **데이터 캐싱 / CacheStorage 페이지 캐시** — `dataCaching = -1`, `pageCache = -1` → Unity 6+에서 dataCaching 활성화, 페이지 캐시 항상 활성화 (CacheStorage 부재 WebView에서는 IndexedDB로 폴백)
- **Native asset prefetch handoff** — `nativeAssetSource = -1` → 페이지 캐시와 연동하여 네이티브 에셋 prefetch 위임
- **Warm manifest emitter** — `warmManifest = -1` → 페이지 캐시 + warmManifest 연동 자동 활성화
- **Warm page emitter** — `warmPage = -1` → 페이지 캐시 + warmManifest 모두 ON일 때 자동 활성화

---

## 직접 켜야 하는 레버 (opt-in)

이 레버들은 기본값 그대로 두면 자동으로 실행을 시도하지만, **게임의 실제 에셋 경로나 임계값을 지정하지 않으면 조건 불충족으로 silent no-op**이 될 수 있습니다. 아래 표의 "주요 설정 필드"를 프로젝트에 맞게 조정하세요.

| 레버 | master switch (필드=값) | 주요 설정 필드 (기본값) | UI 위치 |
|---|---|---|---|
| **폰트 CJK 서브셋** | `fontSubset = -1` (Auto-ON) | `fontSubsetTargetPaths = ""` (비우면 ≥1MB 폰트 자동 감지)<br>`fontSubsetUnicodeRanges = ""` (비우면 프로젝트 전체 유니코드 스캔) | WebGL 최적화 설정 |
| **폰트 스트리밍** | `fontStreaming = -1` (Auto-ON) | `fontStreamingTargetPaths = ""` (manual 모드 전용, 비우면 no-op)<br>`fontStreamingMaxConcurrent = 2` | 고급 설정 |
| **텍스처 스트리밍** | `textureStreaming = -1` (Auto-ON) | `textureStreamingMinBytes = 524288`<br>`textureStreamingDirs = ""` (비우면 전체 프로젝트)<br>`textureStreamingExcludeDirs = ""`<br>`textureStreamingMaxConcurrent = 3` | 콘텐츠 최적화 — 텍스처 스트리밍 |
| **오디오 스트리밍** | `audioStreaming = -1` (Auto-ON) | `audioStreamingMinBytes = 262144`<br>`audioStreamingDirs = ""` (비우면 전체 프로젝트 AudioClip 대상) | WebGL 최적화 설정 |
| **텍스처 Crunch 압축** | `textureCrunch = -1` (Auto-ON) | `textureCrunchQuality = 50`<br>`textureCrunchMaxSize = 0` (0=무제한)<br>`textureCrunchAtlas = true`<br>`textureCrunchAtlasMaxSize = 0`<br>`textureCrunchDirs = ""` (비우면 전체 프로젝트) | WebGL 최적화 설정 |
| **텍스처 크기 클램프** | `textureSizeClamp = 1` (**반드시 1로 명시 설정** — 기본 -1은 auto-OFF) | `textureClampMaxSize = 1024`<br>`textureClampMinBytes = 0`<br>`textureClampDirs = ""`<br>`textureClampExcludeDirs = ""` | WebGL 최적화 설정 |
| **ASTC 블록 에스컬레이션** | `astcBlockEscalation = -1` (Auto-ON) | `astcBlockSize = 12`<br>`astcBlockMaxSize = 0`<br>`astcBlockAtlas = true`<br>`astcBlockDirs = ""`<br>`astcBlockExcludeDirs = ""` | WebGL 최적화 설정 |

> **텍스처 크기 클램프 주의**: 이 레버만 기본값 `-1`이 auto-OFF입니다. 활성화하려면 반드시 `textureSizeClamp = 1`로 명시해야 합니다.

---

## 설정 방법

1. Unity Editor 메뉴에서 **AIT > Configuration**을 엽니다.
2. 위 표의 "UI 위치" 열에 표시된 foldout을 펼칩니다.
   - **WebGL 최적화 설정** foldout: 오디오 스트리밍·텍스처 Crunch·텍스처 크기 클램프·ASTC 블록 에스컬레이션·폰트 CJK 서브셋
   - **콘텐츠 최적화 — 텍스처 스트리밍** foldout: 텍스처 스트리밍 (WebGL 최적화 설정 foldout 바로 아래 독립 foldout)
   - **고급 설정** foldout: 폰트 스트리밍
3. 각 레버의 tri-state 팝업에서 원하는 상태(자동/비활성/활성)를 선택하고, 필요한 경우 경로·임계값 필드를 입력합니다.
4. Inspector에서 직접 편집할 경우 `Assets/AppsInToss/Editor/AITConfig.asset`을 선택하면 동일한 필드를 Inspector에서 수정할 수 있습니다.

---

## 측정

1. 현재 stable 빌드와 perf 베타 채널 빌드를 동일 기기·네트워크 환경에서 각각 배포합니다.
2. opt-in 레버를 프로젝트의 실제 에셋 경로·임계값에 맞게 설정한 뒤 빌드를 생성합니다.
3. 콜드 로드 시간(첫 프레임 표시까지 소요 시간), 초기 다운로드 페이로드 크기를 두 빌드 간에 비교합니다.
4. 번들 어트리뷰션은 `window.AITLoading.buildVariant === "perf"` 조건으로 구분할 수 있습니다.

---

## 새 베타로 업데이트

perf 베타 채널은 자동 업데이트 프롬프트가 뜨지 않습니다(아래 [알아둘 점](#알아둘-점) 참조). 새 스냅샷 배포 안내를 받으면 **수동으로** 최신 `beta-perf` HEAD를 다시 당겨옵니다.

UPM은 git 의존성을 `Packages/packages-lock.json`에 커밋 해시로 잠그므로, 단순히 Unity를 다시 열어도 갱신되지 않습니다. 다음 중 하나로 갱신하세요:

- **Package Manager에서 제거 후 재추가** — 패키지를 remove → 같은 `#beta-perf` URL로 다시 add (HEAD가 재해석됨). 가장 간단합니다.
- **lock 해제** — `Packages/packages-lock.json`에서 `im.toss.apps-in-toss-unity-sdk` 항목의 `"hash"`를 지우고 저장 → Unity가 `#beta-perf` HEAD로 재해석.

---

## stable로 복귀 (repin)

파일럿이 끝났거나 안정 버전으로 돌아가려면 fragment를 **불변 stable 태그**로 바꿉니다:

```json
"im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/vX.Y.Z"
```

stable 태그로 핀하면 자동 업데이터가 다시 정상적으로 해당 stable ref를 추적합니다.

---

## 알아둘 점

- **자동 업데이트 없음**: 자동 업데이터는 prerelease 채널(`#beta-perf` 등)에 프롬프트를 띄우지 않습니다. `AIT` > `Check for Updates...`를 수동 실행하면 "베타 채널 — 수동 관리" 안내만 표시됩니다.
- **이동 브랜치**: `beta-perf`는 새 스냅샷 배포 시 force-push로 갱신됩니다. 재현 가능한 빌드가 필요하면 스냅샷 태그(예: `#release/v2.10.4-beta.efca5a3`)로 핀하세요.
- **Latest 아님**: perf 베타 GitHub Release는 항상 `prerelease`이며 Latest로 표시되지 않습니다. stable `latest` 릴리즈는 영향받지 않습니다.
- **opt-in 레버는 경로·조건 불충족 시 silent no-op**: `fontSubsetTargetPaths`, `fontStreamingTargetPaths`, `textureStreamingDirs`, `audioStreamingDirs` 등을 비워두면 자동 감지가 실행되지만 임계값을 충족하는 에셋이 없으면 아무것도 처리되지 않습니다. 실제 에셋 경로와 크기를 확인한 뒤 필드를 채우세요.
- **손실 레버는 품질과 크기를 교환합니다**: 텍스처 Crunch 압축, 텍스처 크기 클램프, ASTC 블록 에스컬레이션은 빌드 크기를 줄이는 대신 시각적 품질에 영향을 줄 수 있습니다. 품질 검수 후 프로덕션에 적용하세요.
- **서브타깃 제약**: ASTC 블록 에스컬레이션은 WebGL 빌드 subtarget이 ASTC일 때만 동작하며 DXT subtarget에서는 자동으로 비활성화됩니다. 텍스처 Crunch는 ASTC subtarget 빌드에서 자동 skip됩니다(경고 로그 출력).
- **파일럿 지원**: perf 베타 채널은 사내 실게임 실측을 위한 파일럿 전용 채널입니다. 적용 중 발견한 이슈나 측정 결과는 안내받은 채널로 공유해 주세요.
