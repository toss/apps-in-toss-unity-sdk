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

### 1.1 Loading Summary

| 메트릭 | 설명 | 단위 |
|--------|------|------|
| `totalTime_ms` | 전체 로딩 시간 | ms |
| `totalFiles` | 다운로드한 파일 수 | count |
| `totalSize_MB` | 전체 다운로드 크기 | MB |

### 1.2 Loading Events (Timing)

| 메트릭 | 설명 | 수집 방법 |
|--------|------|-----------|
| `loading_start` | 로딩 시작 시점 | `performance.now()` |
| `loader_ready` | Unity Loader 스크립트 로드 완료 | `<script onload>` |
| `loader_error` | Unity Loader 스크립트 로드 실패 | `<script onerror>` |
| `unity_init_start` | Unity 초기화 시작 | `createUnityInstance()` 호출 전 |
| `unity_progress_25` | Unity 로딩 25% 도달 | 진행률 콜백 |
| `unity_progress_50` | Unity 로딩 50% 도달 | 진행률 콜백 |
| `unity_progress_75` | Unity 로딩 75% 도달 | 진행률 콜백 |
| `unity_progress_100` | Unity 로딩 100% 도달 | 진행률 콜백 |
| `unity_init_complete` | Unity 인스턴스 생성 완료 | `createUnityInstance()` 성공 |
| `loading_complete` | 전체 로딩 완료 | `hideLoadingScreen()` 호출 |
| `loading_error` | 로딩 중 에러 발생 | catch 블록 |
| `file_start_{filename}` | 파일 다운로드 시작 | fetch 래핑 |
| `file_complete_{filename}` | 파일 다운로드 완료 | ReadableStream 완료 |
| `file_error_{filename}` | 파일 다운로드 실패 | fetch catch |

각 이벤트는 다음 데이터를 포함합니다:
```javascript
{
  elapsed_ms: number,  // 로딩 시작부터 경과 시간
  data: object|null    // 추가 데이터 (에러 메시지 등)
}
```

### 1.3 File Download Metrics

Unity WebGL 빌드 파일(`.loader.js`, `.framework.js`, `.wasm`, `.data`)에 대한 상세 다운로드 메트릭입니다.

| 필드 | 설명 | 단위 |
|------|------|------|
| `url` | 파일 URL | string |
| `size_bytes` | 파일 크기 | bytes |
| `size_MB` | 파일 크기 | MB |
| `duration_ms` | 다운로드 소요 시간 | ms |
| `startTime_ms` | 다운로드 시작 시점 (상대 시간) | ms |
| `responseEnd_ms` | 다운로드 완료 시점 (상대 시간) | ms |
| `avgSpeed_KBps` | 평균 다운로드 속도 | KB/s |
| `peakSpeed_KBps` | 최대 다운로드 속도 | KB/s |
| `minSpeed_KBps` | 최소 다운로드 속도 | KB/s |
| `speedHistory` | 1초 간격 속도 기록 배열 | KB/s[] |

### 1.4 콜백 API

`window.AITLoading` 객체를 통해 외부에서 로딩 메트릭에 접근할 수 있습니다.

| API | 설명 |
|-----|------|
| `onReady(callback)` | 앱 정보 초기화 완료 시 콜백 |
| `onProgress(callback)` | 진행률 업데이트 콜백 (0.0~1.0) |
| `onComplete(callback)` | 로딩 완료 콜백 |
| `onError(callback)` | 로딩 에러 콜백 |
| `onFileProgress(callback)` | 파일별 진행 콜백 |
| `getFileStats()` | 파일별 다운로드 통계 반환 |
| `getTotalTime()` | 총 로딩 시간 반환 (ms) |

---

## 2. 웹 메트릭 (Web)

브라우저 Web API를 통해 수집되는 메트릭입니다.

### 2.1 JavaScript Memory (Chrome)

`performance.memory` API를 사용합니다. Chromium 기반 브라우저에서만 지원됩니다.

| 메트릭 | 설명 | 단위 |
|--------|------|------|
| `usedJSHeapSize_MB` | 사용 중인 JS 힙 크기 | MB |
| `totalJSHeapSize_MB` | 전체 JS 힙 크기 | MB |
| `jsHeapSizeLimit_MB` | JS 힙 크기 제한 | MB |

### 2.2 Navigator

| 메트릭 | 설명 | API |
|--------|------|-----|
| `userAgent` | 브라우저 User-Agent | `navigator.userAgent` |
| `platform` | 플랫폼 | `navigator.platform` |
| `language` | 언어 | `navigator.language` |
| `cookieEnabled` | 쿠키 활성화 여부 | `navigator.cookieEnabled` |
| `onLine` | 온라인 상태 | `navigator.onLine` |
| `hardwareConcurrency` | CPU 논리 코어 수 | `navigator.hardwareConcurrency` |
| `deviceMemory` | 기기 메모리 (GB) | `navigator.deviceMemory` |
| `maxTouchPoints` | 최대 터치 포인트 | `navigator.maxTouchPoints` |

### 2.3 Screen

| 메트릭 | 설명 | API |
|--------|------|-----|
| `width` | 화면 너비 | `screen.width` |
| `height` | 화면 높이 | `screen.height` |
| `availWidth` | 사용 가능한 너비 | `screen.availWidth` |
| `availHeight` | 사용 가능한 높이 | `screen.availHeight` |
| `colorDepth` | 색 깊이 | `screen.colorDepth` |
| `pixelDepth` | 픽셀 깊이 | `screen.pixelDepth` |
| `devicePixelRatio` | DPI 배율 | `window.devicePixelRatio` |

### 2.4 Window

| 메트릭 | 설명 | API |
|--------|------|-----|
| `innerWidth` | 뷰포트 너비 | `window.innerWidth` |
| `innerHeight` | 뷰포트 높이 | `window.innerHeight` |
| `outerWidth` | 창 외부 너비 | `window.outerWidth` |
| `outerHeight` | 창 외부 높이 | `window.outerHeight` |
| `scrollX` | 수평 스크롤 위치 | `window.scrollX` |
| `scrollY` | 수직 스크롤 위치 | `window.scrollY` |

### 2.5 Network Connection

`navigator.connection` API를 사용합니다. 일부 브라우저에서만 지원됩니다.

| 메트릭 | 설명 | API |
|--------|------|-----|
| `effectiveType` | 유효 연결 유형 (4g, 3g, 2g, slow-2g) | `navigator.connection.effectiveType` |
| `downlink` | 예상 다운링크 속도 (Mbps) | `navigator.connection.downlink` |
| `rtt` | 예상 왕복 지연 시간 (ms) | `navigator.connection.rtt` |
| `saveData` | 데이터 세이버 모드 활성화 여부 | `navigator.connection.saveData` |

### 2.6 Performance Timing

`performance.timing` API를 사용합니다.

| 메트릭 | 설명 | 계산 |
|--------|------|------|
| `navigationStart` | 네비게이션 시작 타임스탬프 | `timing.navigationStart` |
| `domContentLoaded_ms` | DOMContentLoaded까지 시간 | `domContentLoadedEventEnd - navigationStart` |
| `domComplete_ms` | DOM 완료까지 시간 | `domComplete - navigationStart` |
| `loadEvent_ms` | load 이벤트까지 시간 | `loadEventEnd - navigationStart` |
| `dnsLookup_ms` | DNS 조회 시간 | `domainLookupEnd - domainLookupStart` |
| `tcpConnect_ms` | TCP 연결 시간 | `connectEnd - connectStart` |
| `ttfb_ms` | Time to First Byte | `responseStart - navigationStart` |
| `responseTime_ms` | 응답 수신 시간 | `responseEnd - responseStart` |

### 2.7 WebGL

WebGL 컨텍스트에서 수집되는 GPU 관련 메트릭입니다.

| 메트릭 | 설명 | API |
|--------|------|-----|
| `renderer` | GPU 렌더러 | `gl.getParameter(gl.RENDERER)` |
| `vendor` | GPU 벤더 | `gl.getParameter(gl.VENDOR)` |
| `version` | WebGL 버전 | `gl.getParameter(gl.VERSION)` |
| `shadingLanguageVersion` | GLSL 버전 | `gl.getParameter(gl.SHADING_LANGUAGE_VERSION)` |
| `maxTextureSize` | 최대 텍스처 크기 | `gl.getParameter(gl.MAX_TEXTURE_SIZE)` |
| `maxViewportDims` | 최대 뷰포트 크기 | `gl.getParameter(gl.MAX_VIEWPORT_DIMS)` |
| `maxRenderbufferSize` | 최대 렌더버퍼 크기 | `gl.getParameter(gl.MAX_RENDERBUFFER_SIZE)` |
| `unmaskedVendor` | 실제 GPU 벤더 | `WEBGL_debug_renderer_info` 확장 |
| `unmaskedRenderer` | 실제 GPU 렌더러 | `WEBGL_debug_renderer_info` 확장 |

### 2.8 Visibility

| 메트릭 | 설명 | API |
|--------|------|-----|
| `visibilityState` | 페이지 가시성 상태 (visible, hidden) | `document.visibilityState` |
| `hidden` | 페이지 숨김 여부 | `document.hidden` |

---

## 3. Unity 메트릭 (Unity)

Unity WebGL 런타임에서 제공하는 메트릭입니다.

### 3.1 Unity Instance

| 메트릭 | 설명 |
|--------|------|
| `instanceExists` | Unity 인스턴스 존재 여부 |
| `moduleName` | Unity Module 사용 가능 여부 |
| `wasmHeapSize_MB` | WASM 힙 크기 (MB) |

### 3.2 Unity Runtime Metrics

Unity의 `getMetricsInfo()` API를 통해 수집됩니다. Unity WebGL 빌드에서 진단 기능이 활성화된 경우에만 사용 가능합니다.

#### 메모리 메트릭

| 메트릭 | 설명 | 단위 |
|--------|------|------|
| `totalJSHeapSize_MB` | 전체 JavaScript 힙 크기 | MB |
| `usedJSHeapSize_MB` | 사용 중인 JavaScript 힙 크기 | MB |
| `totalWASMHeapSize_MB` | 전체 WebAssembly 힙 크기 | MB |
| `usedWASMHeapSize_MB` | 사용 중인 WebAssembly 힙 크기 | MB |

#### 성능 메트릭

| 메트릭 | 설명 | 단위 |
|--------|------|------|
| `fps` | 현재 프레임 속도 | fps |
| `movingAverageFps` | 10초 이동 평균 프레임 속도 | fps |
| `numJankedFrames` | 프레임 스톨(끊김) 발생 횟수 | count |

#### 로딩 타이밍 메트릭

| 메트릭 | 설명 | 단위 |
|--------|------|------|
| `pageLoadTime_sec` | 전체 페이지 로드 시간 | sec |
| `pageLoadTimeToFrame1_sec` | 첫 프레임 렌더링까지 시간 | sec |
| `codeDownloadTime_sec` | .wasm 파일 다운로드 시간 | sec |
| `assetLoadTime_sec` | .data 파일 로드 시간 | sec |
| `webAssemblyStartupTime_sec` | WebAssembly 초기화 시간 | sec |
| `gameStartupTime_sec` | 게임 시작 시간 | sec |

### 3.3 WASM Heap Arrays

Unity Module의 WASM 힙 배열 정보입니다.

| 메트릭 | 설명 |
|--------|------|
| `HEAPU8_length` | Uint8Array 힙 길이 |
| `HEAP8_length` | Int8Array 힙 길이 |
| `HEAPU16_length` | Uint16Array 힙 길이 |
| `HEAP16_length` | Int16Array 힙 길이 |
| `HEAPU32_length` | Uint32Array 힙 길이 |
| `HEAP32_length` | Int32Array 힙 길이 |
| `HEAPF32_length` | Float32Array 힙 길이 |
| `HEAPF64_length` | Float64Array 힙 길이 |

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
