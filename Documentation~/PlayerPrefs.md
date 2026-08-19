# PlayerPrefs 영속화

Unity WebGL의 `PlayerPrefs`는 Emscripten IDBFS(IndexedDB) 위에 저장됩니다. 그런데 앱인토스 WebView를 포함한 대부분의 모바일 웹뷰 환경에서는 IndexedDB 영속성이 보장되지 않습니다(iOS ITP의 저장소 자동 정리, 앱 재실행 시 세션 격리 등). 그 결과 게임 코드는 전혀 바꾸지 않았는데도 저장한 값이 다음 실행에서 사라지는 문제가 생길 수 있습니다.

SDK는 이 문제를 게임 코드 수정 없이 투명하게 보완합니다. `PlayerPrefs` 파일만 골라 앱인토스 Storage(영속 보장)에 함께 미러링하고, IndexedDB가 비어 있거나 손상되어도 그 미러에서 복원합니다. `PlayerPrefs` API는 그대로 쓰면 됩니다.

## 동작 방식

### 부트 시 복원

Unity 로더가 게임을 초기화하는 과정에서 IDBFS를 마운트하는 시점을 가로채, Unity의 첫 씬 `Awake` 전에 앱인토스 Storage의 스냅샷을 복원합니다. Unity의 `addRunDependency` 게이트를 그대로 사용하므로, 게임 코드 입장에서는 복원이 끝난 뒤에 `Awake`가 실행된다는 보장이 있습니다.

1. 원본 IndexedDB → MEMFS 복원(순정 Unity 동작)을 먼저 마칩니다.
2. 그 직후 앱인토스 Storage에서 스냅샷을 가져옵니다. 스냅샷이 있으면 `PlayerPrefs` 파일만 그 내용으로 덮어씁니다(스냅샷에 없는 파일은 `PlayerPrefs.DeleteAll` 결과로 간주해 제거).

### 저장 시 이중 기록

레이어가 정상 활성(`mode: 'ait'`) 상태일 때, `PlayerPrefs` 파일이 디스크에 flush될 때마다 두 경로에 동시에 씁니다.

- 앱인토스 Storage에 스냅샷 push (영속 보장 경로)
- 원본 IndexedDB syncfs (warm cache / 폴백 경로, 그대로 유지)

변경된 내용이 마지막으로 올린 스냅샷과 같으면(해시 비교) push를 생략해 불필요한 쓰기를 줄입니다.

### Save() 없이도 미러링됨

런타임에 index.html의 `configure(config)` 호출이 Unity 로더 config에 `autoSyncPersistentDataPath = true`를 주입해, 파일이 close될 때마다 자동 persist 기회를 얻습니다. 즉 게임이 `PlayerPrefs.Save()`를 명시적으로 호출하지 않아도 미러링될 수 있습니다(다만 백그라운드 전환 등 특정 타이밍에서의 보장 범위는 [알려진 이슈](#알려진-이슈) 참고).

### 강제 flush

탭이 백그라운드로 전환되거나 페이지가 언로드되는 순간에도 최신 상태를 놓치지 않도록 `visibilitychange`(hidden 전환)와 `pagehide` 이벤트에서 즉시 push를 시도합니다.

## 설정

Configuration 창의 **WebGL 최적화 설정** 섹션에 **"PlayerPrefs 영속화"** tri-state 드롭다운이 있습니다.

| 옵션 | 의미 |
|------|------|
| 자동 (활성화) | 기본값. 버전별 분기 없이 항상 활성(`GetDefaultPlayerPrefsPersistence()`는 무조건 `true` 반환) |
| 비활성화 | 레이어를 완전히 끄고 순정 Unity 동작만 사용 |
| 활성화 | 명시적으로 켬 (기본값과 동일하게 동작) |

기본값은 **활성화**입니다(`AITEditorScriptObject.playerPrefsPersistence`가 `-1`일 때 `AITDefaultSettings.GetDefaultPlayerPrefsPersistence()`가 `true`를 반환).

실효값은 fail-open으로 계산됩니다 — 설정 로드 자체가 실패해도 기본은 "보호 유지(활성화)" 쪽으로 넘어갑니다.

### 빌드 로그로 확인

빌드할 때마다 Console에 실효값이 찍힙니다.

```text
[AIT]   - PlayerPrefs 영속화: True (자동)
```

명시적으로 켜거나 끈 경우 `(자동)` 대신 `(명시)`로 표시됩니다.

## 범위와 한계

- **`PlayerPrefs` 파일만** 대상입니다. IDBFS의 다른 파일(예: 게임이 직접 만든 세이브 파일)은 이 레이어가 건드리지 않습니다.
- 스냅샷 크기 상한은 **512KB**(524288자)입니다. 이 상한은 base64 인코딩(≈1.33배)과 JSON 봉투를 포함한 **직렬화된 매니페스트 문자열** 기준이므로, 실제 저장 가능한 PlayerPrefs 원시 데이터는 대략 **380~390KB** 수준입니다. 초과하면 push를 생략하고 콘솔 경고를 남기되, **기존에 저장된 스냅샷은 그대로 보존**됩니다 — 초과 직전까지의 데이터는 안전합니다.
- **커스텀(비AITTemplate) WebGL 템플릿에는 적용되지 않습니다.** 아래 참고.

> **참고**: Unity WebGL 템플릿으로 AITTemplate이 아닌 다른 템플릿을 사용해 빌드한 경우(수동 WebGL 빌드 등), `index.html`의 `%AIT_PLAYERPREFS_PERSISTENCE%` 치환과 `ait-playerprefs.js` 스크립트 삽입이 일어나지 않아 이 기능이 적용되지 않습니다. 자세한 커스터마이징 범위는 [빌드 커스터마이징](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization)을 참고하세요.

## 하위 호환 · 마이그레이션

이미 IndexedDB에 `PlayerPrefs` 데이터가 있는 기존 배포(이 기능 도입 이전 빌드)를 이 기능이 켜진 빌드로 업데이트하면, 다음 순서로 자동 마이그레이션됩니다.

1. 앱인토스 Storage에 매니페스트 키(`AITUnityFS_v1_manifest`)가 아직 없는지 확인합니다(부재 = 이 기기에서 이 레이어가 처음 동작).
2. 매니페스트가 없으면 기존 IndexedDB 상태를 그대로 채택해 부팅합니다.
3. 채택 직후 그 상태를 앱인토스 Storage로 즉시 승격 push해, 다음부터는 정상적으로 이중 기록됩니다.

게임 쪽에서 별도로 할 일은 없습니다.

## 안전장치 (kill-switch)

어떤 실패 경로에서도 부팅을 막지 않는 것이 최우선 원칙입니다. 문제가 감지되면 단계적으로 강등되며, 최종적으로는 항상 **순정 IndexedDB 동작으로 무회귀 폴백**합니다.

| 레벨 | 트리거 | 동작 | 재시도 여지 |
|------|--------|------|-------------|
| L1 | 앱인토스 Storage 가용성 프로브 실패 또는 부트 게이트 타임아웃(2.5초) | 이번 세션만 AIT 쓰기 금지(in-memory), `mode: 'vanilla'`로 강등 | 있음 — 다음 reload에서 재시도 |
| L2 | `setItem` 연속 3회 실패 | 이번 세션 중단(이전 스냅샷은 보존, IndexedDB 미러는 계속 동작) | L3로 승격 |
| L3 | L2 도달 시 자동 설정 | `sessionStorage`에 킬 플래그를 남겨 **이번 탭 세션 전체**에서 레이어를 끔 | 새 탭/세션에서 초기화 |

L1은 일시적 타이밍 문제일 수 있으므로 L3까지 걸지 않습니다. L2에서 반복 실패가 확정되면 그때 L3로 세션 전체를 끕니다.

## 진단

### `window.AITPlayerPrefs.status()`

브라우저 콘솔(또는 E2E)에서 현재 상태를 조회할 수 있습니다.

| 필드 | 값 | 의미 |
|------|-----|------|
| `enabled` | boolean | 빌드 설정상 활성 여부 |
| `backend` | `'platform'` \| `'override'` \| `'none'` | 실제 사용 중인 storage 백엔드 |
| `disabled` | boolean | AIT 쓰기 금지 여부(L1/L2 강등, 빌드 설정 opt-out(`mode='disabled'`), L3 세션 킬 상태로 부팅 시에도 true) |
| `mode` | `'pending'` \| `'ait'` \| `'vanilla'` \| `'disabled'` \| `'foreign'` | 현재 동작 모드 |
| `restoredBytes` | number | 부트 시 복원한 바이트 수 |
| `mirrorCount` | number | 앱인토스 Storage로 성공한 push 횟수 |
| `lastError` | string \| null | 마지막으로 기록된 에러 설명 |

### 콘솔 경고 (`[AIT-PP]` 접두사, 키당 1회)

| 키 | 상황 |
|----|------|
| `prod-override` | 테스트용 storage 오버라이드 훅이 사용됨(정식 빌드는 항상 프로덕션으로 간주되므로 오버라이드 훅이 있으면 경고 — E2E/로컬 하네스가 `isProduction:false`를 주입한 경우만 억제) |
| `foreign-manifest` | 매니페스트 키가 이 레이어가 아닌 다른 주체의 값으로 이미 사용 중 |
| `too-large` | 스냅샷이 512KB 상한을 초과해 push를 건너뜀 |
| `l2` | `setItem` 연속 3회 실패로 세션 중단 |
| `no-mount` | IDBFS 마운트를 인식하지 못함 |
| `self-check` | 필요한 IDBFS API를 찾지 못함 |
| `trap-too-late` | 마운트 트랩 설치보다 IDBFS 마운트가 먼저 끝남 |
| `scope-miss` | 첫 persist 시점에 `PlayerPrefs` 파일 경로를 찾지 못함 |

## 앱인토스 Storage를 직접 쓰는 게임을 위한 주의

이 레이어는 앱인토스 Storage에서 **`AITUnityFS_v1_manifest`라는 키 하나만** 사용합니다. `getItem`/`setItem`만 호출하며, `removeItem`이나 전체 삭제는 호출하지 않습니다. 게임 코드가 `AIT.Storage*` API로 직접 앱인토스 Storage를 쓰고 있다면, **이 키 하나만 피하면** 이 레이어와 절대 충돌하지 않습니다. 그 외 어떤 키에도 이 레이어는 접근하지 않습니다.

- 게임이 실수로 이 키를 직접 써버리면, 레이어는 다음 스냅샷 읽기에서 자신의 포맷이 아님을 감지해 **"foreign"으로 분류**하고 그 세션 동안 이 키에 대한 쓰기를 완전히 중단합니다. 게임이 써넣은 기존 값은 보호되며 덮어쓰지 않습니다.

> **주의**: `AIT.StorageClearItems()`는 앱인토스 Storage의 **모든 키를 삭제**합니다. 여기에는 이 레이어의 매니페스트 키도 포함됩니다. 이 API를 호출하면 PlayerPrefs 백업이 함께 사라지고, 다음 부팅에서는 (매니페스트 부재로 인식되어) IndexedDB에 남아 있는 값으로 재마이그레이션됩니다 — IndexedDB도 이미 비어 있었다면 PlayerPrefs 데이터가 완전히 유실됩니다. 게임 자체 데이터를 초기화할 목적으로 `StorageClearItems()`를 쓸 때는 이 부수 효과를 고려하세요.

## 테스트 훅

`window.__AIT_PLAYERPREFS_STORAGE__`에 `getItem`/`setItem`을 가진 객체를 대입하면 플랫폼 Storage 대신 그 객체를 씁니다. 오버라이드 훅이 있으면 항상 최우선으로 사용됩니다.

이 훅이 감지되면 `[AIT-PP] window.__AIT_PLAYERPREFS_STORAGE__ 오버라이드가 프로덕션에서 사용됩니다. 테스트용 훅이 남아있지 않은지 확인하세요.` 경고가 1회 출력됩니다(`prod-override`는 1회 억제용 내부 키) — 정식 빌드는 항상 프로덕션으로 간주되므로(index.html이 `isProduction`을 주입하지 않아 기본값 `true`) 오버라이드 훅이 있으면 어떤 정식 빌드에서든 경고가 뜹니다. E2E/로컬 하네스가 스크립트 로드 전 `isProduction:false`를 주입한 경우에만 억제됩니다.

## 알려진 이슈

**Unity 2021.3 순정 IDBFS 세션 노화 결함(CI에서 재현됨)**: E2E CI(Chromium, macOS/Windows)에서 Unity 2021.3(Emscripten 2.0.19) 빌드는 세션 시작 후 약 60초가 지나면 **이 레이어와 무관하게** 순정 IDBFS 저장이 통째로 죽는 현상이 나타납니다. 레이어를 켠 상태에서는 앱인토스 Storage 경로가 2021.3을 포함한 전 버전에서 정상 동작하므로, 이 결함은 오히려 이 기능이 필요한 이유를 뒷받침합니다. 다만 **2021.3에서 이 기능을 opt-out하는 것은 신중히 결정하세요** — 순정 IDBFS만으로는 같은 결함에 노출될 수 있습니다. 실기기(토스 앱 WebView)에서의 재현 여부는 아직 확인 중입니다.

## 관련 문서

- [빌드 파이프라인](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-process) — `%AIT_PLAYERPREFS_PERSISTENCE%` 치환이 일어나는 지점
- [빌드 커스터마이징](https://developers-apps-in-toss.toss.im/documentation/unity/build/build-customization) — 커스텀 템플릿에서의 제약
- [API 사용 패턴](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/api-usage-patterns) — `AIT.Storage*` API 사용법
- [FAQ](https://developers-apps-in-toss.toss.im/documentation/unity/first-steps/faq) — 일반 문제 해결
