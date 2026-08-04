# perf 베타 채널

WebGL 콜드 로드 시간을 줄이는 실험적 최적화 레버를 미리 적용해 보는 옵트인 채널입니다. 자사 게임에 적용하고 실측하려는, 사전 협의된 파일럿 제휴사에게만 안내됩니다.

> **주의**: perf 베타 채널은 production-ready가 아닙니다. 일반 서비스 배포에는 stable 릴리즈 태그(`#release/vX.Y.Z`)를 사용하세요.

## stable 과 무엇이 다른가

| 항목 | stable (v3.0.1 기준) | perf 베타 채널 |
|------|--------|----------------|
| 설치 ref | `#release/vX.Y.Z` (불변 태그) | `#beta-perf` (이동 브랜치) |
| 콜드 로드 최적화 레버 | 없음 | 자동 적용(무손실) + 빌드 시 기본 실행되는 손실 레버 + 명시 활성 레버 |
| 번들 마킹 | 없음 | `.ait` 헤더와 `window.AITLoading.buildVariant`에 `"perf"` 주입 |
| 자동 업데이트 프롬프트 | 표시됨 | 표시 안 됨 (수동 관리) |
| GitHub Release 표시 | Latest | prerelease |
| 권장 용도 | 서비스 배포 | 콜드 로드 최적화 파일럿 측정 |

`beta-perf` 브랜치 하나가 항상 최신 perf 베타를 가리키는 **이동 ref**입니다.

> **참고**: 이 문서가 설명하는 레버와 설정 필드는 현행 stable 릴리즈(v3.0.1) 기준으로 **perf 베타 채널에만 존재합니다.** 설치 ref의 SDK 버전에 레버가 없으면 아래 설정 필드도 보이지 않는 것이 정상입니다. 이후 stable 릴리즈에 레버가 포함되는 경우 해당 릴리즈 노트가 이 표보다 우선합니다. stable/비채널로 빌드한 번들의 `window.AITLoading.buildVariant`는 빈 문자열입니다.

## 옵트인

`Packages/manifest.json`의 fragment를 `#beta-perf`로 바꿉니다.

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#beta-perf"
  }
}
```

설치 ref를 다루는 방법 전반과, 이동 ref를 최신으로 다시 당겨오는 절차는 [시작하기](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/getting-started)의 설치 ref 관리 절에 정리되어 있습니다. perf 베타 채널이라서 다른 점은 없습니다.

특정 스냅샷에 고정하려면 이동 브랜치 대신 스냅샷 태그(`#release/vX.Y.Z-beta.<해시>`)를 쓰세요.

## 자동 적용 레버 (무손실)

설정 없이 빌드 시 자동으로 적용되며 **시각·청취 품질을 바꾸지 않습니다(무손실)**. 엔진/전송/캐시 계층 최적화이므로 별도 QA 없이 안전합니다.

| 레버 | 효과 |
|------|------|
| WebGL LTO 코드 최적화 | `DiskSizeLTO`로 코드 크기 축소 |
| IL2CPP OptimizeSize | 크기 우선 코드 생성 (Unity 6 이상) |
| WebAssembly 2023 타겟 | WASM2023 활성화 (Unity 6 이상) |
| Decompression Fallback 비활성화 | JS Brotli 디컴프레서를 번들에서 제외 |
| wasm Content-Type 재포장 훅 | 서버 Content-Type과 무관하게 스트리밍 컴파일 유지 |
| Mip Stripping | 사용되는 밉만 보존하고 나머지 레벨 제거 |
| Strip Unused Mesh Components | 미사용 메시 컴포넌트 제거 |
| 데이터 캐싱과 CacheStorage 페이지 캐시 | 재방문 로드 단축. CacheStorage가 없는 WebView는 IndexedDB로 폴백 |
| Native asset prefetch handoff | 페이지 캐시와 연동해 네이티브 에셋 prefetch 위임 |
| Warm manifest emitter | 페이지 캐시와 연동해 자동 활성화 |
| Warm page emitter | 페이지 캐시와 warm manifest가 모두 켜졌을 때 자동 활성화 |

## 품질 영향 레버 (빌드 시 기본 실행 · lossy)

> 🔴 **아래 레버는 기본값(`-1` = 자동)에서 빌드 시 실행되며 시각·청취 품질을 낮출 수 있습니다(lossy). opt-in이 아니라 opt-out입니다** — 끄려면 값을 `0`으로 두거나 캡 상향·폴더 제외로 조정하세요. 프로젝트 원본 임포트 설정은 빌드 후 항상 원상 복원되지만, **배포되는 산출물(.data/CDN)의 품질은 낮아지므로 프로덕션 전 반드시 품질 검수(QA)** 를 거치세요.

| 레버 | 마스터 스위치 | 자동 모드 동작 / 주요 필드 (기본값) | 손실 특성 | 설정 위치 |
|------|---------------|--------------------------------------|-----------|-----------|
| 텍스처 Crunch 압축 | `textureCrunch = -1` (auto-ON / opt-out) | crunch(DXT 위 4~8x) 압축 + 선택적 크기 캡으로 reimport.<br>`textureCrunchQuality = 50`<br>`textureCrunchMaxSize = 0` (0=크기 캡 없음, crunch만)<br>`textureCrunchAtlas = true`<br>`textureCrunchAtlasMaxSize = 0`<br>`textureCrunchDirs = ""` (비우면 전체 프로젝트) | lossy(압축 아티팩트). ASTC 서브타겟에서는 빌드 시 자동 skip(경고 로그) | 콘텐츠 최적화 — 텍스처 crunch |
| 텍스처 크기 클램프 | `textureSizeClamp = -1` (auto-ON / opt-out) | maxTextureSize를 캡 2048로 강제해 텍셀 수를 줄임(format/crunch 불변).<br>`textureClampMinBytes = 0`<br>`textureClampDirs = ""`<br>`textureClampExcludeDirs = ""` | lossy(표시 해상도↓). 캡 2048 초과(사실상 4096) 텍스처만 축소. opt-out: `=0` / 폴더 제외 / (`=1` 시) 캡 상향 | 콘텐츠 최적화 — 텍스처 크기 클램프 |
| ASTC 블록 에스컬레이션 | `astcBlockEscalation = -1` (auto-ON / opt-out) | 더 큰 ASTC 블록(기본 12x12)으로 reimport하여 on-wire 크기 축소.<br>`astcBlockSize = 12`<br>`astcBlockMaxSize = 0`<br>`astcBlockAtlas = true`<br>`astcBlockDirs = ""`<br>`astcBlockExcludeDirs = ""` | lossy(블록 확대 화질↓). ASTC 서브타겟 전용 — DXT(기본) 서브타겟에서는 자동 skip | 콘텐츠 최적화 — ASTC 블록 에스컬레이션 |
| 오디오 재인코딩 | `audioReencode = -1` (auto-ON / opt-out) | AudioImporter base 설정(WebGL이 ship하는 `defaultSampleSettings`)을 Vorbis + quality로 변경·reimport. 자동 모드는 비압축(PCM)/ADPCM만 Vorbis로 변환하고 이미 Vorbis인 클립은 건드리지 않음(near-transparent).<br>`audioReencodeQuality = 0.7`<br>`audioReencodeMinBytes = 0`<br>`audioReencodeDirs = ""`<br>`audioReencodeExcludeDirs = ""` | lossy(오디오 재인코딩, .data/CDN 오디오 품질 영향). `audioStreaming`으로 외부화된 클립은 대상 제외 | 콘텐츠 최적화 — 오디오 재인코딩 |
| 스트림 사본 다운스케일 | `textureStreamDownscale = -1` (auto-ON / opt-out) | `textureStreaming`이 외부화한 스트림 사본(StreamingAssets / CDN 배포본)을 캡보다 크면 균일 배율로 축소. | CDN 전용 lossy — 프로젝트 원본 불변, 스트림은 비-부팅이라 로딩속도·부팅 무영향(CDN 무압축 총량만 감소). `textureStreaming`이 외부화한 텍스처에만 적용 | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |

마스터 스위치는 세 상태를 갖습니다 — `-1`은 자동, `0`은 비활성, `1`은 명시적 활성입니다. 이 절과 다음 절의 레버는 **자동이 곧 활성**이므로, 끄려면 `0`으로 명시해야 합니다. ([명시 활성 전용 레버](#명시-활성-전용-레버-opt-in--기본-off)만 자동이 곧 비활성입니다.)

> **주의**: 텍스처 크기 클램프의 기본 상한은 2048입니다. HiDPI 헤드룸을 감안한 값으로, 화면 일부를 차지하는 스프라이트·UI·아이콘에는 충분하고 full-bleed 배경만 최고 DPR 기기에서 약간 소프트해집니다. 1024로 낮추면 DPR 2 풀스크린에서도 뭉개질 수 있습니다. 의도적으로 고해상도를 유지해야 하는 에셋은 상한을 올리거나 `textureClampExcludeDirs`로 빼세요. 빌드가 끝나면 원본 임포트 설정으로 복원됩니다.

> **자동 모드 캡 주의**: `textureSizeClamp`·`textureStreamDownscale`는 자동(`-1`) 모드에서 각각 `textureClampMaxSize`·`textureStreamDownscaleMaxSize` 직렬화 값을 무시하고 항상 2048을 강제합니다. 사용자가 지정한 캡은 명시 활성(값 `1`)일 때만 적용됩니다. (클램프가 opt-in이던 구버전 `AITConfig.asset`에 남아있던 옛 캡이 posture 전환 이후 의도 없이 조용히 적용되는 것을 막기 위한 설계입니다.)

## 콘텐츠 외부화·자동 감지 레버 (무손실 · 경로 미설정 시 no-op)

이 레버들도 기본값(`-1` = 자동)에서 실행을 시도하지만 **표시 품질을 낮추지 않으며(무손실)**, 게임의 실제 에셋 경로·임계값을 지정하지 않으면 조건 불충족으로 silent no-op이 될 수 있습니다. 아래 표의 "주요 설정 필드"를 프로젝트에 맞게 조정하세요. (폰트 레버는 [알아둘 점](#알아둘-점)의 동적 텍스트 리스크도 참고하세요.)

| 레버 | 마스터 스위치 | 주요 설정 필드 (기본값) | 특성 | 설정 위치 |
|------|---------------|--------------------------|------|-----------|
| 텍스처 스트리밍 | `textureStreaming = -1` | `textureStreamingMinBytes = 524288`<br>`textureStreamingDirs = ""` (비우면 전체)<br>`textureStreamingExcludeDirs = ""`<br>`textureStreamingMaxConcurrent = 3` | 무손실(비-부팅 대형 텍스처를 StreamingAssets로 외부화 → 초기 다운로드/TTFF↓, 런타임 복원 시 픽셀 동일) | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |
| 스트림 PNG 무손실 재압축 | `textureStreamRecompress = -1` | (tri-state 전용, 추가 필드 없음) | 무손실(oxipng WASM, 픽셀 불변 — 필터/deflate 재탐색만). CDN 무압축 총량↓. `textureStreaming` 파이프라인 일부 | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |
| 오디오 스트리밍 | `audioStreaming = -1` | `audioStreamingMinBytes = 262144`<br>`audioStreamingDirs = ""` (비우면 전체 AudioClip) | 무손실(256KB 초과 AudioClip을 외부화·런타임 비동기 복원 → TTI↓) | 콘텐츠 최적화 — 오디오 스트리밍 |
| 폰트 CJK 서브셋 | `fontSubset = -1` | `fontSubsetLanguages = ""` (동적 텍스트에 나올 언어 선택, 쉼표 구분 태그)<br>`fontSubsetTargetPaths = ""` (비우면 1MB 이상 폰트 자동 감지)<br>`fontSubsetUnicodeRanges = ""` (비우면 프로젝트 전체 스캔)<br>`fontSubsetExtraRanges = ""` (합집합 보강)<br>`fontSubsetExcludeTargetPaths = ""` (제외) | 동적 텍스트 lossy 가능 — 보존 범위 밖 글자를 제거. 프로젝트에 등장하는 문자체계는 블록 전체를 보존하지만, 등장하지 않는 문자체계를 외부에서 동적 로드하면 □(tofu)가 될 수 있음. **자동 모드는 `fontSubsetLanguages`·`fontSubsetUnicodeRanges`·`fontSubsetExtraRanges`·`fontSubsetTargetPaths`가 모두 비어 있으면 인지된 선택이 없다고 보아 서브셋 자체를 실행하지 않음**(선택 = 인지된 활성화) | 콘텐츠 최적화 — 폰트 CJK subset |
| 폰트 스트리밍 | `fontStreaming = -1` | `fontStreamingTargetPaths = ""` (manual 모드 전용, 비우면 자동 감지)<br>`fontStreamingMaxConcurrent = 2` | 무손실(재수화 후 픽셀 동일)이나 재수화 전(또는 TMP 부재 시) 대상 폰트 글자는 □로 렌더. 비-부팅 1MB 이상 TMP 폰트 외부화 | 콘텐츠 최적화 — 대형 폰트 deferral |

## 명시 활성 전용 레버 (opt-in · 기본 OFF)

아래 레버는 **기본값 `-1`이 auto-OFF**입니다(품질 검증 게이트 미통과). 켜려면 반드시 값을 `1`로 명시해야 하며, lossy이므로 켠 뒤 반드시 품질 검수하세요. 두 레버 모두 프로젝트 원본은 건드리지 않고 외부화된 스트림 사본(CDN 배포본)만 교체합니다.

| 레버 | 마스터 스위치 | 주요 설정 필드 (기본값) | 손실 특성 | 설정 위치 |
|------|---------------|--------------------------|-----------|-----------|
| 스트리밍 오디오 트랜스코딩 | `audioStreamTranscode = 1` (기본 `-1` = auto-OFF) | `audioStreamTranscodeBitrateKbps = 160`<br>`audioStreamTranscodeMinSourceKbps = 256` | lossy(`audioStreaming`이 외부화한 MP3 사본 → 저비트레이트 MP3). 소스가 이미 lossy(MP3)라 세대손실이 누적되고, 루핑 BGM은 인코더 delay/padding으로 루프 이음새 갭이 생길 수 있어 청취 검증 전 기본 OFF | 콘텐츠 최적화 — 오디오 스트리밍 |
| 스트림 PNG → JPEG | `textureStreamJpeg = 1` (기본 `-1` = auto-OFF) | `textureStreamJpegQuality = 90` | lossy(불투명 스트림 사본을 JPEG로 전환 — 알파 없는 RGB, 또는 알파 스캔으로 전량 불투명이 확인된 RGBA. gray/palette는 계속 제외). DCT 아티팩트(플랫 아트 ringing 등) 위험이 있어 시각 검증 전 기본 OFF | 콘텐츠 최적화 — 대형 텍스처 스트리밍 |

## 설정 방법

1. `AIT` > `Configuration`을 엽니다.
2. 위 표의 설정 위치에 해당하는 foldout/Header를 펼칩니다. 콘텐츠 최적화 레버는 Inspector에서 각 `콘텐츠 최적화 — …` Header 아래에 위치합니다.
   - **자동 적용 레버 (무손실)**: 설정 없이 항상 적용되므로 별도 확인 불필요
   - **품질 영향 레버 (lossy, 기본 ON)**: 텍스처 Crunch·텍스처 크기 클램프·ASTC 블록 에스컬레이션·오디오 재인코딩·스트림 사본 다운스케일 — 기본값에서 이미 켜져 있으므로, 끄거나 캡을 조정하려면 이 그룹을 확인하세요
   - **콘텐츠 외부화·자동 감지 레버**: 텍스처 스트리밍·스트림 PNG 무손실 재압축·오디오 스트리밍·폰트 CJK 서브셋(콘텐츠 최적화 Header), 폰트 스트리밍(콘텐츠 최적화 — 대형 폰트 deferral)
   - **명시 활성 전용 레버 (기본 OFF)**: 스트리밍 오디오 트랜스코딩·스트림 PNG→JPEG — 켜려면 값을 `1`로 설정
3. 각 레버의 팝업에서 자동·비활성·활성 중 하나를 고르고, 필요하면 경로와 임계값을 입력합니다. lossy 레버를 끄려면 `0`(비활성)을, 사용자 캡을 존중시키려면 `1`(활성)을 선택하세요. (폰트 CJK 서브셋만 예외적으로 팝업이 자동/비활성/명시 활성(스캔 단독 실행)/수동 설정 4가지입니다 — 자동은 동적 텍스트 언어를 선택해야 실행되고, 명시 활성·수동 설정은 값 `1`로 동일하되 대상/범위 override 유무로 구분됩니다.)
4. `Assets/AppsInToss/Editor/AITConfig.asset`을 선택하면 같은 필드를 Inspector에서 직접 편집할 수도 있습니다.

어떤 레버가 실제로 적용됐는지는 빌드 로그가 최종 확인 수단입니다. 레버마다 활성 여부와 자동 여부가 함께 출력됩니다.

## 측정

1. 현행 stable 빌드와 perf 베타 빌드를 같은 기기, 같은 네트워크 조건에서 각각 배포합니다.
2. 레버를 프로젝트의 실제 에셋 경로와 임계값에 맞게 설정한 뒤 빌드합니다. lossy 레버는 기본 ON이므로, 품질 검수 결과에 따라 끄거나 캡을 조정하세요.
3. 콜드 로드 시간(첫 프레임 표시까지)과 초기 다운로드 페이로드 크기를 두 빌드 사이에서 비교합니다.
4. 어느 번들이 어느 채널 것인지는 `window.AITLoading.buildVariant === "perf"`로 구분합니다.

첫 프레임 시각은 SDK가 자동 수집하는 `unity_first_interactive` 이벤트로도 얻을 수 있습니다. 자세한 내용은 [SDK 이벤트 로깅](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/metrics)을 참고하세요.

## stable 로 복귀

fragment를 불변 stable 태그로 되돌립니다.

```json
"im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/vX.Y.Z"
```

레버가 없는 stable 버전(v3.0.1 이하)으로 돌아가면 위 레버와 설정 필드가 함께 사라집니다. `AITConfig.asset`에 남아 있던 값은 읽히지 않으므로 따로 정리할 필요는 없습니다.

## 알아둘 점

- **자동 업데이트 없음**: `beta-perf`도 prerelease 채널로 판정되어 자동 업데이트 프롬프트가 뜨지 않습니다. 새 스냅샷은 안내를 받고 직접 갱신하세요.
- **재현 가능한 빌드**: `beta-perf`는 force-push로 갱신되는 이동 ref입니다. 측정 결과를 재현해야 한다면 스냅샷 태그로 핀하세요.
- **번들 마킹은 채널 배포본에만 있습니다**: `"perf"` 마킹은 채널 발행 시점에 주입되므로 `#beta-perf` 또는 스냅샷 태그로 설치한 경우에만 적용됩니다. 저장소의 개발 브랜치를 직접 핀해서 빌드하면 `buildVariant`가 빈 문자열이라 측정에서 stable 번들과 구분되지 않습니다 — 반드시 채널 ref로 설치하세요.
- **손실(lossy) 레버는 기본 실행됩니다 — 프로덕션 전 QA 필수**: 텍스처 Crunch 압축, 텍스처 크기 클램프(자동 캡 2048), ASTC 블록 에스컬레이션, 오디오 재인코딩, 스트림 사본 다운스케일은 기본값에서 빌드 시 실행되며(opt-out) 빌드 크기를 줄이는 대신 시각·청취 품질에 영향을 줄 수 있습니다. 프로젝트 원본 임포트 설정은 빌드 후 복원되지만 배포 산출물(.data/CDN)의 품질은 낮아지므로 반드시 품질 검수 후 적용하고, 필요하면 값을 `0`으로 끄거나 캡·폴더로 조정하세요.
- **명시 활성 전용 lossy 레버(기본 OFF)**: 스트리밍 오디오 트랜스코딩(저비트레이트 재인코딩), 스트림 PNG→JPEG는 품질 검증 게이트를 통과하지 못해 기본 OFF입니다. 켜려면 값을 `1`로 명시하고, 켠 뒤 반드시 청취/시각 검증하세요.
- **조건 불충족은 조용히 지나갑니다**: `textureStreaming`·`audioStreaming`·`fontStreaming`은 대상 경로 필드를 비워두면 자동 감지가 돌지만, 임계값을 넘는 에셋이 없으면 아무것도 처리되지 않습니다. `fontSubset`은 자동 모드에서 `fontSubsetLanguages`·`fontSubsetUnicodeRanges`·`fontSubsetExtraRanges`·`fontSubsetTargetPaths`가 모두 비어 있으면 동적 텍스트 언어가 선택되지 않은 것으로 보아 서브셋 자체를 건너뜁니다(선택 = 인지된 활성화). 에셋의 실제 경로와 크기를 확인한 뒤 필드를 채우세요.
- **폰트 레버의 동적 텍스트 리스크**: `fontSubset`은 보존 범위 밖 글자를 제거하므로, 프로젝트에 등장하지 않는 문자체계를 외부에서 동적으로 받아 표시하면 □(tofu)가 될 수 있습니다. Configuration 창에서 동적 텍스트(닉네임·채팅 등)에 나올 수 있는 언어를 `fontSubsetLanguages`로 선택하면 해당 언어의 유니코드 범위가 보존 범위에 합류됩니다. 그 밖의 세부 범위 보강이 필요하면 `fontSubsetExtraRanges` 또는 대상 제외(`fontSubsetExcludeTargetPaths`)로 대응하세요. `fontStreaming`은 재수화 전 대상 폰트 글자가 □로 렌더됩니다.
- **서브타깃 제약**: ASTC 블록 에스컬레이션은 WebGL 빌드 subtarget이 ASTC일 때만 동작하고 DXT에서는 자동 비활성화됩니다. 텍스처 Crunch는 반대로 ASTC subtarget 빌드에서 경고 로그를 남기고 건너뜁니다.
- **파일럿 지원**: 적용 중 발견한 이슈나 측정 결과는 안내받은 채널로 공유해 주세요.

## 관련 문서

- [시작하기](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/getting-started) — 설치 ref 관리 공통 절차
- [베타 채널](BetaChannel.md) — web-framework 메이저 업그레이드 파일럿 채널
- [SDK 이벤트 로깅](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/metrics) — 첫 프레임 시각 계측
- [빌드 프로필](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-profiles) — stable에도 있는 압축·스트리핑 설정
