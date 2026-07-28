# GitHub Actions 워크플로우

## 워크플로우 목록

| 워크플로우 | 트리거 | 용도 |
|-----------|--------|------|
| **E2E Tests** | 수동, workflow_call | Unity WebGL 빌드 + Playwright E2E 테스트 |
| **Preview** | 수동 | PR 브랜치를 빌드하여 미리보기 배포 |
| **Validate** | push, PR | SDK Generator 유닛 테스트, Unity .meta 파일 검사 |
| **Lint** | push, PR | Unity .meta 파일 누락 검사 |
| **Release** | 수동, push(main), workflow_call | npm 패키지 릴리즈 |
| **SDK Update** | 수동, 스케줄(평일 9시) | @apps-in-toss/web-framework 버전 동기화 |
| **Bulk Release** | 수동 | 여러 버전 일괄 릴리즈 |
| **Unity Build** | workflow_call 전용 | 다른 워크플로우에서 호출하는 빌드 모듈 |
| **Update API Changelog** | push(main), 수동 | API 변경 이력 자동 갱신 |
| **SDK Update Auto Rebase** | push(main), 수동 | update/ PR 충돌 자동 rebase |

## 워크플로우 트리거 방법

**⚠️ 중요: gh CLI의 GraphQL API가 차단되어 있으므로 REST API 사용 필수**

### E2E Tests
```bash
# PR 번호로 트리거 (권장 - PR 코멘트에 결과 자동 게시)
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123"
  }
}
EOF

# 브랜치로 트리거
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<EOF
{
  "ref": "feature-branch"
}
EOF

# Library 캐시 정리 옵션
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "clean_library": "true"
  }
}
EOF
```

### Preview
```bash
# 단일 타겟
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216269700/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "targets": "macos-6000.2"
  }
}
EOF

# ✅ 여러 타겟 동시 빌드 (쉼표로 구분)
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216269700/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "targets": "macos-6000.3,macos-6000.2,macos-6000.0,macos-2022.3,macos-2021.3"
  }
}
EOF
```

**지원 타겟 형식**: `{os}-{unity-version}` (예: `macos-6000.2`, `windows-2021.3`)

### Release
```bash
# 특정 버전 릴리즈
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934317/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0"
  }
}
EOF
```

### SDK Update
```bash
# 특정 버전으로 업데이트
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0"
  }
}
EOF

# 누락된 모든 버전 자동 감지 및 업데이트
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main"
}
EOF

# 강제 업데이트 (이미 같은 버전이 있어도)
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0",
    "force": "true"
  }
}
EOF
```

### Bulk Release
```bash
# 특정 버전들 일괄 릴리즈
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/222574658/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main",
  "inputs": {
    "versions": "1.5.0,1.6.0,1.7.0",
    "max_parallel": "2"
  }
}
EOF

# 모든 release/v* 태그 대상 릴리즈
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/222574658/dispatches \
  -X POST --input - <<EOF
{
  "ref": "main"
}
EOF
```

### Validate / Lint
자동 트리거 (push, PR)만 지원. 수동 트리거 불가.

## 워크플로우 상태 확인

```bash
# 최근 실행 목록
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs \
  --jq '.workflow_runs[:10] | .[] | "\(.id) \(.name) \(.status) \(.conclusion)"'

# 특정 실행 상태 확인
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs/RUN_ID \
  --jq '"\(.name): \(.status) / \(.conclusion)"'

# 실행 중인 워크플로우만
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs \
  --jq '.workflow_runs[] | select(.status == "in_progress" or .status == "queued") | "\(.id) \(.name) \(.status)"'
```

## 워크플로우 ID 참조

| 워크플로우 | ID |
|-----------|-----|
| E2E Tests | 216286654 |
| Preview | 216269700 |
| Release | 214934317 |
| SDK Update | 214934319 |
| Bulk Release | 222574658 |
| Unity Build | 216269701 |
| Validate | 216278800 |
| Lint | 214934316 |
| Update API Changelog | 238481894 |
| SDK Update Auto Rebase | 256455113 |

## 주의사항

1. **PR 번호 사용 권장**: `target_ref`에 PR 번호를 사용하면 결과가 PR 코멘트로 자동 게시됨
2. **Preview 다중 타겟**: 여러 Unity 버전을 빌드할 때 쉼표로 구분하여 한 번에 트리거 (N번 호출 금지)
3. **concurrency 그룹**: 같은 PR에 대해 동시 실행 시 이전 실행이 취소될 수 있음
4. **GraphQL 차단**: `gh workflow run` 명령 사용 불가, REST API 사용 필수

## E2E 알려진 flaky 패턴 (상세)

대부분 인프라 기인으로, 코드 변경 없이 `rerun-failed-jobs` 재실행으로 해결 — Unity 라이선스 결함은 예외(아래 참조).

- **Unity 라이선스 충돌** — `Code 8 (또는 Code 10) while verifying Licensing Client signature` / `No ULF license found` / `Token not found in cache` / handshake / IPC 에러 (exit code 42). self-hosted runner는 현재 `unity-<version>` 라벨로 1:1 핀 고정되어 차단 중. 재발 시 라벨이 빠진 머신이 있는지 확인 (위 "워크플로우 ID 참조" 및 runner 라우팅은 `docs/claude/testing.md` 참조).
  - ⚠️ **단, 라벨 핀된 단일 러너(예: `macos-1-1`=2021.3)의 라이선스가 실제로 깨진 경우는 transient가 아니다** — `rerun-failed-jobs`를 반복해도 인프라/사람이 라이선스를 고치기 전까지 매번 동일 시그니처로 실패한다. (2026-06 베타 deploy probe에서 attempt #1~#3이 모두 macos-1-1의 동일 라이선스 시그니처로 실패했고, **시간 경과가 아니라 수동 라이선스 수복 후에야** attempt #4가 통과했다 — 자연 복구 아님.) 동일 라이선스 시그니처가 **2회 연속**이면 transient로 보지 말고 러너 라이선스 수복 필요로 **에스컬레이션**(반복 rerun으로 시간 낭비 금지).
- **Windows artifact upload finalize transient** — `actions/upload-artifact@v7`가 `successfully finalized` 메시지 없이 종료 (~1.3% 빈도). 진단 step + `continue-on-error`가 적용되어 있고 재실행으로 해결됨
- **Unity WebGL Brotli/Gzip 크래시** — `[BUSY Ns] Brotli webgl/Build/...unityweb` 직후 `exit code: 1`. self-hosted runner 동시 빌드 시 리소스 경합. **현재 E2E CI는 압축 비활성화(`AIT_COMPRESSION_FORMAT="0"`)** 로 압축 단계 자체를 건너뛰므로 신규 발생 없음 (E2E는 vite preview에서만 로드되며 배포되지 않아 압축 불필요). 로컬 재현은 `docs/claude/testing.md`의 "로컬 CI 재현" 참조
- **E2E warm-reload `unityInstance` 120s 타임아웃** — `Tests~/E2E/tests/e2e-full-pipeline.test.js`의 test 3-1(`3-1. Page reload should not crash (cache warm)`)에서 `page.waitForFunction(() => window['unityInstance'] !== undefined, {timeout:120000})`(line ~795)가 120s 예산을 초과. reload 자체는 200 성공(line 793 통과) 후 **Unity WASM의 warm 재초기화가 CI 부하 편차로 예산 내 미완료** → `unityInstance` 미설정이 원인. E2E TEST 잡은 **격리된 GitHub-hosted `ubuntu-latest`** 에서 실행되어 self-hosted 경합과 무관. **비결정적**: 동일 코드가 실행마다 랜덤하게 다른 macOS leg에서 실패(2022.3↔2021.3 이동)하므로 특정 커밋/버전의 결정적 회귀가 아님 (2026-07 #929 검증 중 확인 — run 28743857273=2022.3 실패, run 28802654528=2021.3 실패, 89e353f/run 28802966012=5/5 통과). **처리**: E2E Tests는 non-required라 머지 차단 안 함 — 단일 leg 랜덤 실패면 transient로 보고 `rerun-failed-jobs`로 실패 leg만 재실행.
  - ⚠️ **비인과적 red herring 주의**: 같은 3-1 창에 찍히는 vite `Pre-transform error: Failed to load /unity-bridge.ts`·`/src/main.ts`(404), `net::ERR_CONNECTION_CLOSED`, `wasm streaming compile failed`, 다수의 `AppsInToss 존재: false` 폴링, `createUnityInstance` 사이클은 **통과 leg에도 카운트가 동일**하므로 원인이 아니다. 로그 끝의 `vite preview ... SIGKILL (Forced termination)`은 타임아웃 후 Playwright teardown의 정리 동작(원인이 아니라 결과). 실제 차이는 `unityInstance set/ready` 마커뿐(통과 leg 9회 / 실패 leg 0회).

## Library/Bee 캐시 무효화 정책 (상세)

CI Unity 빌드의 `Library/Bee` 캐시 무효화 정책:
- **SDK/asmdef/jslib 변경 있음** → `Library/Bee` 삭제 (full rebuild — stale ref.dll 차단)
- **변경 없음** → 캐시 보존 (incremental rebuild로 빌드 시간 단축)
- **fallback** (`git diff` 실패, 얕은 fetch 등) → 보수적으로 Bee 삭제
- **escape hatch**: workflow_dispatch에서 `clean_library=true`로 강제 풀 클린 가능 (위 "Library 캐시 정리 옵션" 참조)

캐시가 의심되는 빌드 실패가 있으면 먼저 `clean_library=true`로 재트리거 후 재현 여부 확인.
