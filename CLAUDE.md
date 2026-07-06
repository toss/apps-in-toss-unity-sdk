# CLAUDE.md

이 파일은 Claude Code (claude.ai/code)가 이 저장소의 코드를 다룰 때 참고하는 가이드입니다.

## 개요

**Apps in Toss Unity SDK** - Unity/Tuanjie 엔진 게임 프로젝트를 Apps in Toss 플랫폼의 미니앱으로 변환하고 배포할 수 있게 해주는 Unity 패키지입니다. SDK 제공 기능:

- Apps in Toss 플랫폼에 최적화된 WebGL 빌드 자동화
- AIT 메뉴 및 Configuration 윈도우를 통한 Unity Editor 통합
- 플랫폼별 브릿지 코드가 포함된 커스텀 WebGL 템플릿 (AITTemplate)
- granite 빌드 시스템을 사용한 Vite 기반 빌드 파이프라인
- 플랫폼 기능을 위한 종합 C# API (결제, 사용자 인증, 기기 API 등)

## ⚠️ 필수 규칙

### 브랜치 보호 규칙
- **main 브랜치에 직접 push 불가** — 반드시 PR을 통해 머지
- main 브랜치 규칙 (Repository Rulesets, 서버 측 강제):
  - PR 필수 (승인 없이 머지 가능)
  - **머지 방식: squash merge만 허용** (merge commit, rebase 불가)
  - 커밋 서명 필수 (`required_signatures`)
  - 삭제 불가, force push 불가
  - bypass 권한자 없음 (`current_user_can_bypass: never`)
- **작업 시**: 항상 feature 브랜치 생성 → PR 제출 → squash merge로 병합

### 머지 실행 정책
- Claude는 사용자의 **명시적 머지 요청**이 있을 때만 머지를 실행한다.
  - 허용 예: "머지해줘", "squash merge로 머지", "/ship merge" + 명시적 확인
  - PR 생성/푸시 같은 일반 작업의 일부로 자동 머지 금지
- 명시적 요청을 받은 경우 다음 절차로 진행:
  1. PR이 mergeable + 모든 required check가 success인지 확인
  2. 머지 직전에 한 번 더 사용자에게 확인 ("PR #N을 squash merge합니다. 진행할까요?")
  3. 사용자 확인 후 `gh pr merge <N> --squash` 실행
- GitHub Ruleset이 서버 단에서 squash merge / 서명 / non-bypass를 강제하므로 Claude의 머지 실행도 이 경계 안에서만 가능

### Git 커밋 가이드라인
- **모든 커밋 메시지는 반드시 한국어로 작성**
- 커밋 메시지 형식: `<타입>: <설명>`
  - 타입 예시: 기능, 수정, 개선, 문서, 리팩토링, 테스트, 빌드
- 예시:
  - ✅ `기능: 사용자 인증 API 추가`
  - ✅ `수정: WebGL 빌드 오류 해결`
  - ❌ `feat: Add user authentication API` (영어 - 허용 안됨)

### 문서 생성 정책
- **사용자의 명시적 허락 없이 *.md 파일을 생성하거나 수정하지 말 것**
- 해당 파일:
  - README.md, CHANGELOG.md, CONTRIBUTING.md
  - PRD.md, USER_GUIDE.md, API.md
  - 기타 모든 마크다운 문서 파일
- **예외**: 명시적으로 지시받은 경우 CLAUDE.md 수정 가능, TODO.md는 PR 제출 시 자동 최신화 (아래 규칙 참조)
- **이유**: AI가 생성한 문서는 기업 저장소에 부적절하게 보일 수 있음

### 저장소 소유권 및 공개 범위
- ⚠️ **이 저장소는 GitHub상 public(소스 공개) 저장소다.** 개발자가 UPM git URL(`im.toss.apps-in-toss-unity-sdk` → `github.com/toss/apps-in-toss-unity-sdk.git`)로 소비하는 **개발자용 SDK**라서 public이 의도된 설계다. **여기에 push하는 모든 브랜치·커밋·PR(제목/본문 포함)은 익명 사용자에게 즉시 공개**되며, 한 번 push되면 GH Archive 등에 수집되어 사실상 회수 불가다.
- **기업 소유이며 오픈소스 라이선스는 부여하지 않는다**(source-available, OSS 아님). 따라서 LICENSE 파일이나 오픈소스 라이선스 정보 추가 금지, package.json "license" 필드 추가 금지.
- 🔒 **비공개 자원을 이 public 저장소에 절대 엮지 말 것.** 비공개 repo(별도의 비공개 게임/앱 프로젝트 등)의 이름·존재·clone URL·빌드 워크플로·관련 PR/커밋 메시지를 이 저장소(브랜치/PR/Actions 로그/커밋)에 넣지 않는다. (이 CLAUDE.md 자체도 public이므로 비공개 repo 이름을 예시로도 적지 않는다.) 비공개 프로젝트의 빌드·측정 등은 **로컬 환경**에서 수행한다(로컬 git 자격증명으로 clone, 산출물은 로컬 디스크에만 보관). 실수로 노출 시 브랜치 삭제+PR close만으로는 완전 제거가 안 되므로(커밋/PR 페이지·GH Archive 잔존) GitHub Support 퍼지가 필요하다.

### 자동 생성 코드 정책
- **`Runtime/SDK/` 디렉토리의 파일을 직접 수정하지 말 것**
- `Runtime/SDK/`의 모든 파일은 `sdk-runtime-generator~/`에서 자동 생성됨
- 버그 수정이나 변경이 필요한 경우:
  1. `sdk-runtime-generator~/`의 생성기 코드 수정
  2. `pnpm run generate`로 SDK 파일 재생성
  3. `./run-local-tests.sh --all`로 변경사항 검증
- 생성된 파일을 직접 수정하면 다음 생성 시 덮어씌워짐

### TODO.md 최신화
- **PR을 제출할 때마다** `TODO.md`를 확인하고, 해당 PR의 변경사항으로 완료된 항목이 있으면 제거
- 확인 방법: PR에 포함된 커밋/변경 내용이 TODO 항목의 문제를 해결했는지 코드를 기준으로 검증
- 완료된 항목은 통째로 제거 (주석 처리나 ~~취소선~~ 사용하지 않음)
- TODO.md 변경은 별도 커밋이 아닌 해당 PR의 커밋에 포함

### 파일 위생
- 불필요한 파일 발견 시 **적극적으로 .gitignore에 추가**
- 무시해야 할 일반적인 파일:
  - Unity: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`
  - 빌드 산출물: `ait-build/`, `webgl/`, `dist/`, `node_modules/`
  - 로그: `*.log`, `*.log.meta`
  - IDE: `.idea/`, `.vscode/`, `*.swp`
- 커밋하면 안 되는 추적되지 않은 파일 발견 시 적절한 `.gitignore` 파일에 추가

## 자주 하는 실수 방지

### SDK 생성기 관련
- `Runtime/SDK/` 파일을 직접 수정하면 다음 `pnpm generate` 시 덮어씌워짐
- SDK API 변경이 필요하면 반드시 `sdk-runtime-generator~/` 내 코드를 수정할 것
- 생성기 수정 후 검증 순서: `pnpm generate` → `pnpm validate` → `pnpm test`

### pnpm 버전 핀 동기화

`Editor/AITPackageManagerHelper.cs`의 `PNPM_VERSION` 상수와 다음 세 파일의 `"packageManager"` 필드는 **항상 같은 버전**으로 유지해야 한다. 한 곳을 bump하면 나머지도 함께 bump:

- `package.json` (UPM 매니페스트)
- `sdk-runtime-generator~/package.json`
- `WebGLTemplates/AITTemplate/BuildConfig~/package.json`

값이 갈라지면 클라이언트 SDK가 사용하는 pnpm과 lockfile을 갱신한 pnpm이 달라져 미세한 specifier drift가 발생할 수 있다.

### GitHub Actions 관련
- `gh workflow run` 명령어 사용 불가 (GraphQL 차단) — REST API 사용 필수
- 워크플로우 트리거 예시와 ID 참조 테이블은 `docs/claude/github-actions.md` 참조
- PR 번호 사용 시 `target_ref`에 숫자만 입력 (# 접두사 불필요)

### E2E 테스트 실패 대응
- E2E 실패 시 먼저 **코드 변경과 무관한 인프라 이슈인지** 판별
- **인프라 기인 실패 시**: `rerun-failed-jobs`로 실패한 job만 재실행 (전체 재실행보다 성공률 높음 — self-hosted runner 리소스 경합 감소)
  ```bash
  gh api repos/{owner}/{repo}/actions/runs/{run_id}/rerun-failed-jobs -X POST
  ```
- **알려진 flaky 패턴** (대부분 인프라 기인, 코드 변경 없이 재실행으로 해결 — Unity 라이선스 결함은 예외, 아래 참조):
  - **Unity 라이선스 충돌** — `Code 8 (또는 Code 10) while verifying Licensing Client signature` / `No ULF license found` / `Token not found in cache` / handshake / IPC 에러 (exit code 42). self-hosted runner는 현재 `unity-<version>` 라벨로 1:1 핀 고정되어 차단 중. 재발 시 라벨이 빠진 머신이 있는지 확인 (`docs/claude/github-actions.md`).
    - ⚠️ **단, 라벨 핀된 단일 러너(예: `macos-1-1`=2021.3)의 라이선스가 실제로 깨진 경우는 transient가 아니다** — `rerun-failed-jobs`를 반복해도 인프라/사람이 라이선스를 고치기 전까지 매번 동일 시그니처로 실패한다. (2026-06 베타 deploy probe에서 attempt #1~#3이 모두 macos-1-1의 동일 라이선스 시그니처로 실패했고, **시간 경과가 아니라 수동 라이선스 수복 후에야** attempt #4가 통과했다 — 자연 복구 아님.) 동일 라이선스 시그니처가 **2회 연속**이면 transient로 보지 말고 러너 라이선스 수복 필요로 **에스컬레이션**(반복 rerun으로 시간 낭비 금지).
  - **Windows artifact upload finalize transient** — `actions/upload-artifact@v7`가 `successfully finalized` 메시지 없이 종료 (~1.3% 빈도). 진단 step + `continue-on-error`가 적용되어 있고 재실행으로 해결됨
  - **Unity WebGL Brotli/Gzip 크래시** — `[BUSY Ns] Brotli webgl/Build/...unityweb` 직후 `exit code: 1`. self-hosted runner 동시 빌드 시 리소스 경합. **현재 E2E CI는 압축 비활성화(`AIT_COMPRESSION_FORMAT="0"`)** 로 압축 단계 자체를 건너뛰므로 신규 발생 없음 (E2E는 vite preview에서만 로드되며 배포되지 않아 압축 불필요). 로컬 재현은 아래 "로컬 CI 재현" 가이드 참조
  - **E2E warm-reload `unityInstance` 120s 타임아웃** — `Tests~/E2E/tests/e2e-full-pipeline.test.js`의 test 3-1(`3-1. Page reload should not crash (cache warm)`)에서 `page.waitForFunction(() => window['unityInstance'] !== undefined, {timeout:120000})`(line ~795)가 120s 예산을 초과. reload 자체는 200 성공(line 793 통과) 후 **Unity WASM의 warm 재초기화가 CI 부하 편차로 예산 내 미완료** → `unityInstance` 미설정이 원인. E2E TEST 잡은 **격리된 GitHub-hosted `ubuntu-latest`** 에서 실행되어 self-hosted 경합과 무관. **비결정적**: 동일 코드가 실행마다 랜덤하게 다른 macOS leg에서 실패(2022.3↔2021.3 이동)하므로 특정 커밋/버전의 결정적 회귀가 아님 (2026-07 #929 검증 중 확인 — run 28743857273=2022.3 실패, run 28802654528=2021.3 실패, 89e353f/run 28802966012=5/5 통과). **처리**: E2E Tests는 non-required라 머지 차단 안 함 — 단일 leg 랜덤 실패면 transient로 보고 `rerun-failed-jobs`로 실패 leg만 재실행.
    - ⚠️ **비인과적 red herring 주의**: 같은 3-1 창에 찍히는 vite `Pre-transform error: Failed to load /unity-bridge.ts`·`/src/main.ts`(404), `net::ERR_CONNECTION_CLOSED`, `wasm streaming compile failed`, 다수의 `AppsInToss 존재: false` 폴링, `createUnityInstance` 사이클은 **통과 leg에도 카운트가 동일**하므로 원인이 아니다. 로그 끝의 `vite preview ... SIGKILL (Forced termination)`은 타임아웃 후 Playwright teardown의 정리 동작(원인이 아니라 결과). 실제 차이는 `unityInstance set/ready` 마커뿐(통과 leg 9회 / 실패 leg 0회).
- **Sentry 노이즈 패턴 추가**는 자동화(`auto-resolve`)가 처리하므로 수동 PR 불필요 — `Editor/ErrorTracker/AITEditorErrorTracker.cs`의 `NonSdkMessagePatterns`에 자동 흡수됨
- **Sentry 자동 resolve는 "머지 시점"이 아니라 "다음 릴리즈 시점"에 발생 (release-gated)** — auto-resolve PR body의 `Fixes APPS-IN-TOSS-UNITY-SDK-XX` 키워드는 squash 머지 커밋에 그대로 들어가지만, 이슈가 실제로 closed 되는 건 **다음 `apps-in-toss.unity@X.Y.Z` 릴리즈가 cut될 때**다. `release.yml`의 `sentry-release` job(`getsentry/action-release@v3`, `set_commits: auto`, PR #657)이 릴리즈에 커밋 범위를 연결하고, 그 커밋의 `Fixes` trailer가 참조한 이슈를 Sentry가 resolve한다. 따라서 **머지 직후에는 대상 이슈가 여전히 `unresolved`로 보이는 게 정상**이다 (버그 아님, 2026-06 PR #703에서 확인).
  - **즉시 닫아야 할 때만 수동 resolve** (릴리즈를 기다리지 않거나, 아래 잔여 이벤트처럼 미래 릴리즈 커밋 범위에 안 잡히는 경우):
    1. 상태 확인 — Sentry MCP `get_sentry_resource(resourceType='issue', organizationSlug='toss', resourceId='APPS-IN-TOSS-UNITY-SDK-XX')` 또는 `search_issues`
    2. `unresolved`이면 직접 resolve — Sentry MCP `update_issue(organizationSlug='toss', issueId='APPS-IN-TOSS-UNITY-SDK-XX', status='resolved', reason='노이즈 패턴 PR #N 머지로 현행 SDK가 드롭 — 릴리즈 전 즉시 resolve')`
  - **이미 현행 main에 커버된 잔여 이벤트**(구버전 SDK에서 유입, 코드/PR 변경 없음)는 미래 릴리즈 커밋 범위에 `Fixes`가 없어 자동 resolve되지 않으므로 **항상 수동 resolve**로 정리한다.

### 테스트 관련
- E2E 테스트 전 빌드 필요: `./run-local-tests.sh --all` (빌드+테스트) vs `--e2e` (테스트만)
- Level 0 테스트(EditMode)는 빌드 없이 ~10초만에 실행 가능
- 상세 테스트 구조는 `docs/claude/testing.md` 참조

### Sentry 이슈 관련
- EditMode 통합 테스트가 의도적으로 발생시키는 무시 가능한 이슈들이 있음 (SDK-2, SDK-3, SDK-8, SDK-9, SDK-A, SDK-B, SDK-C) — 이미 Sentry에서 ignored 처리됨
- 상세 목록 및 통합 테스트 environment 분리 절차는 `docs/claude/sentry-known-issues.md` 참조

### Push 직전 검증 체크리스트

SDK 생성기 작업을 포함하는 변경사항을 커밋/푸시하기 전, **순서대로** 확인:

1. **생성물 상태 확인**: `sdk-runtime-generator~/` 또는 `Runtime/SDK/` 근처를 수정했다면 먼저
   ```bash
   cd sdk-runtime-generator~ && pnpm generate && cd ..
   git status --short
   ```
   `Runtime/SDK/` 하위에 예상치 못한 변경이 보이면 생성기 수정 의도와 맞는지 확인. 의도하지 않은 산출물은 커밋 전 조치.
2. **.gitignore 적용 확인**: `webgl/`, `ait-build/dist/`, `ait-build/node_modules/`, `Library/`, `Temp/`, `*.log` 등이 `git status`에 나타나면 `.gitignore`에 빠진 항목이 있는지 검토 (기존 `.gitignore` 참조).
3. **로컬 검증 실행**: 빠른 경로는 `./run-local-tests.sh --validate` (~30초) — 파일 구조 + SDK 유닛 테스트. 생성기 변경에는 `--editmode`(~10초)도 병행 권장.
4. **스테이지 최종 리뷰**: `git diff --cached`로 정확히 어떤 파일이 커밋되는지 한 번 더 확인. 특히 다음을 체크:
   - 의도하지 않은 `Runtime/SDK/` 재생성 산출물 포함 여부
   - `.meta` 파일 누락/추가 여부 (Lint 워크플로우에서 검출)
   - 대용량 바이너리/빌드 산출물 혼입 여부

### 로컬 CI 재현 (압축 포맷 / 리소스 경합)

E2E CI는 현재 압축이 **Disabled**(`AIT_COMPRESSION_FORMAT="0"`)이므로 신규 빌드에서 Brotli/Gzip 크래시는 발생하지 않음. 다만 이전 빌드 분석이나 압축별 동작 검증이 필요하면 `--compression`과 `--parallel`을 조합:

```bash
# E2E CI와 동일한 경로(압축 비활성화)
./run-local-tests.sh --unity-build --compression disabled --unity-version 6000.2

# Gzip / Brotli 강제 (압축 단계 flaky 재현용)
./run-local-tests.sh --unity-build --compression gzip --unity-version 6000.2
./run-local-tests.sh --unity-build --compression brotli --unity-version 6000.2

# 동시 빌드로 리소스 경합 재현 (모든 버전 병렬)
./run-local-tests.sh --unity-build --parallel --compression brotli
```

**압축 포맷 값**: `auto` | `disabled` | `gzip` | `brotli` (`run-local-tests.sh` 참조).
**주의**: self-hosted runner의 리소스 경합 자체(CPU/메모리/디스크 경합)는 로컬 머신 스펙에 따라 재현이 보장되지 않음. 로컬에서 통과해도 CI flaky가 재현되지 않을 수 있으며, 이 경우 `rerun-failed-jobs` 경로를 사용 (위 "E2E 테스트 실패 대응" 참조).

### Library/Bee 캐시 동작

CI Unity 빌드의 `Library/Bee` 캐시 무효화 정책:
- **SDK/asmdef/jslib 변경 있음** → `Library/Bee` 삭제 (full rebuild — stale ref.dll 차단)
- **변경 없음** → 캐시 보존 (incremental rebuild로 빌드 시간 단축)
- **fallback** (`git diff` 실패, 얕은 fetch 등) → 보수적으로 Bee 삭제
- **escape hatch**: workflow_dispatch에서 `clean_library=true`로 강제 풀 클린 가능 (`docs/claude/github-actions.md` 참조)

캐시가 의심되는 빌드 실패가 있으면 먼저 `clean_library=true`로 재트리거 후 재현 여부 확인.

## 빠른 참조: 주요 명령어

| 작업 | 명령어 |
|------|--------|
| SDK 재생성 | `cd sdk-runtime-generator~ && pnpm generate` |
| SDK 검증 | `cd sdk-runtime-generator~ && pnpm validate` |
| SDK 테스트 | `cd sdk-runtime-generator~ && pnpm test` |
| 전체 로컬 테스트 | `./run-local-tests.sh --all` |
| 빠른 검증만 | `./run-local-tests.sh --validate` |
| E2E만 | `./run-local-tests.sh --e2e` |
| CI 트리거 (E2E) | `docs/claude/github-actions.md` 참조 |

## Verification Commands

review-fix-loop 등 자동화 skill이 파싱하는 규약 섹션. 각 항목은 실제 명령 또는 `none` 리터럴.

- **Typecheck**: `./run-local-tests.sh --validate`
- **Test**: none
- **Lint**: none

`--validate`(~30초)는 파일 구조 검증 + Playwright 설정 + SDK 유닛 테스트(vitest invariants 포함)를 묶어서 실행하므로 별도 Test 항목을 두지 않는다. Unity E2E(`--all`)는 비용이 크고 매 패스 실행에 부적합해 제외한다 — 변경이 E2E에 영향을 주면 `./run-local-tests.sh --e2e`를 수동 호출한다. Lint(`.meta` 체크, 포맷 등)는 GitHub Actions `lint` 워크플로우가 담당한다.

## 상세 문서

필요할 때 Read로 가져가서 참조:

- `docs/claude/github-actions.md` — 워크플로우 ID 전체 테이블, 트리거 예시 (E2E/Preview/Release/SDK Update/Bulk Release), 상태 확인 명령
- `docs/claude/project-structure.md` — 전체 디렉토리 트리 (Runtime/SDK partial class 목록 포함)
- `docs/claude/build-pipeline.md` — 2단계 빌드 시스템 (Unity WebGL → Granite), 플레이스홀더 치환 규칙
- `docs/claude/testing.md` — 3-Level 테스트 구조, Unity 버전 요구사항, E2E 디렉토리 구조, 실행 명령, CI/CD 통합
- `docs/claude/sdk-generator.md` — `sdk-runtime-generator~` 워크플로우, 타입 매핑, API 사용 패턴
- `docs/claude/implementation-details.md` — WebGL 템플릿, 빌드 설정 자동 구성, 내장 Node.js 시스템, 설정 저장소
- `docs/claude/sentry-known-issues.md` — 무시 가능 Sentry 이슈 목록, 통합 테스트 environment 분리, strict 게이트 + NonSdkMessagePatterns 이중 안전망
- `docs/claude/build-session-recovery.md` — 빌드 중 도메인 리로드 복원력 (`AITBuildSessionRecovery`) 수동 재현 절차 (`.cs` 저장 / Unity 강제 종료 / Stale 세션 / Idle gate)
