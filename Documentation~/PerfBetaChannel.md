# perf 베타 채널

WebGL 콜드 로드 시간을 줄이는 실험적 최적화 레버를 미리 적용해 보는 옵트인 채널입니다. 자사 게임에 적용하고 실측하려는, 사전 협의된 파일럿 제휴사에게만 안내됩니다.

> **주의**: perf 베타 채널은 production-ready가 아닙니다. 일반 서비스 배포에는 stable 릴리즈 태그(`#release/vX.Y.Z`)를 사용하세요.

## stable 과 무엇이 다른가

| 항목 | stable | perf 베타 채널 |
|------|--------|----------------|
| 설치 ref | `#release/vX.Y.Z` (불변 태그) | `#beta-perf` (이동 브랜치) |
| 콜드 로드 최적화 레버 | 없음 | 자동 적용 + 직접 확인해야 하는 레버 |
| 번들 마킹 | 없음 | `.ait` 헤더와 `window.AITLoading.buildVariant`에 `"perf"` 주입 |
| 자동 업데이트 프롬프트 | 표시됨 | 표시 안 됨 (수동 관리) |
| GitHub Release 표시 | Latest | prerelease |
| 권장 용도 | 서비스 배포 | 콜드 로드 최적화 파일럿 측정 |

`beta-perf` 브랜치 하나가 항상 최신 perf 베타를 가리키는 **이동 ref**입니다.

> **참고**: 이 문서가 설명하는 레버와 설정 필드는 **`beta-perf` 브랜치에만 존재합니다.** stable로 빌드한 번들에는 이 코드가 없고 `window.AITLoading.buildVariant`도 빈 문자열입니다. stable 체크아웃에서 아래 필드를 찾으면 나오지 않는 것이 정상입니다.

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

## 자동으로 적용되는 레버

설정 없이 빌드 시 적용됩니다.

| 레버 | 효과 |
|------|------|
| WebGL LTO 코드 최적화 | `DiskSizeLTO`로 코드 크기 축소 |
| IL2CPP OptimizeSize | 크기 우선 코드 생성 (Unity 6 이상) |
| WebAssembly 2023 타겟 | WASM2023 활성화 (Unity 6 이상) |
| Decompression Fallback 비활성화 | JS Brotli 디컴프레서를 번들에서 제외 |
| wasm Content-Type 재포장 훅 | 서버 Content-Type과 무관하게 스트리밍 컴파일 유지 |
| Mip Stripping | 미사용 밉맵 레벨 제거 |
| Strip Unused Mesh Components | 미사용 메시 컴포넌트 제거 |
| 데이터 캐싱과 CacheStorage 페이지 캐시 | 재방문 로드 단축. CacheStorage가 없는 WebView는 IndexedDB로 폴백 |
| Native asset prefetch handoff | 페이지 캐시와 연동해 네이티브 에셋 prefetch 위임 |
| Warm manifest emitter | 페이지 캐시와 연동해 자동 활성화 |
| Warm page emitter | 페이지 캐시와 warm manifest가 모두 켜졌을 때 자동 활성화 |

## 직접 확인해야 하는 레버

아래 레버도 기본값이 자동 활성이지만, **게임의 실제 에셋 경로나 임계값과 맞지 않으면 조건 불충족으로 아무 일도 하지 않습니다.** 로그에는 활성으로 찍히는데 산출물이 그대로라면 대개 이 경우입니다.

| 레버 | 마스터 스위치 | 주요 설정 필드와 기본값 | 설정 위치 |
|------|---------------|--------------------------|-----------|
| 폰트 CJK 서브셋 | `fontSubset = -1` | `fontSubsetTargetPaths = ""` (비우면 1MB 이상 폰트 자동 감지)<br>`fontSubsetUnicodeRanges = ""` (비우면 프로젝트 전체 스캔) | WebGL 최적화 설정 |
| 폰트 스트리밍 | `fontStreaming = -1` | `fontStreamingTargetPaths = ""` (manual 모드 전용, 비우면 no-op)<br>`fontStreamingMaxConcurrent = 2` | 고급 설정 |
| 텍스처 스트리밍 | `textureStreaming = -1` | `textureStreamingMinBytes = 524288`<br>`textureStreamingDirs = ""` (비우면 전체)<br>`textureStreamingExcludeDirs = ""`<br>`textureStreamingMaxConcurrent = 3` | 콘텐츠 최적화 — 텍스처 스트리밍 |
| 오디오 스트리밍 | `audioStreaming = -1` | `audioStreamingMinBytes = 262144`<br>`audioStreamingDirs = ""` (비우면 전체 AudioClip) | WebGL 최적화 설정 |
| 텍스처 Crunch 압축 | `textureCrunch = -1` | `textureCrunchQuality = 50`<br>`textureCrunchMaxSize = 0` (0은 무제한)<br>`textureCrunchAtlas = true`<br>`textureCrunchAtlasMaxSize = 0`<br>`textureCrunchDirs = ""` | WebGL 최적화 설정 |
| 텍스처 크기 클램프 | `textureSizeClamp = -1` | `textureClampMaxSize = 2048`<br>`textureClampMinBytes = 0`<br>`textureClampDirs = ""`<br>`textureClampExcludeDirs = ""` | WebGL 최적화 설정 |
| ASTC 블록 에스컬레이션 | `astcBlockEscalation = -1` | `astcBlockSize = 12`<br>`astcBlockMaxSize = 0`<br>`astcBlockAtlas = true`<br>`astcBlockDirs = ""`<br>`astcBlockExcludeDirs = ""` | WebGL 최적화 설정 |

마스터 스위치는 세 상태를 갖습니다 — `-1`은 자동, `0`은 비활성, `1`은 명시적 활성입니다. 위 레버는 **모두 자동이 곧 활성**이므로, 끄려면 `0`으로 명시해야 합니다.

> **주의**: 텍스처 크기 클램프의 기본 상한은 2048입니다. HiDPI 헤드룸을 감안한 값으로, 화면 일부를 차지하는 스프라이트·UI·아이콘에는 충분하고 full-bleed 배경만 최고 DPR 기기에서 약간 소프트해집니다. 1024로 낮추면 DPR 2 풀스크린에서도 뭉개질 수 있습니다. 의도적으로 고해상도를 유지해야 하는 에셋은 상한을 올리거나 `textureClampExcludeDirs`로 빼세요. 빌드가 끝나면 원본 임포트 설정으로 복원됩니다.

## 설정 방법

1. `AIT` > `Configuration`을 엽니다.
2. 위 표의 설정 위치에 해당하는 foldout을 펼칩니다.
   - **WebGL 최적화 설정**: 폰트 CJK 서브셋, 오디오 스트리밍, 텍스처 Crunch 압축, 텍스처 크기 클램프, ASTC 블록 에스컬레이션
   - **콘텐츠 최적화 — 텍스처 스트리밍**: 텍스처 스트리밍
   - **고급 설정**: 폰트 스트리밍
3. 각 레버의 팝업에서 자동·비활성·활성 중 하나를 고르고, 필요하면 경로와 임계값을 입력합니다.
4. `Assets/AppsInToss/Editor/AITConfig.asset`을 선택하면 같은 필드를 Inspector에서 직접 편집할 수도 있습니다.

어떤 레버가 실제로 적용됐는지는 빌드 로그가 최종 확인 수단입니다. 레버마다 활성 여부와 자동 여부가 함께 출력됩니다.

## 측정

1. 현행 stable 빌드와 perf 베타 빌드를 같은 기기, 같은 네트워크 조건에서 각각 배포합니다.
2. 직접 확인해야 하는 레버를 프로젝트의 실제 에셋 경로와 임계값에 맞게 설정한 뒤 빌드합니다.
3. 콜드 로드 시간(첫 프레임 표시까지)과 초기 다운로드 페이로드 크기를 두 빌드 사이에서 비교합니다.
4. 어느 번들이 어느 채널 것인지는 `window.AITLoading.buildVariant === "perf"`로 구분합니다.

첫 프레임 시각은 SDK가 자동 수집하는 `unity_first_interactive` 이벤트로도 얻을 수 있습니다. 자세한 내용은 [SDK 이벤트 로깅](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/metrics)을 참고하세요.

## stable 로 복귀

fragment를 불변 stable 태그로 되돌립니다.

```json
"im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/vX.Y.Z"
```

stable로 돌아가면 위 레버와 설정 필드가 함께 사라집니다. `AITConfig.asset`에 남아 있던 값은 읽히지 않으므로 따로 정리할 필요는 없습니다.

## 알아둘 점

- **자동 업데이트 없음**: `beta-perf`도 prerelease 채널로 판정되어 자동 업데이트 프롬프트가 뜨지 않습니다. 새 스냅샷은 안내를 받고 직접 갱신하세요.
- **재현 가능한 빌드**: `beta-perf`는 force-push로 갱신되는 이동 ref입니다. 측정 결과를 재현해야 한다면 스냅샷 태그로 핀하세요.
- **조건 불충족은 조용히 지나갑니다**: `fontSubsetTargetPaths`, `fontStreamingTargetPaths`, `textureStreamingDirs`, `audioStreamingDirs` 등을 비워두면 자동 감지가 돌지만, 임계값을 넘는 에셋이 없으면 아무것도 처리되지 않습니다. 에셋의 실제 경로와 크기를 확인한 뒤 필드를 채우세요.
- **손실 레버는 품질과 크기를 맞바꿉니다**: 텍스처 Crunch 압축, 텍스처 크기 클램프, ASTC 블록 에스컬레이션은 시각적 품질에 영향을 줄 수 있습니다. 품질 검수를 거친 뒤 프로덕션에 적용하세요.
- **서브타깃 제약**: ASTC 블록 에스컬레이션은 WebGL 빌드 subtarget이 ASTC일 때만 동작하고 DXT에서는 자동 비활성화됩니다. 텍스처 Crunch는 반대로 ASTC subtarget 빌드에서 경고 로그를 남기고 건너뜁니다.
- **파일럿 지원**: 적용 중 발견한 이슈나 측정 결과는 안내받은 채널로 공유해 주세요.

## 관련 문서

- [시작하기](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/getting-started) — 설치 ref 관리 공통 절차
- [베타 채널](BetaChannel.md) — web-framework 메이저 업그레이드 파일럿 채널
- [SDK 이벤트 로깅](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/metrics) — 첫 프레임 시각 계측
- [빌드 프로필](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-profiles) — stable에도 있는 압축·스트리핑 설정
