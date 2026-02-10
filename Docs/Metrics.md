# SDK 메트릭 및 이벤트 로깅

이 문서는 Apps in Toss Unity SDK에서 수집하는 모든 메트릭과 이벤트를 정리합니다.

## 목차

1. [Debug Console](#1-debug-console)
2. [이벤트 로깅](#2-이벤트-로깅)

---

## 1. Debug Console

**소스**: JS (`index.html`) — 화면 표시 전용, EventLog 전송 없음

빌드 설정에서 `enableDebugConsole: true` 설정 시 활성화됩니다. 게임 실행 후 왼쪽 하단 🛠️ 버튼으로 열 수 있습니다.

### 표시 메트릭

0.5초마다 갱신되며, Debug Console 패널 상단에 표시됩니다.

| 메트릭 | 설명 | 단위 |
|--------|------|------|
| FPS | 현재 프레임 속도 | fps |
| Frame | 평균 프레임 시간 | ms |
| JS Heap | 사용 중 / 전체 JavaScript 힙 크기 | MB |
| Resolution | 윈도우 해상도 + 픽셀 비율 | px |
| Device | 디바이스 메모리 + CPU 코어 수 | GB, cores |
| GPU | WebGL 렌더러 정보 | string |

### 기능

| 버튼 | 설명 |
|------|------|
| **Copy Logs** | 콘솔 로그를 클립보드에 복사 |
| **Clear** | 콘솔 로그 초기화 |
| **Close** | Debug Console 닫기 |

---

## 2. 이벤트 로깅

**소스**: C# (`Runtime/Helpers/AIT.EventLogger.cs`, `AITEventLogger`) — 이벤트 발생 시 `AIT.EventLog()`로 전송

`[RuntimeInitializeOnLoadMethod]`로 자동 초기화되어 사용자 코드 작성이 불필요합니다.

모든 이벤트의 `log_type`은 `"unity_runtime"`입니다.

### 이벤트 카테고리 요약

| # | log_name | 트리거 | Rate Limit |
|---|----------|--------|------------|
| 1 | `unity_scene_transition` | SceneManager.sceneLoaded/sceneUnloaded | 없음 |
| 2 | `unity_low_memory` | Application.lowMemory | 30초당 1회 |
| 3 | `unity_error` | Application.logMessageReceived (Error/Exception/Assert) | 60초당 10회 + 중복 제거 |
| 4 | `unity_lifecycle` | Application.focusChanged, Application.quitting | focus: 5초당 1회 |
| 5 | `unity_frame_stall` | Update() delta > 500ms | 60초당 5회 |
| 6 | `unity_screen_change` | Screen.width/height/orientation 변경 감지 | 2초당 1회 |
| 7 | `unity_gc_collection` | GC.CollectionCount() 변화 감지 | 60초당 5회 |
| 8 | `unity_timescale_change` | Time.timeScale 변경 감지 | 5초당 1회 |

### 이벤트별 파라미터

#### 1. Scene 전환 (`unity_scene_transition`)

```json
{
    "event_type": "scene_loaded",
    "scene_name": "GameScene",
    "scene_build_index": 2,
    "load_mode": "Single",
    "previous_scene": "MainMenu",
    "total_loaded_scenes": 3,
    "time_since_start_sec": 12.5
}
```

| 파라미터 | 설명 | event_type |
|----------|------|------------|
| `event_type` | `scene_loaded` 또는 `scene_unloaded` | 전체 |
| `scene_name` | Scene 이름 | 전체 |
| `scene_build_index` | Build Settings 인덱스 | 전체 |
| `load_mode` | `Single` 또는 `Additive` | scene_loaded만 |
| `previous_scene` | 이전 활성 Scene 이름 | scene_loaded만 |
| `total_loaded_scenes` | 현재 로드된 Scene 수 | 전체 |
| `time_since_start_sec` | 앱 시작 이후 경과 시간 | 전체 |

#### 2. Low Memory (`unity_low_memory`)

```json
{
    "event_type": "low_memory",
    "time_since_start_sec": 120.5
}
```

#### 3. 에러/예외 (`unity_error`)

```json
{
    "event_type": "exception",
    "message": "NullReferenceException: ...",
    "stack_trace": "at GameManager.Update() ...",
    "log_type": "Exception",
    "time_since_start_sec": 45.2
}
```

| 파라미터 | 설명 |
|----------|------|
| `event_type` | `error`, `exception`, `assert` |
| `message` | 에러 메시지 (최대 500자) |
| `stack_trace` | 스택 트레이스 (최대 200자) |
| `log_type` | Unity LogType (`Error`, `Exception`, `Assert`) |

#### 4. 앱 라이프사이클 (`unity_lifecycle`)

```json
{ "event_type": "focus_changed", "has_focus": true, "time_since_start_sec": 120.5 }
{ "event_type": "quitting", "session_duration_sec": 300.5, "total_scenes_loaded": 5 }
```

#### 5. 프레임 스톨 (`unity_frame_stall`)

```json
{
    "event_type": "frame_stall",
    "frame_duration_ms": 750,
    "threshold_ms": 500,
    "time_since_start_sec": 45.2
}
```

#### 6. 화면 변경 (`unity_screen_change`)

```json
{ "event_type": "screen_resize", "width": 1920, "height": 1080, "previous_width": 1280, "previous_height": 720, "time_since_start_sec": 30.0 }
{ "event_type": "orientation_change", "width": 1080, "height": 1920, "orientation": "Portrait", "previous_orientation": "LandscapeLeft", "time_since_start_sec": 30.0 }
```

#### 7. GC 수집 (`unity_gc_collection`)

```json
{
    "event_type": "gc_collection",
    "generation": 1,
    "gen0_total": 45,
    "gen1_total": 12,
    "gen2_total": 3,
    "time_since_start_sec": 60.0
}
```

#### 8. TimeScale 변경 (`unity_timescale_change`)

```json
{
    "event_type": "timescale_changed",
    "time_scale": 0.0,
    "previous_time_scale": 1.0,
    "time_since_start_sec": 15.0
}
```

### 안전장치

| 항목 | 설명 |
|------|------|
| try-catch | 모든 핸들러를 감싸서 로깅이 게임을 크래시하지 않음 |
| 재진입 방지 | `_isSending` guard로 `logMessageReceived` → `EventLog` 무한루프 방지 |
| fire-and-forget | `_ = AIT.EventLog(...)` 패턴으로 Task/Awaitable 양쪽 호환 |
| Rate Limiting | 카테고리별 고정 rate limit으로 과도한 전송 방지 |

---

## 관련 소스 파일

| 파일 | 설명 |
|------|------|
| `WebGLTemplates/AITTemplate/index.html` | Debug Console (화면 표시 전용) |
| `Runtime/Helpers/AIT.EventLogger.cs` | 이벤트 로거 (C#) |

---

## 요약

| 카테고리 | 수집 방식 | 화면 표시 | EventLog 전송 |
|----------|-----------|-----------|---------------|
| Debug Console 메트릭 | JS, 0.5초 폴링 | ✅ | ❌ |
| 이벤트 로깅 (8개 카테고리) | C#, 발생 시 | ❌ | ✅ |
