# SDK 런타임 메트릭

이 문서는 Apps in Toss Unity SDK 런타임에서 수집 가능한 모든 메트릭을 정리합니다.

## 목차

1. [Metric Explorer](#metric-explorer)
2. [로딩 메트릭 (Loading)](#1-로딩-메트릭-loading)
3. [웹 메트릭 (Web)](#2-웹-메트릭-web)
4. [Unity 메트릭 (Unity)](#3-unity-메트릭-unity)

---

## Metric Explorer

Debug Console에서 **Metrics** 버튼을 클릭하면 Metric Explorer가 열립니다. 3개의 탭(Loading, Web, Unity)에서 모든 메트릭의 raw data를 확인할 수 있습니다.

### 사용 방법

1. Debug Console 활성화: 빌드 설정에서 `enableDebugConsole: true` 설정
2. 게임 실행 후 왼쪽 하단의 🛠️ 버튼 클릭
3. Debug Console 헤더의 **Metrics** 버튼 클릭
4. Loading / Web / Unity 탭에서 메트릭 확인

### 기능

| 버튼 | 설명 |
|------|------|
| **Refresh All** | 현재 탭의 메트릭 새로고침 |
| **Copy JSON** | 현재 탭의 모든 메트릭을 JSON으로 클립보드 복사 |
| **Close** | Metric Explorer 닫기 |

---

## 1. 로딩 메트릭 (Loading)

**소스**: `window.AITLoadingLogger`

| 탭 | 분류 | 메트릭 | 설명 | 단위 | Android | iOS | Unity 6+ | Unity 2022.2+ | Unity 2021.3 |
|-----|------|--------|------|------|---------|-----|----------|---------------|--------------|
| Loading | Summary | `totalTime_ms` | 전체 로딩 시간 | ms | ✅ | ✅ | - | - | - |
| Loading | Summary | `totalFiles` | 다운로드한 파일 수 | count | ✅ | ✅ | - | - | - |
| Loading | Summary | `totalSize_MB` | 전체 다운로드 크기 | MB | ✅ | ✅ | - | - | - |
| Loading | Summary | `decompressionFallbackCount` | JS 압축해제 fallback 발생 파일 수 | count | ✅ | ✅ | - | - | - |
| Loading | Summary | `decompressionFallbackOccurred` | JS 압축해제 fallback 발생 여부 | bool | ✅ | ✅ | - | - | - |
| Loading | Events | `loading_start` | 로딩 시작 시점 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `loader_ready` | Unity Loader 스크립트 로드 완료 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `loader_error` | Unity Loader 스크립트 로드 실패 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `unity_init_start` | Unity 초기화 시작 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `unity_progress_25` | Unity 로딩 25% 도달 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `unity_progress_50` | Unity 로딩 50% 도달 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `unity_progress_75` | Unity 로딩 75% 도달 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `unity_progress_100` | Unity 로딩 100% 도달 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `unity_init_complete` | Unity 인스턴스 생성 완료 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `loading_complete` | 로딩 화면 숨김 시점 (SDK 측정) | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `loading_error` | 로딩 중 에러 발생 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `file_start_{filename}` | 파일 다운로드 시작 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `file_complete_{filename}` | 파일 다운로드 완료 | ms | ✅ | ✅ | - | - | - |
| Loading | Events | `file_error_{filename}` | 파일 다운로드 실패 | ms | ✅ | ✅ | - | - | - |
| Loading | File Download | `url` | 파일 URL | string | ✅ | ✅ | - | - | - |
| Loading | File Download | `size_bytes` | 파일 크기 | bytes | ✅ | ✅ | - | - | - |
| Loading | File Download | `size_MB` | 파일 크기 | MB | ✅ | ✅ | - | - | - |
| Loading | File Download | `duration_ms` | 다운로드 소요 시간 | ms | ✅ | ✅ | - | - | - |
| Loading | File Download | `startTime_ms` | 다운로드 시작 시점 | ms | ✅ | ✅ | - | - | - |
| Loading | File Download | `responseEnd_ms` | 다운로드 완료 시점 | ms | ✅ | ✅ | - | - | - |
| Loading | File Download | `avgSpeed_KBps` | 평균 다운로드 속도 | KB/s | ✅ | ✅ | - | - | - |
| Loading | File Download | `peakSpeed_KBps` | 최대 다운로드 속도 | KB/s | ✅ | ✅ | - | - | - |
| Loading | File Download | `minSpeed_KBps` | 최소 다운로드 속도 | KB/s | ✅ | ✅ | - | - | - |
| Loading | File Download | `speedHistory` | 1초 간격 속도 기록 배열 | KB/s[] | ✅ | ✅ | - | - | - |
| Loading | File Download | `compressionType` | 파일 압축 형식 (brotli, gzip, unityweb, none) | string | ✅ | ✅ | - | - | - |
| Loading | File Download | `contentEncoding` | 서버 Content-Encoding 헤더 값 | string | ✅ | ✅ | - | - | - |
| Loading | File Download | `decompressionFallback` | JS 압축해제 fallback 발생 여부 | bool | ✅ | ✅ | - | - | - |
| Web | JS Memory | `usedJSHeapSize_MB` | 사용 중인 JS 힙 크기 | MB | ⚠️ | ❌ | - | - | - |
| Web | JS Memory | `totalJSHeapSize_MB` | 전체 JS 힙 크기 | MB | ⚠️ | ❌ | - | - | - |
| Web | JS Memory | `jsHeapSizeLimit_MB` | JS 힙 크기 제한 | MB | ⚠️ | ❌ | - | - | - |
| Web | Navigator | `userAgent` | 브라우저 User-Agent | string | ✅ | ✅ | - | - | - |
| Web | Navigator | `platform` | 플랫폼 | string | ✅ | ✅ | - | - | - |
| Web | Navigator | `language` | 언어 | string | ✅ | ✅ | - | - | - |
| Web | Navigator | `cookieEnabled` | 쿠키 활성화 여부 | bool | ✅ | ✅ | - | - | - |
| Web | Navigator | `onLine` | 온라인 상태 | bool | ✅ | ✅ | - | - | - |
| Web | Navigator | `hardwareConcurrency` | CPU 논리 코어 수 | count | ✅ | ✅ | - | - | - |
| Web | Navigator | `deviceMemory` | 기기 메모리 | GB | ⚠️ | ❌ | - | - | - |
| Web | Navigator | `maxTouchPoints` | 최대 터치 포인트 | count | ✅ | ✅ | - | - | - |
| Web | Screen | `width` | 화면 너비 | px | ✅ | ✅ | - | - | - |
| Web | Screen | `height` | 화면 높이 | px | ✅ | ✅ | - | - | - |
| Web | Screen | `availWidth` | 사용 가능한 너비 | px | ✅ | ✅ | - | - | - |
| Web | Screen | `availHeight` | 사용 가능한 높이 | px | ✅ | ✅ | - | - | - |
| Web | Screen | `colorDepth` | 색 깊이 | bit | ✅ | ✅ | - | - | - |
| Web | Screen | `pixelDepth` | 픽셀 깊이 | bit | ✅ | ✅ | - | - | - |
| Web | Screen | `devicePixelRatio` | DPI 배율 | ratio | ✅ | ✅ | - | - | - |
| Web | Window | `innerWidth` | 뷰포트 너비 | px | ✅ | ✅ | - | - | - |
| Web | Window | `innerHeight` | 뷰포트 높이 | px | ✅ | ✅ | - | - | - |
| Web | Window | `outerWidth` | 창 외부 너비 | px | ✅ | ✅ | - | - | - |
| Web | Window | `outerHeight` | 창 외부 높이 | px | ✅ | ✅ | - | - | - |
| Web | Window | `scrollX` | 수평 스크롤 위치 | px | ✅ | ✅ | - | - | - |
| Web | Window | `scrollY` | 수직 스크롤 위치 | px | ✅ | ✅ | - | - | - |
| Web | Network | `effectiveType` | 유효 연결 유형 (4g, 3g 등) | string | ✅ | ❌ | - | - | - |
| Web | Network | `downlink` | 예상 다운링크 속도 | Mbps | ✅ | ❌ | - | - | - |
| Web | Network | `rtt` | 예상 왕복 지연 시간 | ms | ✅ | ❌ | - | - | - |
| Web | Network | `saveData` | 데이터 세이버 모드 활성화 여부 | bool | ✅ | ❌ | - | - | - |
| Web | Timing | `navigationStart` | 네비게이션 시작 타임스탬프 | timestamp | ✅ | ✅ | - | - | - |
| Web | Timing | `domContentLoaded_ms` | DOMContentLoaded까지 시간 | ms | ✅ | ✅ | - | - | - |
| Web | Timing | `domComplete_ms` | DOM 완료까지 시간 | ms | ✅ | ✅ | - | - | - |
| Web | Timing | `loadEvent_ms` | load 이벤트까지 시간 | ms | ✅ | ✅ | - | - | - |
| Web | Timing | `dnsLookup_ms` | DNS 조회 시간 | ms | ✅ | ✅ | - | - | - |
| Web | Timing | `tcpConnect_ms` | TCP 연결 시간 | ms | ✅ | ✅ | - | - | - |
| Web | Timing | `ttfb_ms` | Time to First Byte | ms | ✅ | ✅ | - | - | - |
| Web | Timing | `responseTime_ms` | 응답 수신 시간 | ms | ✅ | ✅ | - | - | - |
| Web | WebGL | `renderer` | GPU 렌더러 | string | ✅ | ✅ | - | - | - |
| Web | WebGL | `vendor` | GPU 벤더 | string | ✅ | ✅ | - | - | - |
| Web | WebGL | `version` | WebGL 버전 | string | ✅ | ✅ | - | - | - |
| Web | WebGL | `shadingLanguageVersion` | GLSL 버전 | string | ✅ | ✅ | - | - | - |
| Web | WebGL | `maxTextureSize` | 최대 텍스처 크기 | px | ✅ | ✅ | - | - | - |
| Web | WebGL | `maxViewportDims` | 최대 뷰포트 크기 | px | ✅ | ✅ | - | - | - |
| Web | WebGL | `maxRenderbufferSize` | 최대 렌더버퍼 크기 | px | ✅ | ✅ | - | - | - |
| Web | WebGL | `unmaskedVendor` | 실제 GPU 벤더 | string | ✅ | ✅ | - | - | - |
| Web | WebGL | `unmaskedRenderer` | 실제 GPU 렌더러 | string | ✅ | ✅ | - | - | - |
| Web | Visibility | `visibilityState` | 페이지 가시성 상태 | string | ✅ | ✅ | - | - | - |
| Web | Visibility | `hidden` | 페이지 숨김 여부 | bool | ✅ | ✅ | - | - | - |
| Unity | Instance | `unityVersion` | 감지된 Unity 버전 | string | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | Instance | `metricsAPI` | 사용 가능한 API | string | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | Instance | `moduleAvailable` | Unity Module 사용 가능 여부 | bool | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | Instance | `wasmHeapSize_MB` | WASM 힙 크기 | MB | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | Performance | `fps` | 현재 프레임 속도 | fps | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Performance | `movingAverageFps` | 10초 이동 평균 프레임 속도 | fps | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Performance | `numJankedFrames` | 프레임 스톨(끊김) 발생 횟수 | count | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Memory | `totalJSHeapSize_MB` | 전체 JavaScript 힙 크기 | MB | ✅ | ✅ | ✅ | ✅ | ❌ |
| Unity | Memory | `usedJSHeapSize_MB` | 사용 중인 JavaScript 힙 크기 | MB | ✅ | ✅ | ✅ | ✅ | ❌ |
| Unity | Memory | `totalWASMHeapSize_MB` | 전체 WebAssembly 힙 크기 | MB | ✅ | ✅ | ✅ | ✅ | ❌ |
| Unity | Memory | `usedWASMHeapSize_MB` | 사용 중인 WebAssembly 힙 크기 | MB | ✅ | ✅ | ✅ | ✅ | ❌ |
| Unity | Timing | `pageLoadTime_sec` | navigationStart부터 게임 루프 시작까지 (Unity 측정) | sec | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Timing | `pageLoadTimeToFrame1_sec` | navigationStart부터 첫 프레임 렌더링까지 (TTFF) | sec | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Timing | `codeDownloadTime_sec` | .wasm 파일 다운로드 시간 | sec | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Timing | `assetLoadTime_sec` | .data 파일 로드 시간 | sec | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Timing | `webAssemblyStartupTime_sec` | WASM 컴파일 및 인스턴스화 시간 | sec | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | Timing | `gameStartupTime_sec` | Unity 엔진 초기화부터 게임 루프 시작까지 | sec | ✅ | ✅ | ✅ | ❌ | ❌ |
| Unity | WASM Heap | `HEAPU8_length` | Uint8Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAP8_length` | Int8Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAPU16_length` | Uint16Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAP16_length` | Int16Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAPU32_length` | Uint32Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAP32_length` | Int32Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAPF32_length` | Float32Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |
| Unity | WASM Heap | `HEAPF64_length` | Float64Array 힙 길이 | bytes | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## Decompression Fallback

Unity WebGL 빌드 파일은 일반적으로 Brotli(`.br`) 또는 Gzip(`.gz`)으로 압축됩니다. 서버가 `Content-Encoding` 헤더를 올바르게 설정하면 브라우저가 네이티브로 압축을 해제하지만, 헤더가 없으면 Unity가 JavaScript로 압축을 해제합니다 (fallback).

### Fallback 발생 조건

| 조건 | 결과 |
|------|------|
| 서버가 `Content-Encoding: br` 또는 `gzip` 헤더 제공 | 브라우저 네이티브 압축 해제 (빠름) |
| 서버가 `Content-Encoding` 헤더 미제공 | JavaScript 압축 해제 fallback (느림) |

### 성능 영향

- **네이티브 압축 해제**: 브라우저가 최적화된 네이티브 코드로 처리
- **JS Fallback**: JavaScript로 압축 해제하여 로딩 시간 증가, 메모리 사용량 증가

### 관련 메트릭

| 메트릭 | 설명 |
|--------|------|
| `compressionType` | 파일 압축 형식 (brotli, gzip, unityweb, none) |
| `contentEncoding` | 서버가 보낸 Content-Encoding 헤더 값 (br, gzip, null) |
| `decompressionFallback` | 해당 파일에서 JS fallback 발생 여부 |
| `decompressionFallbackCount` | 전체 파일 중 fallback 발생 파일 수 |
| `decompressionFallbackOccurred` | fallback이 한 번이라도 발생했는지 여부 |

---

## 관련 소스 파일

| 파일 | 메트릭 카테고리 |
|------|----------------|
| `WebGLTemplates/AITTemplate/index.html` | AITLoadingLogger, Metric Explorer, Debug Console |
| `WebGLTemplates/AITTemplate/TemplateData/diagnostics.js` | Unity 진단 오버레이 (참조용) |

---

## 메트릭 사용 상태 요약

| 카테고리 | Metric Explorer 탭 | 상태 |
|----------|-------------------|------|
| 로딩 이벤트 메트릭 | Loading | ✅ 활성 |
| 파일 다운로드 메트릭 | Loading | ✅ 활성 |
| JavaScript Memory | Web | ✅ 활성 (Chrome) |
| Navigator / Screen / Window | Web | ✅ 활성 |
| Network Connection | Web | ⚠️ 일부 브라우저 |
| Performance Timing | Web | ✅ 활성 |
| WebGL | Web | ✅ 활성 |
| Unity Instance | Unity | ✅ 활성 |
| Unity Runtime Metrics | Unity | ⚠️ 진단 빌드만 |
| WASM Heap Arrays | Unity | ✅ 활성 |
