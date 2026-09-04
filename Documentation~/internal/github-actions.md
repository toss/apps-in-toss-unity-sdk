# GitHub Actions 워크플로

어떤 워크플로가 있고 어떻게 트리거하는지에 대한 내부 메모입니다.

> **중요**: 이 환경에서는 github.com에 대해 REST API만 쓸 수 있고 GraphQL은 차단되어 있습니다. `gh workflow run`은 내부적으로 GraphQL을 쓰므로 사용할 수 없습니다. 아래 예시는 전부 REST `dispatches` 엔드포인트를 씁니다.

## 워크플로 목록

| 워크플로 | 트리거 | 용도 |
|----------|--------|------|
| E2E Tests | 수동, `workflow_call` | Unity WebGL 빌드와 Playwright E2E |
| Unity Build | `workflow_call` 전용 | 다른 워크플로가 호출하는 빌드 모듈 |
| Preview | 수동 | 브랜치나 PR을 빌드해 미리보기 배포 |
| Perf | PR 라벨, push(main), 수동 | 콜드 로드 TTFF 측정과 baseline 비교 |
| Validate | push, PR | SDK Generator 유닛 테스트와 불변식 검사 |
| Lint | push, PR | `.meta` 누락과 GUID 위생 검사 |
| String Check | push, PR | 내부 호스트명·자격증명·사설 식별자 유출 스캔 |
| Release | 수동, push(main), `workflow_call` | 버전 결정, SDK 재생성, 빌드 검증, 릴리즈 태그 생성, 배포 |
| Beta Release | 수동 | 파일럿 채널 브랜치 갱신과 prerelease 태그 |
| Bulk Release | 수동 | 여러 버전 일괄 릴리즈 |
| SDK Update | 수동, 스케줄(평일 09시 KST) | `@apps-in-toss/web-framework` 버전 동기화 |
| SDK Update Auto Rebase | push(main), 수동 | `update/` PR 충돌 자동 rebase |
| Update API Changelog | push(main), 수동 | API 변경 이력 갱신 |
| Regenerate Lockfiles | 스케줄(매일), 수동 | pnpm lockfile 재생성 PR |

Validate와 Lint, String Check는 자동 트리거가 주 경로입니다.

## 워크플로 ID

REST `dispatches` 엔드포인트는 워크플로 ID나 파일명을 받습니다. 아래는 스냅샷이고, 권위 있는 목록은 API에서 직접 받습니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows --paginate \
  --jq '.workflows[] | "\(.id)\t\(.name)\t\(.path)"' | sort -k2
```

| 워크플로 | ID |
|----------|-----|
| E2E Tests | 216286654 |
| Unity Build | 216269701 |
| Preview | 216269700 |
| Perf | 291523311 |
| Validate | 216278800 |
| Lint | 214934316 |
| String Check | 275178129 |
| Release | 214934317 |
| Beta Release | 286845872 |
| Bulk Release | 222574658 |
| SDK Update | 214934319 |
| SDK Update Auto Rebase | 256455113 |
| Update API Changelog | 238481894 |
| Regenerate Lockfiles | 274621744 |

## 트리거 예시

### E2E Tests

`target_ref`에 PR 번호를 넣으면 결과가 PR 코멘트로 자동 게시됩니다. `#` 접두사 없이 숫자만 넣습니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "target_ref": "123"
  }
}
EOF
```

브랜치를 직접 대상으로 하려면 `ref`만 지정합니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "feature-branch"
}
EOF
```

`Library/Bee` 캐시가 의심되면 `clean_library`로 강제 풀 클린을 겁니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216286654/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "clean_library": "true"
  }
}
EOF
```

`test_level`로 실행 범위를 줄일 수 있습니다. 값의 의미는 [테스트 전략](testing.md)에 있습니다.

### Preview

타겟 형식은 `<os>-<unity-version>`입니다. 여러 버전을 빌드할 때는 쉼표로 이어 한 번에 트리거합니다. 워크플로를 N번 호출하지 마세요.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/216269700/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "target_ref": "123",
    "targets": "macos-6000.3,macos-6000.2,macos-6000.0,macos-2022.3,macos-2021.3"
  }
}
EOF
```

빌드가 끝나면 deploy 단계가 `intoss-private://` URL을 추출해 QR 이미지를 생성하고 Job Summary에 게시합니다. `target_ref`가 PR로 해석된 경우에만 PR 코멘트에도 게시됩니다(브랜치로 dispatch한 경우는 Job Summary에만 게시). QR을 토스 앱으로 스캔하면 해당 빌드가 실기기에서 열립니다.

### Release

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934317/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0"
  }
}
EOF
```

### SDK Update

`version`을 비우면 누락된 버전을 모두 감지해 처리합니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main"
}
EOF
```

특정 버전을 지정하거나, 같은 버전이 이미 있어도 강제로 돌리려면 입력을 추가합니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/214934319/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "version": "1.6.0",
    "force": "true"
  }
}
EOF
```

### Beta Release

`channel_ref`가 제휴사에게 안내되는 브랜치 이름이고, `source_ref`는 빌드 베이스입니다. `build_strategy`로 검증 빌드 수를 고릅니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/286845872/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "version": "beta",
    "channel_ref": "beta",
    "build_strategy": "standard"
  }
}
EOF
```

### Bulk Release

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/workflows/222574658/dispatches \
  -X POST --input - <<'EOF'
{
  "ref": "main",
  "inputs": {
    "versions": "1.5.0,1.6.0,1.7.0",
    "max_parallel": "2"
  }
}
EOF
```

`inputs`를 비우면 모든 `release/v*` 태그를 대상으로 합니다.

## 상태 확인

```bash
# 최근 실행 10건
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs \
  --jq '.workflow_runs[:10] | .[] | "\(.id) \(.name) \(.status) \(.conclusion)"'

# 특정 실행
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs/RUN_ID \
  --jq '"\(.name): \(.status) / \(.conclusion)"'

# 실행 중인 것만
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs \
  --jq '.workflow_runs[] | select(.status == "in_progress" or .status == "queued") | "\(.id) \(.name) \(.status)"'
```

## 실패한 잡만 재실행

인프라 기인 실패는 전체 재실행보다 실패한 잡만 재실행하는 쪽이 성공률이 높습니다. self-hosted 러너의 리소스 경합이 줄어듭니다.

```bash
gh api repos/toss/apps-in-toss-unity-sdk/actions/runs/RUN_ID/rerun-failed-jobs -X POST
```

> **주의**: 같은 실패 시그니처가 두 번 연속 나오면 transient가 아닙니다. 라벨 핀된 단일 러너의 라이선스가 실제로 깨진 경우 재실행을 반복해도 사람이 고치기 전까지 매번 같은 자리에서 실패합니다. 이때는 반복 재실행 대신 에스컬레이션하세요.

## E2E 알려진 flaky 패턴

대부분 인프라 기인이라 코드 변경 없이 `rerun-failed-jobs` 재실행으로 해결됩니다. Unity 라이선스 결함은 예외입니다(첫 항목 참조).

- **Unity 라이선스 충돌** — `Code 8 (또는 Code 10) while verifying Licensing Client signature` / `No ULF license found` / `Token not found in cache` / handshake / IPC 에러(exit code 42). self-hosted 러너는 현재 `unity-<version>` 라벨로 1:1 핀 고정되어 차단 중입니다. 재발하면 라벨이 빠진 머신이 있는지 확인하세요(러너 라우팅은 [테스트 전략](testing.md) 참조).
  - 단, 라벨 핀된 단일 러너(특정 Unity 버전에 1:1 핀된 머신)의 라이선스가 실제로 깨진 경우는 transient가 아닙니다. `rerun-failed-jobs`를 반복해도 인프라나 사람이 라이선스를 고치기 전까지 매번 동일 시그니처로 실패합니다. 2026-06 베타 deploy probe에서 attempt #1~#3이 모두 해당 러너의 동일 라이선스 시그니처로 실패했고, 시간 경과가 아니라 수동 라이선스 수복 후에야 attempt #4가 통과했습니다(자연 복구 아님). 동일 라이선스 시그니처가 **2회 연속**이면 transient로 보지 말고 러너 라이선스 수복 필요로 에스컬레이션하세요(반복 rerun으로 시간 낭비 금지).
- **Windows artifact upload finalize transient** — `actions/upload-artifact@v7`가 `successfully finalized` 메시지 없이 종료합니다(~1.3% 빈도). 진단 step과 `continue-on-error`가 적용되어 있고 재실행으로 해결됩니다.
- **Unity WebGL Brotli/Gzip 크래시** — `[BUSY Ns] Brotli webgl/Build/...unityweb` 직후 `exit code: 1`. self-hosted 러너 동시 빌드 시 리소스 경합입니다. 현재 E2E CI는 압축 비활성화(`AIT_COMPRESSION_FORMAT="0"`)로 압축 단계 자체를 건너뛰므로 신규 발생이 없습니다(E2E는 vite preview에서만 로드되며 배포되지 않아 압축 불필요). 로컬 재현은 [테스트 전략](testing.md)의 "로컬 CI 재현"을 참조하세요.
- **E2E warm-reload `unityInstance` 타임아웃** — `Tests~/E2E/tests/e2e-full-pipeline.test.js`의 test 3-1(`3-1. Page reload should not crash (cache warm)`)에서 리로드 후 `window['unityInstance']`가 예산 내 설정되지 않습니다. 대기 로직은 `waitForUnityBounded`(정의 L926, 호출 L980)이며 **75초 예산을 최대 3회 재시도**합니다(`maxAttempts = 3`, L871). 예산을 넘기면 L993이 다음 문자열을 던지므로, 로그 검색은 이 문자열로 하세요.

  ```text
  [3-1] attempt 1/3 reload status=200 after 180ms
  [3-1] attempt 1/3 FAILED after 75666ms: unityInstance not set within 75s budget (evalThrows=2)
  [3-1] harness connection-drop classified (server dropped webgl.data stream) — retrying reload
  ```

  reload 자체는 매 시도 200으로 성공합니다. 재시도 사유가 `harness connection-drop classified`(L1011)로 찍히면 원인은 warm 재초기화 지연이 아니라 **서버가 `webgl.data` 스트림 연결을 끊은 것**입니다(판정은 `hadHarnessDrop()`, L903). 진짜 크래시 시그니처(`CRASH_RE`, L869 — `webglcontextlost`/`Aborted(`/`RuntimeError`/`out of bounds`/`memory access`)는 재시도 없이 즉시 hard-fail하므로(L998), 재시도 로그가 보인다면 크래시가 아닙니다. E2E TEST 잡은 격리된 GitHub-hosted `ubuntu-latest`에서 실행되어 self-hosted 경합과 무관합니다.

  **비결정적**입니다 — 실행마다 다른 macOS leg 조합이 걸립니다(run 30412296776=6000.2+6000.3, run 30606499460=6000.3+6000.0). **1개가 아니라 2개 leg이 동시에 걸리는 경우가 있으니 "단일 leg만 flaky"로 가정하지 마세요.** **처리**: E2E Tests는 non-required라 머지를 차단하지 않습니다. `rerun-failed-jobs`로 실패 leg만 재실행하면 통과합니다(run 30606499460은 코드 변경 없이 attempt 2에서 E2E 10개 leg 전부 통과).
  - 버전 bump를 범인으로 지목하기 전에 **대조군부터** 확인하세요. 2026-07 `@playwright/test` 1.61.1 → 1.62.0 직후 이 실패가 났을 때, bump **이전** run 30412296776(로그에 `+ @playwright/test 1.61.1`)에 동일 시그니처(75s×3 예산, connection-drop 분류)가 이미 존재해 회귀 가설이 기각됐습니다. 실패 코드 경로는 playwright API가 아니라 테스트 하네스 자체 워치독이므로, playwright 회귀라면 나올 시그니처(strict mode violation, `Executable doesn't exist`, `browserType.launch` 실패)를 먼저 grep해 0건임을 확인하는 것이 빠릅니다.
  - 근본 원인 미규명 — `webgl.data` 스트림이 왜 끊기는지는 확인되지 않았습니다. 동일 시그니처가 2개 이상 leg에서 **반복** 재현되면 transient로 넘기지 말고 별건 조사로 승격하세요.
  - 비인과적 red herring 주의 — 같은 3-1 창에 찍히는 vite `Pre-transform error: Failed to load /unity-bridge.ts`·`/src/main.ts`(404), `net::ERR_CONNECTION_CLOSED`, `wasm streaming compile failed`, 다수의 `AppsInToss 존재: false` 폴링, `createUnityInstance` 사이클은 통과 leg에도 카운트가 동일하므로 원인이 아닙니다. 로그 끝의 `vite preview ... SIGKILL (Forced termination)`은 타임아웃 후 Playwright teardown의 정리 동작입니다(원인이 아니라 결과). 실제 차이는 `unityInstance set/ready` 마커뿐입니다(통과 leg 9회 / 실패 leg 0회).

## Library/Bee 캐시 무효화 정책

CI Unity 빌드의 `Library/Bee` 캐시 무효화 정책은 다음과 같습니다.

- **SDK/asmdef/jslib 변경 있음** → `Library/Bee` 삭제 (full rebuild — stale ref.dll 차단)
- **변경 없음** → 캐시 보존 (incremental rebuild로 빌드 시간 단축)
- **fallback** (`git diff` 실패, 얕은 fetch 등) → 보수적으로 Bee 삭제
- **escape hatch** — workflow_dispatch에서 `clean_library=true`로 강제 풀 클린 (위 트리거 예시의 `clean_library` 참조)

캐시가 의심되는 빌드 실패는 먼저 `clean_library=true`로 재트리거해 재현 여부를 확인합니다.

## 알아둘 점

- **PR 번호 사용 권장** — `target_ref`에 PR 번호를 넣으면 결과가 PR 코멘트로 자동 게시됩니다.
- **concurrency 그룹** — 같은 PR에 대해 동시 실행하면 이전 실행이 취소될 수 있습니다.
- **러너 라벨** — self-hosted 러너는 `unity-<version>` 라벨로 1:1 핀되어 있습니다. 라벨이 빠진 머신이 생기면 Unity 라이선스 충돌이 재발합니다.

## 관련 문서

- [테스트 전략](testing.md) — E2E 레벨 구조와 러너 라우팅
- [Sentry 알려진 이슈](sentry-known-issues.md) — CI가 만들어 내는 노이즈 이벤트
- [기여 가이드](../Contributing.md) — 푸시 전 로컬 검증
