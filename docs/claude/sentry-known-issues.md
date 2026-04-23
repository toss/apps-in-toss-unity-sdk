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
