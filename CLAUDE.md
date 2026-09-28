# CLAUDE.md

이 파일은 Claude Code (claude.ai/code)가 이 저장소의 코드를 다룰 때 참고하는 가이드입니다.

## 개요

**Apps in Toss Unity SDK** - Unity/Tuanjie 게임을 Apps in Toss 플랫폼의 미니앱으로 변환·배포하는 Unity 패키지입니다.

## ⚠️ 필수 규칙

### 브랜치 보호 규칙
- **main에 직접 push 금지. feature 브랜치 → PR → squash merge만 허용**
- Repository Rulesets(서버 강제): PR 필수(승인 없이 머지 가능) · **squash만**(merge commit·rebase 불가) · 커밋 서명 필수(`required_signatures`) · 삭제·force push 불가 · bypass 없음(`current_user_can_bypass: never`)

### 머지 실행 정책
- **명시적 머지 요청**이 있을 때만 머지한다(예: "머지해줘" + 확인). PR 생성·push 같은 일반 작업의 일부로는 자동 머지하지 않는다.
- 순서: ① mergeable이고 required check가 전부 success인지 확인. `gh api repos/toss/apps-in-toss-unity-sdk/pulls/<N> --jq .mergeable_state`가 `clean`이면 충족, `unstable`이면 실패·대기 중인 체크가 non-required뿐인지 `commits/<sha>/check-runs`로 확인, 그 외 값은 중단 ② 머지 직전 재확인("PR #N을 squash merge합니다. 진행할까요?") ③ `gh api -X PUT repos/toss/apps-in-toss-unity-sdk/pulls/<N>/merge -f merge_method=squash`
- Ruleset이 squash·서명·non-bypass를 서버에서 강제하므로 머지도 이 경계 안에서만 가능하다.

### Git 커밋 가이드라인
- **커밋 메시지는 한국어 `<타입>: <설명>` 형식**(기능/수정/개선/문서/리팩토링/테스트/빌드)
- 예: ✅ `수정: WebGL 빌드 오류 해결` / ❌ `feat: Add user authentication API`(영어 불허)

### 문서 생성 정책
- **사용자의 명시적 허락 없이 모든 `*.md` 파일의 생성·수정을 금지**(README, CHANGELOG, 기타 문서 파일 전부 포함)
- **예외**: 명시적으로 지시받은 경우의 CLAUDE.md 수정, PR 제출 시 자동 최신화하는 TODO.md(아래 규칙 참조)

### 저장소 소유권 및 공개 범위
- ⚠️ **GitHub public 저장소다.** push한 브랜치·커밋·PR(제목·본문)·Actions 로그는 즉시 공개되고 회수 불가능하다.
- **기업 소유, source-available(OSS 아님).** LICENSE·라이선스 정보·package.json `"license"` 필드 추가 금지.
- 🔒 **비공개 자원을 여기에 엮지 않는다.** 비공개 repo의 이름·존재·clone URL·빌드 워크플로·관련 PR·커밋 메시지를 브랜치·PR·Actions 로그·커밋 어디에도 넣지 않는다(이 파일도 공개되므로 예시로도 적지 않는다). 비공개 프로젝트의 빌드·측정은 로컬에서 하고 산출물도 로컬에만 둔다.
- 노출되면 브랜치 삭제·PR close로 제거되지 않는다(GH Archive 잔존). 노출되면 즉시 사용자에게 알리고 GitHub Support 퍼지를 요청한다.

### 자동 생성 코드 정책
- **`Runtime/SDK/` 파일을 직접 수정하지 말 것.** `sdk-runtime-generator~/`에서 자동 생성되며, 직접 수정하면 `pnpm generate` 때 덮어씌워진다.
- 변경 시 생성기 코드를 고친 뒤 `pnpm generate` → `./run-local-tests.sh --validate` 순으로 검증한다. 전체 절차: `.claude/commands/generate.md`(`/generate`).

### TODO.md 최신화
- **PR 제출마다** `TODO.md` 확인, 완료 항목은 통째로 제거(주석·취소선 금지)
- 완료 여부는 PR의 코드 변경이 문제를 실제로 해결했는지로 판단
- TODO.md 변경은 해당 PR 커밋에 포함(별도 커밋 금지)
- 항목은 요지와 심볼 포인터로, 줄번호·조사 경위는 적지 않는다(재비대화 방지)

### 파일 위생
- 불필요한 파일은 적극적으로 `.gitignore`에 추가한다
- `git status`에 `webgl/`, `ait-build/dist/`, `Library/`, `Temp/`, `*.log` 등이 보이면 누락을 검토한다
- 커밋 직전 `git diff --cached`로 확인: 의도치 않은 `Runtime/SDK/` 산출물, `.meta` 누락·추가, 대용량 바이너리 혼입

## 자주 하는 실수 방지

### pnpm 버전 핀 동기화

`Editor/AITPackageManagerHelper.cs`의 `PNPM_VERSION` 상수와 다음 세 파일의 `"packageManager"` 필드를 동기화한다:

- `package.json`
- `sdk-runtime-generator~/package.json`
- `WebGLTemplates/AITTemplate/BuildConfig~/package.json`

### GitHub CLI / Actions
- GraphQL 기반 `gh` 명령(`gh pr create`/`merge`/`view`, `gh workflow run`)은 실패하므로 REST `gh api`를 쓴다.
- PR 생성: `gh api -X POST repos/toss/apps-in-toss-unity-sdk/pulls --input pr.json`
- 트리거 예시·ID 테이블: `Documentation~/internal/github-actions.md`
- PR 번호는 `target_ref`에 숫자만(#불필요)

### 테스트 관련
- E2E 전 빌드 필요: `--all`(빌드+테스트) vs `--e2e`(테스트만). EditMode는 빌드 없이 ~10초
- 상세 구조와 로컬 CI 재현: `Documentation~/internal/testing.md`
- E2E 실패 시 `e2e-triage` 스킬을 먼저 로드한다

### Sentry 이슈 관련
EditMode 테스트가 의도적으로 내는 이슈는 이미 ignored 처리돼 있다. 목록과 절차: `Documentation~/internal/sentry-known-issues.md`.

### Library/Bee 캐시 동작
의심되는 빌드 실패는 workflow_dispatch `clean_library=true`로 재트리거해 재현한다. 정책: `Documentation~/internal/github-actions.md`의 "Library/Bee 캐시 무효화 정책".

## 빠른 참조: 주요 명령어

- SDK 재생성: `cd sdk-runtime-generator~ && pnpm generate`
- 로컬 테스트 옵션·소요시간: `./run-local-tests.sh --help`

## Verification Commands

review-fix-loop 등 자동화 skill이 파싱하는 규약 섹션. 각 항목은 실제 명령 또는 `none` 리터럴.

- **Typecheck**: `./run-local-tests.sh --validate`
- **Test**: none
- **Lint**: none

`--validate`(~30초)는 파일 구조 검증 + Playwright 설정 + SDK 유닛 테스트(vitest invariants 포함)를 묶어서 실행하므로 별도 Test 항목을 두지 않는다. Unity E2E(`--all`)는 비용이 크고 매 패스 실행에 부적합해 제외한다 — 변경이 E2E에 영향을 주면 `./run-local-tests.sh --e2e`를 수동 호출한다. Lint(`.meta` 체크, 포맷 등)는 GitHub Actions `lint` 워크플로우가 담당한다.

## 상세 문서

`Documentation~/`가 문서 루트다. 공개 문서 정본은 포털(https://developers-apps-in-toss.toss.im/documentation/unity)이고, 저장소에는 사본을 두지 않는다.

**내부 런북**(`Documentation~/internal/`) — 필요할 때 Read로 참조:

- `github-actions.md`: 워크플로우 목록·트리거, 상태 확인, `rerun-failed-jobs`, E2E flaky
- `project-structure.md`: 디렉토리 지도
- `implementation-details.md`: 관심사→파일 매핑
- `testing.md`: 테스트 구조, Unity 버전, 로컬 CI 재현
- `sdk-generator.md`: 생성기 입출력·타입 매핑
- `sentry-known-issues.md`: 무시 가능 이슈, resolve 절차
- `build-session-recovery.md`: `AITBuildSessionRecovery` 수동 재현 절차
- `playerprefs-device-verification.md`: 실기기 검증 절차

**저장소 잔류 문서**(`Documentation~/`):

- `Contributing.md`: 개발 환경, git hooks, 커밋·PR 규칙
- `ManualIntegration.md`: 수동 WebGL 빌드
- `BetaChannel.md` / `PerfBetaChannel.md`: 옵트인 채널 가이드
- `changelog/`: 변경 이력
