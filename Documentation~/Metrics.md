# SDK 이벤트 로깅

SDK가 사용자 코드 없이 자동으로 수집해 플랫폼에 보내는 런타임 이벤트를 정리합니다. 내가 무엇을 계측해야 하는지가 아니라, **이미 무엇이 계측되고 있는지**를 확인하는 문서입니다.

## 어떻게 켜지나

`Runtime/Helpers/AIT.PerformanceLogger.cs`의 `AITPerformanceLogger`가 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`로 자동 초기화됩니다. 설치하거나 호출할 것이 없습니다.

전송은 **WebGL 빌드에서만** 일어납니다. Unity Editor와 그 외 플랫폼에서는 `SendLog`가 진입 즉시 반환하므로 이벤트가 만들어지지도, 나가지도 않습니다. 브릿지가 WebGL 빌드에만 존재하기 때문입니다.

모든 이벤트의 `log_type`은 `unity_runtime`이고, 이벤트 종류는 `log_name`으로 구분합니다.

## 이벤트 카테고리

| log_name | 트리거 | Rate Limit |
|----------|--------|------------|
| `unity_scene_transition` | `SceneManager.sceneLoaded` / `sceneUnloaded` | 없음 |
| `unity_first_interactive` | 원래 첫 씬 로드 완료 | 세션당 1회 |
| `unity_low_memory` | `Application.lowMemory` | 30초당 1회 |
| `unity_error` | `Application.logMessageReceived` (Error/Exception/Assert) | 60초당 10회 + 중복 제거 |
| `unity_lifecycle` | `AITVisibilityHelper.OnVisibilityChanged`, `Application.quitting` | focus_changed: 5초당 1회 |
| `unity_frame_stall` | `Time.unscaledDeltaTime` > 500ms | 60초당 5회 |
| `unity_screen_change` | `Screen.width`/`height`/`orientation` 변경 감지 | 2초당 1회 |
| `unity_gc_collection` | `GC.CollectionCount(0)` 변화 감지 | 60초당 5회 |
| `unity_timescale_change` | `Time.timeScale` 변경 감지 | 5초당 1회 |

폴링으로 감지하는 넷(`frame_stall`, `screen_change`, `gc_collection`, `timescale_change`)은 전용 `AITPerformanceLoggerMonitor` GameObject의 `Update`에서 매 프레임 확인합니다. 이 오브젝트는 `HideAndDontSave` + `DontDestroyOnLoad`라 Hierarchy에 보이지 않고 씬 전환에도 살아남습니다.

> **참고**: 포커스 이벤트는 `Application.focusChanged`가 아니라 SDK 자체의 `AITVisibilityHelper`에서 옵니다. WebGL에서는 브라우저 탭 가시성이 실제 신호이기 때문입니다.

## 이벤트별 파라미터

모든 이벤트는 아래 공통 파라미터를 포함합니다.

| 파라미터 | 설명 |
|----------|------|
| `event_type` | 같은 `log_name` 안에서 세부 종류를 구분 |
| `time_since_start_sec` | 앱 시작 이후 경과 시간 (소수점 1자리) |

`unity_first_interactive`만 예외로 `time_since_start_sec` 대신 `time_since_start_ms`를 씁니다.

### unity_scene_transition

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
| `previous_scene` | 직전에 로드된 Scene 이름 | scene_loaded만 |
| `total_loaded_scenes` | 현재 로드된 Scene 수 (`SceneManager.sceneCount`) | 전체 |

### unity_first_interactive

원래 첫 씬의 로드가 끝난 시점, 즉 게임이 실제로 조작 가능해진 순간을 재는 이벤트입니다. 세션당 한 번만 나갑니다.

```json
{
    "event_type": "first_interactive",
    "scene_name": "MainMenu",
    "scene_build_index": 0,
    "time_since_start_ms": 4820
}
```

발화 판정에는 두 가지 규칙이 있습니다.

- **`AITProxyBoot`로 시작하는 씬은 건너뜁니다.** SDK가 주입한 프록시 부팅 씬은 게임의 원래 첫 씬이 아니기 때문입니다.
- **활성 여부와 무관하게 최초 대상 씬에서 "최초"가 확정됩니다.** 로깅이 꺼져 있어도 그 씬에서 플래그가 고정되므로, 나중에 로드되는 씬이 뒤늦게 first로 보고되지 않습니다.

활성 여부는 빌드 시 템플릿에 새겨진 값을 jslib으로 한 번 조회한 뒤 캐시합니다. 조회가 실패하면 **활성으로 간주**합니다(fail-open).

> **참고**: 부팅 첫 씬 로드는 first-paint 직전에 일어나므로, 별도 최적화가 없는 빌드에서는 이 값이 first-paint 시각과 거의 같게 나옵니다. 두 지표의 간격이 벌어진다면 첫 씬이 무거워졌다는 신호입니다.

### unity_low_memory

```json
{
    "event_type": "low_memory",
    "time_since_start_sec": 120.5
}
```

### unity_error

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
| `message` | 에러 메시지 (500자에서 잘림) |
| `stack_trace` | 스택 트레이스 (200자에서 잘림) |
| `log_type` | Unity `LogType` (`Error`, `Exception`, `Assert`) |

중복 제거는 **60초 창 안에서 메시지 해시 기준**입니다. 같은 메시지가 창 안에서 반복되면 첫 건만 전송되고, 창이 지나면 해시 집합이 비워져 다시 한 번 보고됩니다. 스택 트레이스가 달라도 메시지가 같으면 같은 것으로 봅니다.

### unity_lifecycle

```json
{ "event_type": "focus_changed", "has_focus": true, "time_since_start_sec": 120.5 }
{ "event_type": "quitting", "session_duration_sec": 300.5, "total_scenes_loaded": 5 }
```

`total_scenes_loaded`는 세션 동안 로드된 **누적** 씬 수로, `unity_scene_transition`의 `total_loaded_scenes`(현재 동시 로드 수)와 다른 값입니다.

### unity_frame_stall

```json
{
    "event_type": "frame_stall",
    "frame_duration_ms": 750,
    "threshold_ms": 500,
    "time_since_start_sec": 45.2
}
```

기준은 `Time.deltaTime`이 아니라 `Time.unscaledDeltaTime`입니다. `Time.timeScale`을 0으로 두는 일시정지 구간이 스톨로 잡히지 않습니다.

### unity_screen_change

```json
{ "event_type": "screen_resize", "width": 1920, "height": 1080, "previous_width": 1280, "previous_height": 720, "time_since_start_sec": 30.0 }
{ "event_type": "orientation_change", "width": 1080, "height": 1920, "orientation": "Portrait", "previous_orientation": "LandscapeLeft", "time_since_start_sec": 30.0 }
```

크기와 방향이 함께 바뀌면 `orientation_change` 하나만 나갑니다. 회전은 대개 크기 변화를 동반하므로 두 이벤트가 겹쳐 나가지 않게 한 것입니다.

### unity_gc_collection

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

감지는 **gen0 카운터 변화만** 봅니다. `generation` 값은 gen1/gen2 누적 카운트로 추정한 것이라, 이번 수집이 실제로 어느 세대였는지를 정확히 가리키지는 않습니다. 세 `gen*_total`이 프로세스 시작 이후 누적값이라는 점에서 이 값들이 더 신뢰할 만합니다.

### unity_timescale_change

```json
{
    "event_type": "timescale_changed",
    "time_scale": 0.0,
    "previous_time_scale": 1.0,
    "time_since_start_sec": 15.0
}
```

## 디버그 콘솔에서 확인하기

디버그 콘솔을 켜면 화면 좌측 하단 버튼으로 콘솔을 열고 **메트릭** 탭에서 이 이벤트들을 그대로 볼 수 있습니다. 이벤트 목록과 카테고리별 누적 카운트가 표시되므로, 플랫폼 대시보드를 보지 않고도 계측이 도는지 즉시 확인할 수 있습니다.

디버그 콘솔은 Dev Server 프로필에서 기본 활성화되어 있고, 다른 프로필에서도 [빌드 프로필](BuildProfiles.md)의 `AIT_DEBUG_CONSOLE` 환경 변수로 켤 수 있습니다.

> **참고**: 카테고리별 카운트 표는 위 8개 카테고리 이름을 부분 문자열로 매칭합니다. `unity_first_interactive`는 그중 어디에도 걸리지 않아 표 아래쪽에 별도 행으로 나타납니다. 누락이 아니라 분류 방식의 차이입니다.

## 안전장치

| 항목 | 설명 |
|------|------|
| try-catch | 모든 핸들러를 감싸 로깅 실패가 게임을 멈추지 않음 |
| 재진입 방지 | `_isSending` 가드로 `logMessageReceived` → `SendLog` → 경고 로그 → `logMessageReceived` 무한 루프 차단 |
| Rate limiting | 카테고리별 고정 한도로 과도한 전송 방지 |
| 문자열 절단 | 에러 메시지·스택 트레이스를 고정 길이에서 자름 |

재진입 가드가 특히 중요합니다. `SendLog`가 WebGL 이외 환경에서 `Debug.LogWarning`을 쓰지 않는 것도 같은 이유입니다 — 경고를 남기면 그 경고가 다시 `logMessageReceived`를 타고 들어옵니다.

## 관련 문서

- [빌드 프로필](BuildProfiles.md) — 디버그 콘솔 켜고 끄기
- [Sentry 연동](SentryIntegration.md) — 에러를 Sentry로도 보내기
- [API 사용 패턴](APIUsagePatterns.md) — 직접 이벤트를 보내는 Analytics API
