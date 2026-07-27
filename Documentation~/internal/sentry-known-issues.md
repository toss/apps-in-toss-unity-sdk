# Sentry 알려진 이슈

Sentry에 뜨는 이슈 중 조치가 필요 없는 것과, 노이즈를 막는 장치에 대한 내부 메모입니다.

> **대상**: SDK 기여자. 사용자 프로젝트에서 Sentry를 붙이는 방법은 [Sentry 연동](../SentryIntegration.md)에 있습니다.

## 무시해도 되는 이슈 패턴

EditMode 통합 테스트가 Sentry 전송 기능을 검증하려고 의도적으로 발생시키는 이벤트입니다. `ErrorTrackerIntegrationTests.cs`가 출처이고, Sentry에서 이미 ignored 처리되어 있어 새로 발생해도 조치가 필요 없습니다.

| 메시지 | 이슈 |
|--------|------|
| `EditMode 통합 테스트: pnpm build 실패 시뮬레이션` | SDK-9 |
| `EditMode 통합 테스트: 에러 이벤트 전송 확인` | SDK-B |
| `EditMode 통합 테스트: WebGL 빌드 실패 시뮬레이션` | SDK-C |
| `시뮬레이션: FAIL_NPM_BUILD` | SDK-8 |
| `테스트 예외: Error Tracker 전송 확인용` | SDK-3 |
| `테스트 에러: Error Tracker 전송 확인용` | SDK-2 |
| `AITNpmRunner: pnpm install 실패 — ENOENT` | SDK-A |

`AITNpmRunner: pnpm install 실패 — ENOENT`는 테스트 환경(`test: true`)에서만 발생합니다.

## SDK 문제가 아닌 이슈

사용자 프로젝트나 실행 환경에서 오는 것들입니다. resolve 처리하고 재발만 모니터링합니다.

| 증상 | 원인 |
|------|------|
| FMOD 오디오 오류 | 사용자 프로젝트의 WebGL 오디오 포맷 설정 |
| GUI Layer, tk2dCamera 경고 | 레거시 Unity 컴포넌트 호환성 |
| `Failed to compile player scripts` | 사용자 프로젝트 스크립트 컴파일 오류 |
| `FAIL_NPM_BUILD` | 사용자 빌드 환경의 Node.js 또는 pnpm 문제 |

## 통합 테스트 environment 분리

`ErrorTrackerIntegrationTests.cs`는 실제 Sentry DSN에 envelope을 POST해 HTTP 200을 검증합니다. 이 이벤트는 `environment: "edit-mode-test"`로 전송되어 프로덕션 `editor` 환경과 구분됩니다.

Sentry 대시보드에서 Inbound Filter를 한 번 설정해야 합니다.

1. Sentry의 `apps-in-toss-unity-sdk` 프로젝트에서 Settings의 Inbound Filters로 들어갑니다.
2. Filter by environment를 켜고 `edit-mode-test`를 차단합니다.
3. 추가 안전망으로 Filter by error message에 `[AIT-TEST]` 접두사를 차단합니다.

> **중요**: 이 필터가 없으면 CI에서 통합 테스트가 돌 때마다 프로덕션 Sentry에 이벤트가 유입됩니다.

## fallback warning 컨벤션

SDK 정상 흐름의 fallback이나 timeout, 예측된 분기에서 나는 warning은 `Debug.LogWarning(...)`이 아니라 `AITLog.Warning(msg, sentryCapture: false)`를 씁니다(`Editor/AITLog.cs`).

판정 기준은 하나입니다 — "이 메시지가 Sentry 이슈로 등록됐을 때 SDK 결함 조사가 필요한가?" 필요 없으면(네트워크 timeout, 사용자 환경 차이로 인한 분기 등) `sentryCapture: false`입니다.

적용된 사례는 아래와 같습니다.

- `Editor/Menu/PortResolver.cs` — Vite 포트 polling timeout 후 브라우저 직접 열기
- `Editor/AITDeprecationChecker.cs` — `sdk-policy.json` fetch 실패 시 호환성 검사 스킵

`AITDeprecationChecker.cs`에는 다른 `Debug.LogWarning(...)` 호출이 남아 있습니다(파싱 실패, minVersion 비정상, 태그 자동 감지 실패 등). 이 컨벤션은 일괄 적용이 아니라 신규나 수정 호출 지점에서 판정 기준에 따라 점진 적용합니다.

> **권장**: 새 PR에서 `Debug.LogWarning(`이 추가되면 호출 지점 컨텍스트로 결함 조사가 필요한지 판정하고, 불필요하면 `AITLog.Warning(..., sentryCapture: false)`를 권합니다.

## 이중 안전망

`Editor/ErrorTracker/AITEditorErrorTracker.cs`의 필터 체인은 두 메커니즘으로 노이즈를 막습니다.

1. **`NonSdkMessagePatterns` 배열** — 알려진 Unity·사용자 메시지의 부분 문자열 매칭. 명시적이라 코드 리뷰로 의도를 확인할 수 있습니다.
2. **strict error_source 게이트**(`ShouldDropAsNonSdkSource`) — `DetermineErrorSource()`가 `"sdk"`로 분류하지 않은 메시지를 전부 드롭합니다. 새 노이즈가 등장해도 패턴 추가 없이 차단됩니다.

두 메커니즘은 독립적이라 한쪽이 실패해도 다른 쪽이 보완합니다. strict 게이트가 SDK 결함을 false negative로 드롭하면 위 무시 가능 이슈 모니터링에서 포착한 뒤 `IsAitRelated` 화이트리스트나 `SdkMessagePatterns` 키워드 추가로 복구합니다.

## 자동 resolve 시점

노이즈 패턴 추가 PR의 본문에 넣는 `Fixes APPS-IN-TOSS-UNITY-SDK-XX`는 squash 커밋에 그대로 들어가지만, 이슈가 실제로 닫히는 건 **다음 릴리즈가 cut될 때**입니다. `release.yml`의 Sentry 릴리즈 등록 잡이 릴리즈에 커밋 범위를 연결하고, 그 커밋의 trailer가 참조한 이슈를 Sentry가 resolve합니다.

따라서 머지 직후에 대상 이슈가 여전히 unresolved로 보이는 것은 정상입니다. 릴리즈를 기다릴 수 없거나, 구버전 SDK에서 유입되어 미래 릴리즈 커밋 범위에 잡히지 않는 잔여 이벤트는 수동으로 resolve합니다.

## 관련 문서

- [Sentry 연동](../SentryIntegration.md) — 사용자 프로젝트 관점의 Sentry 설정
- [GitHub Actions 워크플로](github-actions.md) — 릴리즈 워크플로
- [테스트 전략](testing.md) — 통합 테스트가 도는 위치
