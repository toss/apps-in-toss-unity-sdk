---
name: e2e-triage
description: E2E Tests 워크플로우 실패 시 사용 — 인프라 기인 flaky 판별, rerun-failed-jobs로 실패 job만 재실행, Unity 라이선스 2연속 실패 에스컬레이션, Sentry 노이즈/자동 resolve 처리 기준.
---

# E2E 테스트 실패 대응

- E2E 실패 시 먼저 **코드 변경과 무관한 인프라 이슈인지** 판별 — 알려진 flaky 패턴(Unity 라이선스 충돌 / Windows artifact finalize / Brotli·Gzip 크래시 / warm-reload `unityInstance` 120s 타임아웃)의 시그니처·판별 상세와 red herring 목록은 `Documentation~/internal/github-actions.md`의 "E2E 알려진 flaky 패턴"을 Read 후 대조
- **인프라 기인 실패 시**: `rerun-failed-jobs`로 실패한 job만 재실행 (전체 재실행보다 성공률 높음 — self-hosted runner 리소스 경합 감소)
  ```bash
  gh api repos/{owner}/{repo}/actions/runs/{run_id}/rerun-failed-jobs -X POST
  ```
- ⚠️ **동일 Unity 라이선스 시그니처가 2회 연속이면 transient가 아니다** — 라벨 핀된 단일 러너의 라이선스가 실제로 깨진 경우로, 수동 수복 전까지 매번 동일 시그니처로 실패한다. 반복 rerun으로 시간 낭비 금지, 러너 라이선스 수복 필요로 **에스컬레이션**
- **E2E Tests는 non-required라 머지 차단 안 함** — 단일 leg 랜덤 실패면 transient로 보고 실패 leg만 재실행
- **Sentry 노이즈 패턴 추가**는 자동화(`auto-resolve`)가 처리하므로 수동 PR 불필요 — `Editor/ErrorTracker/AITEditorErrorTracker.cs`의 `NonSdkMessagePatterns`에 자동 흡수됨
- **Sentry 자동 resolve는 "머지 시점"이 아니라 "다음 릴리즈 시점"에 발생 (release-gated)** — 머지 직후 대상 이슈가 `unresolved`로 보이는 게 정상 (버그 아님). 즉시 닫아야 하는 경우와 잔여 이벤트(미래 릴리즈 커밋 범위 밖 — 항상 수동 resolve)의 Sentry MCP 절차는 `Documentation~/internal/sentry-known-issues.md`의 "자동 resolve 시점" 참조
