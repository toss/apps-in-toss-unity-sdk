# SDK 런타임 메트릭

이 문서는 Apps in Toss Unity SDK 런타임에서 수집 가능한 모든 메트릭을 정리합니다.

## 목차

1. [Metric Explorer](#metric-explorer)
2. [Unity 메트릭 (Metric Explorer)](#1-unity-메트릭-metric-explorer)
3. [로딩 메트릭 (AITLoading API)](#2-로딩-메트릭-aitloading-api)
4. [Decompression Fallback](#decompression-fallback)

---

## Metric Explorer

Debug Console에서 **Metrics** 버튼을 클릭하면 Metric Explorer가 열립니다. Unity 탭에서 런타임 메트릭의 raw data를 확인할 수 있습니다.

### 사용 방법

1. Debug Console 활성화: 빌드 설정에서 `enableDebugConsole: true` 설정
2. 게임 실행 후 왼쪽 하단의 🛠️ 버튼 클릭
3. Debug Console 헤더의 **Metrics** 버튼 클릭
4. Unity 탭에서 메트릭 확인

### 기능

| 버튼 | 설명 |
|------|------|
| **Refresh All** | 현재 탭의 메트릭 새로고침 |
| **Copy JSON** | 현재 탭의 모든 메트릭을 JSON으로 클립보드 복사 |
| **Close** | Metric Explorer 닫기 |

---

## 1. Unity 메트릭 (Metric Explorer)

**소스**: Metric Explorer Unity 탭 (`collectUnityMetrics()`)

Metric Explorer에 표시되며, `unity_runtime_metrics` 이벤트로 EventLog에도 자동 전송됩니다 (기본 10초 간격, `AIT_UNITY_METRICS_INTERVAL_MS`로 조정 가능).

### Instance

| 메트릭 | 설명 | 단위 | Unity 6+ | Unity 2022.2+ | Unity 2021.3 |
|--------|------|------|----------|---------------|--------------|
| `unityVersion` | 감지된 Unity 버전 | string | ✅ | ✅ | ✅ |
| `metricsAPI` | 사용 가능한 API | string | ✅ | ✅ | ✅ |
| `moduleAvailable` | Unity Module 사용 가능 여부 | bool | ✅ | ✅ | ✅ |
| `wasmHeapSize_MB` | WASM 힙 크기 | MB | ✅ | ✅ | ✅ |

### Performance (Unity 6+)

| 메트릭 | 설명 | 단위 | Unity 6+ | Unity 2022.2+ | Unity 2021.3 |
|--------|------|------|----------|---------------|--------------|
| `fps` | 현재 프레임 속도 | fps | ✅ | ❌ | ❌ |
| `movingAverageFps` | 10초 이동 평균 프레임 속도 | fps | ✅ | ❌ | ❌ |
| `numJankedFrames` | 프레임 스톨(끊김) 발생 횟수 | count | ✅ | ❌ | ❌ |

### Memory (Unity 2022.2+)

| 메트릭 | 설명 | 단위 | Unity 6+ | Unity 2022.2+ | Unity 2021.3 |
|--------|------|------|----------|---------------|--------------|
| `totalJSHeapSize_MB` | 전체 JavaScript 힙 크기 | MB | ✅ | ✅ | ❌ |
| `usedJSHeapSize_MB` | 사용 중인 JavaScript 힙 크기 | MB | ✅ | ✅ | ❌ |
| `totalWASMHeapSize_MB` | 전체 WebAssembly 힙 크기 | MB | ✅ | ✅ | ❌ |
| `usedWASMHeapSize_MB` | 사용 중인 WebAssembly 힙 크기 | MB | ✅ | ✅ | ❌ |

### Timing (Unity 6+)

| 메트릭 | 설명 | 단위 | Unity 6+ | Unity 2022.2+ | Unity 2021.3 |
|--------|------|------|----------|---------------|--------------|
| `pageLoadTime_sec` | navigationStart부터 게임 루프 시작까지 (Unity 측정) | sec | ✅ | ❌ | ❌ |
| `pageLoadTimeToFrame1_sec` | navigationStart부터 첫 프레임 렌더링까지 (TTFF) | sec | ✅ | ❌ | ❌ |
| `codeDownloadTime_sec` | .wasm 파일 다운로드 시간 | sec | ✅ | ❌ | ❌ |
| `assetLoadTime_sec` | .data 파일 로드 시간 | sec | ✅ | ❌ | ❌ |
| `webAssemblyStartupTime_sec` | WASM 컴파일 및 인스턴스화 시간 | sec | ✅ | ❌ | ❌ |
| `gameStartupTime_sec` | Unity 엔진 초기화부터 게임 루프 시작까지 | sec | ✅ | ❌ | ❌ |

### WASM Heap

| 메트릭 | 설명 | 단위 | Unity 6+ | Unity 2022.2+ | Unity 2021.3 |
|--------|------|------|----------|---------------|--------------|
| `HEAPU8_length` | Uint8Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAP8_length` | Int8Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAPU16_length` | Uint16Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAP16_length` | Int16Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAPU32_length` | Uint32Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAP32_length` | Int32Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAPF32_length` | Float32Array 힙 길이 | bytes | ✅ | ✅ | ✅ |
| `HEAPF64_length` | Float64Array 힙 길이 | bytes | ✅ | ✅ | ✅ |

> **참고**: WASM Heap 메트릭은 Metric Explorer에 표시되지만, EventLog 전송에는 포함되지 않습니다.

---

## 2. 로딩 메트릭 (AITLoading API)

**소스**: `window.AITLoadingLogger`, `window.AITLoading`

로딩 메트릭은 Metric Explorer에 표시되지 않으며, JavaScript API를 통해 프로그래밍 방식으로만 접근할 수 있습니다. 로딩 화면 커스터마이징에 활용할 수 있습니다. 자세한 내용은 [로딩 화면 커스터마이징 가이드](LoadingScreenCustomization.md)를 참조하세요.

### 이벤트 (`AITLoadingLogger.events`)

| 이벤트 | 설명 | 단위 |
|--------|------|------|
| `loading_start` | 로딩 시작 시점 | ms |
| `loader_ready` | Unity Loader 스크립트 로드 완료 | ms |
| `loader_error` | Unity Loader 스크립트 로드 실패 | ms |
| `unity_init_start` | Unity 초기화 시작 | ms |
| `unity_progress_25` | Unity 로딩 25% 도달 | ms |
| `unity_progress_50` | Unity 로딩 50% 도달 | ms |
| `unity_progress_75` | Unity 로딩 75% 도달 | ms |
| `unity_progress_100` | Unity 로딩 100% 도달 | ms |
| `unity_init_complete` | Unity 인스턴스 생성 완료 | ms |
| `loading_complete` | 로딩 화면 숨김 시점 (SDK 측정) | ms |
| `loading_error` | 로딩 중 에러 발생 | ms |
| `file_start_{filename}` | 파일 다운로드 시작 | ms |
| `file_complete_{filename}` | 파일 다운로드 완료 | ms |
| `file_error_{filename}` | 파일 다운로드 실패 | ms |
| `resource_timing_{filename}` | 리소스 타이밍 (initiator, duration) | ms |

### 총 로딩 시간 (`AITLoading.getTotalTime()`)

`loading_start`부터 `loading_complete`까지의 전체 로딩 시간(ms)을 반환합니다.

### 파일 다운로드 통계 (`AITLoading.getFileStats()`)

| 필드 | 설명 | 단위 |
|------|------|------|
| `name` | 파일명 | string |
| `url` | 파일 URL | string |
| `size` | 파일 크기 | bytes |
| `duration` | 다운로드 소요 시간 | ms |
| `startTime` | 다운로드 시작 시점 (로딩 시작 기준 상대) | ms |
| `responseEnd` | 다운로드 완료 시점 (로딩 시작 기준 상대) | ms |
| `avgSpeed` | 평균 다운로드 속도 | KB/s |
| `peakSpeed` | 최대 다운로드 속도 | KB/s |
| `minSpeed` | 최소 다운로드 속도 | KB/s |
| `speedHistory` | 1초 간격 속도 기록 배열 | KB/s[] |
| `compressionType` | 파일 압축 형식 (brotli, gzip, unityweb, none) | string |
| `contentEncoding` | 서버 Content-Encoding 헤더 값 | string |
| `decompressionFallback` | JS 압축해제 fallback 발생 여부 | bool |
| `preloaded` | HTML5 Preload로 로드되었는지 여부 | bool |

### Preload 통계

`AITLoadingLogger`는 HTML5 Preload 적용 여부도 추적합니다:

| 필드 | 설명 |
|------|------|
| `preload_enabled` | HTML5 Preload가 적용되었는지 여부 |
| `preload_file_count` | Preload로 로드된 파일 수 |
| `preload_cache_hits` | Preload 캐시 히트 파일 수 |

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

---

## 관련 소스 파일

| 파일 | 설명 |
|------|------|
| `WebGLTemplates/AITTemplate/index.html` | AITLoadingLogger, Metric Explorer, Unity 메트릭 수집/전송 |

---

## 메트릭 사용 상태 요약

| 카테고리 | 수집 | Metric Explorer | EventLog 전송 | 접근 방법 |
|----------|------|-----------------|---------------|-----------|
| Unity Instance | ✅ | ✅ | ✅ | Metric Explorer |
| Unity Performance (6+) | ✅ | ✅ | ✅ | Metric Explorer |
| Unity Memory (2022.2+) | ✅ | ✅ | ✅ | Metric Explorer |
| Unity Timing (6+) | ✅ | ✅ | ✅ | Metric Explorer |
| WASM Heap | ✅ | ✅ | ❌ | Metric Explorer |
| 로딩 이벤트 | ✅ | ❌ | ❌ | `AITLoadingLogger.events` |
| 파일 다운로드 | ✅ | ❌ | ❌ | `AITLoading.getFileStats()` |
