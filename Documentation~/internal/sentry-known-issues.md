# Sentry 알려진 이슈

## 무시해도 되는 이슈 패턴

EditMode 통합 테스트에서 의도적으로 발생시키는 이벤트입니다.

- `EditMode 통합 테스트: pnpm build 실패 시뮬레이션` (SDK-9)
- `EditMode 통합 테스트: 에러 이벤트 전송 확인` (SDK-B)
- `EditMode 통합 테스트: WebGL 빌드 실패 시뮬레이션` (SDK-C)
- `시뮬레이션: FAIL_NPM_BUILD` (SDK-8)
- `테스트 예외: Error Tracker 전송 확인용` (SDK-3)
- `테스트 에러: Error Tracker 전송 확인용` (SDK-2)
- `AITNpmRunner: pnpm install 실패 — ENOENT` (SDK-A) — 테스트 환경(`test: true`)에서만 78회 발생

이 이슈들은 `ErrorTrackerIntegrationTests.cs`에서 Sentry 전송 기능을 검증하기 위해 의도적으로 생성됩니다.

Sentry에서 이미 **ignored** 처리되어 있으므로, 새로 발생해도 별도 조치 불필요입니다.

## SDK 문제가 아닌 사용자 프로젝트/환경 이슈

resolve 처리됨, 재발 시 모니터링합니다.

- FMOD 오디오 오류 — 사용자 프로젝트의 WebGL 오디오 포맷 설정 문제
- GUI Layer/tk2dCamera 경고 — 레거시 Unity 컴포넌트 호환성 문제
- `Failed to compile player scripts` — 사용자 프로젝트 스크립트 컴파일 오류
- `FAIL_NPM_BUILD` — 사용자 빌드 환경(Node.js/pnpm) 문제

## 통합 테스트 environment 분리

- `ErrorTrackerIntegrationTests.cs`는 실제 Sentry DSN에 envelope을 POST해 HTTP 200을 검증
- 이 테스트는 `environment: "edit-mode-test"` 로 전송됨 (프로덕션 `editor` 환경과 구분)
- **Sentry 대시보드에서 Inbound Filter 설정 필요** (수동, 1회성):
  - Sentry → `apps-in-toss-unity-sdk` Project → Settings → Inbound Filters
  - "Filter by environment" 활성화 → `edit-mode-test` 차단
  - 추가 안전망: "Filter by error message" → `[AIT-TEST]` prefix 포함 메시지 차단
- 이 필터 설정 없이는 CI에서 통합 테스트가 돌 때마다 프로덕션 Sentry에 이벤트가 유입됨

## 정상 흐름 fallback warning 컨벤션

SDK 정상 흐름의 fallback / timeout / 예측된 분기에서 발생하는 warning은
`Debug.LogWarning(...)`이 아닌 `AITLog.Warning(msg, sentryCapture: false)`를
사용한다 (`Editor/AITLog.cs`).

**판정 기준**: "이 메시지가 Sentry 이슈로 등록되었을 때 SDK 결함 조사가 필요한가?"
필요 없으면(예: 네트워크 timeout, 사용자 환경 차이로 인한 분기) `sentryCapture: false`.

**대표 사례 (점진 적용 중)**:
- `Editor/Menu/PortResolver.cs` — Vite 포트 polling timeout 후 브라우저 직접 열기
- `Editor/AITDeprecationChecker.cs` — sdk-policy.json fetch 실패 시 호환성 검사 스킵

`AITDeprecationChecker.cs`에는 다른 `Debug.LogWarning(...)` 호출이 남아 있다(파싱 실패,
minVersion 비정상, 태그 자동 감지 실패 등). 이 컨벤션은 일괄 적용이 아니라 신규/수정
호출 지점에서 판정 기준에 따라 점진 적용한다.

**리뷰 항목**: 새 PR에서 `Debug.LogWarning(` 추가 시 — 진짜 결함 조사가 필요한지
호출 지점 컨텍스트로 판정. 불필요하면 `AITLog.Warning(..., sentryCapture: false)`를 권고.

## 이중 안전망: strict 게이트 + NonSdkMessagePatterns

`Editor/ErrorTracker/AITEditorErrorTracker.cs`의 필터 체인은 두 메커니즘으로 노이즈를 차단한다.

1. **`NonSdkMessagePatterns` 배열**: 알려진 Unity/사용자 메시지의 부분 문자열 매칭.
   명시적이라 코드 리뷰로 의도 확인 가능.
2. **strict error_source 게이트** (`ShouldDropAsNonSdkSource`): `DetermineErrorSource()`가
   `"sdk"`로 분류하지 않은 메시지를 모두 드롭. 새 노이즈가 등장해도 패턴 추가 없이 차단.

두 메커니즘은 독립적이라 한쪽이 실패해도 다른 쪽이 보완한다. strict 게이트가
SDK 결함을 false negative로 드롭하면 §"무시해도 되는 이슈 패턴" 모니터링에서
포착 → `IsAitRelated` 화이트리스트 또는 `SdkMessagePatterns` 키워드 추가로 복구.
