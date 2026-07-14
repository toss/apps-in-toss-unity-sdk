# perf 베타 채널 (파일럿 전용)

이 문서는 **선택된 파일럿 제휴사**가 WebGL 콜드 로드 시간 단축을 목표로 하는 실험적 빌드 채널인 **perf 베타 채널**을 옵트인으로 미리 테스트하는 방법을 안내합니다.

> ⚠️ **주의 — perf 베타 채널은 production-ready가 아닙니다.**
> 이 채널은 콜드 로드 최적화 레버를 자사 게임에 적용·실측해 보려는 사전 협의된 파일럿 대상에게만 안내됩니다. 일반 서비스 배포에는 stable(`#release/vX.Y.Z`)을 사용하세요.

## 목차

- [stable과의 차이](#stable과의-차이)
- [옵트인 (설치)](#옵트인-설치)
- [자동 적용 레버 (무손실)](#자동-적용-레버-무손실)
- [품질 영향 레버 (빌드 시 기본 실행 · lossy)](#품질-영향-레버-빌드-시-기본-실행--lossy)
- [콘텐츠 외부화·자동 감지 레버 (무손실 · 경로 미설정 시 no-op)](#콘텐츠-외부화자동-감지-레버-무손실--경로-미설정-시-no-op)
- [명시 활성 전용 레버 (opt-in · 기본 OFF)](#명시-활성-전용-레버-opt-in--기본-off)
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

## 자동 적용 레버 (무손실)

아래 레버는 설정 없이 빌드 시 자동으로 적용되며 **시각·청취 품질을 바꾸지 않습니다(무손실)**. 엔진/전송/캐시 계층 최적화이므로 별도 QA 없이 안전합니다.

- **WebGL LTO 코드 최적화** — `webGLCodeOptimization = -1` → `DiskSizeLTO` 자동 적용
- **IL2CPP OptimizeSize** — `il2cppCodeGeneration = -1` → `OptimizeSize` 자동 적용 (Unity 6+)
- **WebAssembly 2023 타겟** — `wasm2023 = -1` → WASM2023 활성화 (Unity 6+)
- **Decompression Fallback 비활성화** — `decompressionFallback = -1` → JS Brotli 디컴프레서 번들 제외
- **wasm Content-Type 재포장 훅** — 서버 Content-Type 설정과 무관하게 WebAssembly 스트리밍 컴파일을 유지
- **Mip Stripping** — `mipStripping = -1` → 미사용 밉맵 레벨 스트립 (사용되는 밉만 보존 → 무손실)
- **Strip Unused Mesh Components** — `stripUnusedMeshComponents = -1` → 미사용 메시 컴포넌트 스트립
- **데이터 캐싱 / CacheStorage 페이지 캐시** — `dataCaching = -1`, `pageCache = -1` → Unity 6+에서 dataCaching 활성화, 페이지 캐시 항상 활성화 (CacheStorage 부재 WebView에서는 IndexedDB로 폴백)
- **Native asset prefetch handoff** — `nativeAssetSource = -1` → 페이지 캐시와 연동하여 네이티브 에셋 prefetch 위임
- **Warm manifest emitter** — `warmManifest = -1` → 페이지 캐시 + warmManifest 연동 자동 활성화
- **Warm page emitter** — `warmPage = -1` → 페이지 캐시 + warmManifest 모두 ON일 때 자동 활성화

---

## 품질 영향 레버 (빌드 시 기본 실행 · lossy)

> 🔴 **아래 레버는 기본값(`-1` = 자동)에서 빌드 시 실행되며 시각·청취 품질을 낮출 수 있습니다(lossy). opt-in이 아니라 opt-out입니다** — 끄려면 값을 `0`으로 두거나 캡 상향·폴더 제외로 조정하세요.
> 프로젝트 원본 임포트 설정은 빌드 후 항상 원상 복원되지만, **배포되는 산출물(.data/CDN)의 품질은 낮아지므로 프로덕션 전 반드시 품질 검수(QA)** 를 거치세요.

| 레버 | master switch (필드=값) | 자동 모드 동작 / 주요 필드 (기본값) | 손실 특성 | UI 위치 (Inspector Header) |
|---|---|---|---|---|
| **텍스처 Crunch 압축** | `textureCrunch = -1` (**auto-ON / opt-out**) | crunch(DXT 위 4~8x) 압축 + 선택적 크기 캡으로 reimport.<br>`textureCrunchQuality = 50`<br>`textureCrunchMaxSize = 0` (0=크기 캡 없음, crunch만)<br>`textureCrunchAtlas = true`<br>`textureCrunchAtlasMaxSize = 0`<br>`textureCrunchDirs = ""` (비우면 전체 프로젝트) | lossy(압축 아티팩트). **ASTC 서브타겟에서는 빌드 시 자동 skip**(경고 로그) | 콘텐츠 최적화 — 텍스처 crunch |
| **텍스처 크기 클램프** | `textureSizeClamp = -1` (**auto-ON / opt-out**) | maxTextureSize 를 **캡 2048로 강제**하여 텍셀 수를 줄임(format/crunch 불변).<br>⚠ **자동(-1) 모드는 직렬화된 `textureClampMaxSize`(기본 2048)를 사용하지 않고 항상 `GetDefaultTextureClampMaxSize()`=2048을 강제**합니다. 사용자 캡은 **명시 활성(`=1`)** 에서만 존중됩니다.<br>`textureClampMinBytes = 0`<br>`textureClampDirs = ""`<br>`textureClampExcludeDirs = ""` | lossy(표시 해상도↓). 캡 2048 초과(사실상 4096) 텍스처만 축소. opt-out: `=0` / 폴더 제외 / (`=1` 시)캡 상향 | 콘텐츠 최적화 — 텍스처 크기 클램프 (lossy, 기본 ON) |
| **ASTC 블록 에스컬레이션** | `astcBlockEscalation = -1` (**auto-ON / opt-out**) | 더 큰 ASTC 블록(기본 12x12)으로 reimport 하여 on-wire 크기 축소.<br>`astcBlockSize = 12`<br>`astcBlockMaxSize = 0`<br>`astcBlockAtlas = true`<br>`astcBlockDirs = ""`<br>`astcBlockExcludeDirs = ""` | lossy(블록 확대 화질↓). **ASTC 서브타겟 전용 — DXT(기본) 서브타겟에서는 자동 skip** | 콘텐츠 최적화 — ASTC 블록 에스컬레이션 |
| **오디오 재인코딩** | `audioReencode = -1` (**auto-ON / opt-out**) | AudioImporter base 설정(WebGL 이 ship 하는 `defaultSampleSettings`)을 Vorbis + quality 로 변경·reimport.<br>**자동 모드는 비압축(PCM)/ADPCM 만 Vorbis 로 변환하고 이미 Vorbis 인 클립은 건드리지 않음**(세대손실 없이 near-transparent).<br>`audioReencodeQuality = 0.7` (자동 모드에서도 이 값 사용)<br>`audioReencodeMinBytes = 0`<br>`audioReencodeDirs = ""`<br>`audioReencodeExcludeDirs = ""` | lossy(오디오 재인코딩, .data/CDN 오디오 품질 영향). `audioStreaming` 으로 외부화된 클립은 대상 제외 | 콘텐츠 최적화 — 오디오 재인코딩 (lossy, 기본 ON) |
| **스트림 사본 다운스케일** | `textureStreamDownscale = -1` (**auto-ON / opt-out**) | `textureStreaming` 이 외부화한 **스트림 사본(StreamingAssets / CDN 배포본)** 을 캡보다 크면 균일 배율로 축소.<br>⚠ **자동(-1) 모드는 직렬화된 `textureStreamDownscaleMaxSize`(기본 2048)를 사용하지 않고 항상 `GetDefaultTextureStreamDownscaleMaxSize()`=2048을 강제**합니다(캡은 `=1` 에서만 존중). | **CDN 전용 lossy** — 프로젝트 원본 불변, 스트림은 비-부팅이라 **로딩속도·부팅 무영향(CDN 무압축 총량만 감소)**. `textureStreaming` 이 외부화한 텍스처에만 적용 | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |

> **⚠ 자동 모드 캡 주의 (구버전 config 잔재 방지)**: `textureSizeClamp`·`textureStreamDownscale` 는 자동(`-1`) 모드에서 각각 `textureClampMaxSize`·`textureStreamDownscaleMaxSize` **직렬화 값을 무시하고 항상 2048로 강제**합니다. 사용자가 지정한 캡은 **명시 활성(값 `1`)** 일 때만 적용됩니다. (클램프가 opt-in 이던 구버전 `AITConfig.asset` 에 박제된 옛 캡이 posture 플립 이후 의도 없이 조용히 적용되는 것을 막기 위한 설계입니다.)

---

## 콘텐츠 외부화·자동 감지 레버 (무손실 · 경로 미설정 시 no-op)

이 레버들도 기본값(`-1` = 자동)에서 실행을 시도하지만 **표시 품질을 낮추지 않으며(무손실)**, 게임의 실제 에셋 경로·임계값을 지정하지 않으면 조건 불충족으로 **silent no-op**이 될 수 있습니다. "주요 설정 필드"를 프로젝트에 맞게 조정하세요. (단, 폰트 레버는 아래 **동적 텍스트 리스크**를 참고하세요.)

| 레버 | master switch (필드=값) | 주요 설정 필드 (기본값) | 특성 | UI 위치 |
|---|---|---|---|---|
| **텍스처 스트리밍** | `textureStreaming = -1` (Auto-ON) | `textureStreamingMinBytes = 524288`<br>`textureStreamingDirs = ""` (비우면 전체 프로젝트)<br>`textureStreamingExcludeDirs = ""`<br>`textureStreamingMaxConcurrent = 3` | 무손실(비-부팅 대형 텍스처를 StreamingAssets 로 외부화 → 초기 다운로드/TTFF↓, 런타임 복원 시 픽셀 동일) | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |
| **스트림 PNG 무손실 재압축** | `textureStreamRecompress = -1` (Auto-ON) | (tri-state 전용, 추가 필드 없음) | 무손실(oxipng WASM, 픽셀 불변 — 필터/deflate 재탐색만). CDN 무압축 총량↓. `textureStreaming` 파이프라인 일부 | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |
| **오디오 스트리밍** | `audioStreaming = -1` (Auto-ON) | `audioStreamingMinBytes = 262144`<br>`audioStreamingDirs = ""` (비우면 전체 프로젝트 AudioClip 대상) | 무손실(>256KB AudioClip 을 외부화·런타임 비동기 복원 → TTI↓) | 콘텐츠 최적화 — 오디오 스트리밍 |
| **폰트 CJK 서브셋** | `fontSubset = -1` (Auto-ON) | `fontSubsetTargetPaths = ""` (비우면 ≥1MB 폰트 자동 감지)<br>`fontSubsetUnicodeRanges = ""` (비우면 프로젝트 전체 유니코드 스캔)<br>`fontSubsetExtraRanges = ""` (합집합 보강)<br>`fontSubsetExcludeTargetPaths = ""` (제외) | ⚠ **동적 텍스트 lossy 가능** — 보존 범위 밖 글자를 제거합니다. 프로젝트에 등장하는 문자체계는 블록 전체를 보존하지만, 프로젝트에 없는 문자체계를 외부에서 동적 로드하면 □(tofu)가 될 수 있으므로 `fontSubsetExtraRanges`/제외로 보강 | 콘텐츠 최적화 — 폰트 CJK subset |
| **폰트 스트리밍** | `fontStreaming = -1` (Auto-ON) | `fontStreamingTargetPaths = ""` (manual 모드 전용, 비우면 자동 감지)<br>`fontStreamingMaxConcurrent = 2` | 무손실(재수화 후 픽셀 동일)이나 ⚠ **재수화 전(또는 TMP 부재 시) 대상 폰트 글자는 □ 로 렌더**. 비-부팅 ≥1MB TMP 폰트 외부화 | 콘텐츠 최적화 — 대형 폰트 deferral |

---

## 명시 활성 전용 레버 (opt-in · 기본 OFF)

아래 레버는 **기본값 `-1` 이 auto-OFF** 입니다(품질 검증 게이트 미통과). 켜려면 반드시 값을 `1`로 명시해야 하며, **lossy 이므로 켠 뒤 반드시 품질 검수**하세요. 두 레버 모두 프로젝트 원본은 건드리지 않고 **외부화된 스트림 사본(CDN 배포본)만** 교체합니다.

| 레버 | master switch (필드=값) | 주요 설정 필드 (기본값) | 손실 특성 | UI 위치 |
|---|---|---|---|---|
| **스트리밍 오디오 트랜스코딩** | `audioStreamTranscode = 1` (**기본 `-1` = auto-OFF**) | `audioStreamTranscodeBitrateKbps = 160`<br>`audioStreamTranscodeMinSourceKbps = 256` | lossy(`audioStreaming` 이 외부화한 MP3 사본 → 저비트레이트 MP3). 소스가 이미 lossy(MP3)라 **세대손실 누적**, 루핑 BGM 은 인코더 delay/padding 으로 **루프 이음새 갭 위험** → 청취 검증 전 기본 OFF | 콘텐츠 최적화 — 오디오 스트리밍 |
| **스트림 PNG → JPEG** | `textureStreamJpeg = 1` (**기본 `-1` = auto-OFF**) | `textureStreamJpegQuality = 90` | lossy(알파 없는 불투명 RGB 스트림 사본을 JPEG 로 전환). **DCT 아티팩트(플랫 아트 ringing 등) 위험** → 시각 검증 전 기본 OFF | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |

---

## 설정 방법

1. Unity Editor 메뉴에서 **AIT > Configuration**을 엽니다.
2. 위 표의 "UI 위치" 열에 표시된 foldout/Header 를 펼칩니다. 콘텐츠 최적화 레버는 Inspector 에서 각 `콘텐츠 최적화 — …` Header 아래에 위치합니다.
   - **품질 영향 레버 (lossy, 기본 ON)**: 텍스처 Crunch·텍스처 크기 클램프·ASTC 블록 에스컬레이션·오디오 재인코딩·스트림 사본 다운스케일 — 기본값에서 이미 켜져 있으므로, 끄거나 캡을 조정하려면 이 그룹을 확인하세요.
   - **콘텐츠 외부화 레버**: 텍스처 스트리밍·오디오 스트리밍·폰트 CJK 서브셋(콘텐츠 최적화 Header), 폰트 스트리밍(콘텐츠 최적화 — 대형 폰트 deferral)
   - **명시 활성 전용 레버 (기본 OFF)**: 스트리밍 오디오 트랜스코딩·스트림 PNG→JPEG — 켜려면 값을 `1`로 설정
3. 각 레버의 tri-state 팝업에서 원하는 상태(자동/비활성/활성)를 선택하고, 필요한 경우 경로·임계값 필드를 입력합니다. **lossy 레버를 끄려면 `0`(비활성)** 을, 사용자 캡을 존중시키려면 `1`(활성) 을 선택하세요.
4. Inspector에서 직접 편집할 경우 `Assets/AppsInToss/Editor/AITConfig.asset`을 선택하면 동일한 필드를 Inspector에서 수정할 수 있습니다.

---

## 측정

1. 현재 stable 빌드와 perf 베타 채널 빌드를 동일 기기·네트워크 환경에서 각각 배포합니다.
2. 레버를 프로젝트의 실제 에셋 경로·임계값에 맞게 설정한 뒤 빌드를 생성합니다. (lossy 레버는 기본 ON 이므로, 품질 검수 결과에 따라 끄거나 캡을 조정하세요.)
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
- **🔴 손실(lossy) 레버는 기본 실행됩니다 — 프로덕션 전 QA 필수**: `textureCrunch`(Crunch 압축), `textureSizeClamp`(해상도 상한 강제, 자동 캡 2048), `astcBlockEscalation`(ASTC 블록 확대), `audioReencode`(오디오 Vorbis 재인코딩), `textureStreamDownscale`(스트림 사본 다운스케일)은 **기본값에서 빌드 시 실행되며(opt-out)** 빌드 크기를 줄이는 대신 시각·청취 품질에 영향을 줄 수 있습니다. 프로젝트 원본 임포트 설정은 빌드 후 복원되지만 **배포 산출물(.data/CDN)의 품질은 낮아지므로 반드시 품질 검수 후** 적용하고, 필요하면 값을 `0`으로 끄거나 캡·폴더로 조정하세요.
- **명시 활성 전용 lossy 레버 (기본 OFF)**: `audioStreamTranscode`(스트리밍 MP3 저비트레이트 재인코딩), `textureStreamJpeg`(불투명 스트림 PNG→JPEG)는 품질 검증 게이트 미통과로 **기본 OFF** 입니다. 켜려면 값을 `1`로 명시하고, 켠 뒤 반드시 청취/시각 검증하세요.
- **자동 감지 레버는 경로·조건 불충족 시 silent no-op**: `textureStreaming`·`audioStreaming`·`fontSubset`·`fontStreaming` 은 `textureStreamingDirs`·`audioStreamingDirs`·`fontSubsetTargetPaths`·`fontStreamingTargetPaths` 등을 비워두면 자동 감지가 실행되지만 임계값을 충족하는 에셋이 없으면 아무것도 처리되지 않습니다. 실제 에셋 경로와 크기를 확인한 뒤 필드를 채우세요.
- **폰트 레버의 동적 텍스트 리스크**: `fontSubset`은 보존 범위 밖 글자를 제거하므로, 프로젝트에 등장하지 않는 문자체계를 외부에서 동적으로 받아 표시하면 □(tofu)가 될 수 있습니다. `fontStreaming`은 재수화 전 대상 폰트 글자가 □로 렌더됩니다. 필요 시 `fontSubsetExtraRanges` 보강 또는 대상 제외로 대응하세요.
- **서브타깃 제약**: ASTC 블록 에스컬레이션은 WebGL 빌드 subtarget이 ASTC일 때만 동작하며 DXT subtarget에서는 자동으로 비활성화됩니다. 텍스처 Crunch는 ASTC subtarget 빌드에서 자동 skip됩니다(경고 로그 출력).
- **파일럿 지원**: perf 베타 채널은 사내 실게임 실측을 위한 파일럿 전용 채널입니다. 적용 중 발견한 이슈나 측정 결과는 안내받은 채널로 공유해 주세요.
