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

대부분 인프라 기인이라 코드 변경 없이 `rerun-failed-jobs` 재실행으로 해결됩니다. Unity 라이선스 결함은 예외입니다. 빌드 로그의 인프라 시그니처 목록은 E2E 매트릭스 실패 분류기 `classify_infra()`(`.github/scripts/e2e/classify-matrix-failures.sh`)에 있습니다.

- **Unity 라이선스 충돌** — `Code 8 (또는 Code 10) while verifying Licensing Client signature` / `No ULF license found` / `Token not found in cache` / handshake·IPC 에러(exit code 42). self-hosted 러너는 `unity-<version>` 라벨로 1:1 핀 고정돼 있습니다. 재발하면 라벨이 빠진 머신이 있는지 확인합니다. 같은 시그니처가 2회 연속이면 러너의 라이선스가 실제로 깨진 것이므로 rerun을 반복하지 말고 라이선스 수복을 에스컬레이션합니다.
- **Windows artifact upload finalize transient** — `actions/upload-artifact`가 `successfully finalized` 없이 끝납니다(~1.3%). 재실행으로 해결됩니다.
- **Unity WebGL Brotli/Gzip 크래시** — `Brotli webgl/Build/...unityweb` 직후 `exit code: 1`. self-hosted 러너의 동시 빌드 경합입니다. E2E CI는 압축을 끄고(`AIT_COMPRESSION_FORMAT="0"`) 돌아 새로 발생하지 않습니다. 로컬 재현은 [테스트 전략](testing.md)의 "로컬 CI 재현"을 참조하세요.
- **E2E warm-reload `unityInstance` 타임아웃(3-1)** — 리로드 후 `window['unityInstance']`가 `UNITY_WAIT_BUDGET_MS`(25초) 안에 설정되지 않습니다. 시도는 최대 3회(`maxAttempts`)입니다. 재시도 루프는 `Tests~/E2E/tests/lib/reload-retry.js`의 `reloadWithRetry()`이고, 3-1은 `POLICY_WARM_CACHE` 정책을 씁니다. 9-x가 쓰는 `reloadAndWaitForUnity()`는 `POLICY_ISOLATED_PAGE`라 값이 다릅니다. 예산이 75초씩 3회이고 `Failed to download file`도 재시도 대상으로 봅니다.
  - 로그 검색어: `unityInstance not set within 25s budget`, `harness connection-drop classified`
  - 판정 순서(`reloadWithRetry()`의 catch 분기, 3-1 기준):
    1. `CRASH_RE`가 맞으면 즉시 실패합니다. 로그는 `genuine crash signature detected`.
    2. `productHangRe`(`Failed to download file`)가 있고 net 에러가 없으면 즉시 실패합니다. 로그는 `product hang signature detected`.
    3. 페이지가 닫혔으면(`has been closed`/`Target closed`) 즉시 실패합니다. 로그는 `page/context closed`.
    4. `hadHarnessDrop()`이 참이고 시도가 남았으면 재시도합니다. 로그는 `harness connection-drop classified`.
  - `hadHarnessDrop()`이 보는 net 에러(`POLICY_WARM_CACHE.harnessRe`)는 `ERR_CONNECTION_CLOSED`/`ERR_CONNECTION_RESET`/`ERR_EMPTY_RESPONSE`/`ERR_INCOMPLETE_CHUNKED_ENCODING`입니다. net 에러가 `Failed to download file`과 함께 찍히면 순단이 원인이므로 4번으로 갑니다. 1번과 2번은 flaky가 아니라 제품 결함 신호입니다. 재시도 로그가 보이면 크래시가 아닙니다. reload 자체는 매 시도 200입니다.
  - 처리: 비결정적이라 실행마다 걸리는 leg가 다르고 두 leg가 함께 걸리기도 합니다. E2E Tests는 non-required이므로 실패한 leg만 재실행합니다. 같은 시그니처가 2개 이상 leg에서 반복되면 별건 조사로 올립니다. `webgl.data` 스트림이 끊기는 원인은 아직 모릅니다.
  - 버전 bump를 의심하기 전에 bump 이전 run에 같은 시그니처가 있는지 봅니다. playwright 회귀라면 `strict mode violation`, `Executable doesn't exist`, `browserType.launch` 실패가 나옵니다.
  - red herring: `Pre-transform error: Failed to load /unity-bridge.ts`·`/src/main.ts`(404), `net::ERR_CONNECTION_CLOSED`, `wasm streaming compile failed`, `AppsInToss 존재: false` 폴링, `createUnityInstance` 사이클은 통과한 leg에도 똑같이 찍힙니다. 로그 끝의 `vite preview ... SIGKILL (Forced termination)`은 타임아웃 뒤 teardown의 결과입니다. 통과와 실패를 가르는 마커는 `unityInstance set/ready`뿐입니다.
- **E2E 9-2 `indexedDB.databases()` 프로브 hang** — #1168로 해결됐습니다. 420초 테스트 타임아웃까지 멈춘 뒤 아래 시그니처로 실패하던 문제입니다.
  - 시그니처: `[9-2] indexedDB.databases() verification failed/hung`, `[9-2] reload attempt 1/3 FAILED ... Target page, context or browser has been closed`, `Test timeout of 420000ms exceeded`
  - 원인: `Storage.clearDataForOrigin` 뒤의 `indexedDB.databases()` 확인에 데드라인이 없었고, IDBFS가 커넥션을 열어 둔 탓에 응답하지 않았습니다. `has been closed`는 teardown의 결과이고 크래시가 아닙니다. 지금은 프로브에 10초 데드라인이 있어 응답이 없으면 검증만 건너뜁니다.
  - 브라우저 판별: `playwright.config.ts`가 `channel: 'chrome'`이라 러너 이미지가 바뀌면 브라우저도 바뀝니다. 이미지 버전은 잡 로그 `Runner Image` 블록의 `Version:`에서 읽습니다. 바로 위 `Runner Image Provisioner` 블록의 `Version:`은 다른 값입니다. `Log system Chrome version` 스텝에도 버전이 찍힙니다.
  - 정상 로그: `IDBFS 열린 커넥션 wedge로 보고 검증 skip`, 그리고 afterAll 시점에 leg당 1회 찍히는 `[9-x] mockPage CLOSED`.
  - 재발 판정: 9-2의 `Test timeout of 420000ms exceeded`가 다시 나오거나 9-2~9-11 사이에 `mockPage CLOSED`/`mockPage CRASHED`가 찍히면 rerun하지 말고 별건 조사로 올립니다.
  - 교훈: `page.evaluate`에는 자체 타임아웃이 없으므로 부가 프로브에는 데드라인을 겁니다.

## Library/Bee 캐시 무효화 정책

CI Unity 빌드의 `Library/Bee` 캐시 무효화 정책은 다음과 같습니다.

- **SDK/asmdef/jslib 변경 있음** → `Library/Bee` 삭제 (full rebuild — stale ref.dll 차단)
- **변경 없음** → 캐시 보존 (incremental rebuild로 빌드 시간 단축)
- **fallback** (`git diff` 실패, 얕은 fetch 등) → 보수적으로 Bee 삭제
- **escape hatch** — workflow_dispatch에서 `clean_library=true`로 강제 풀 클린 (위 트리거 예시의 `clean_library` 참조)

캐시가 의심되는 빌드 실패는 먼저 `clean_library=true`로 재트리거해 재현 여부를 확인합니다.

## CI 스크립트

워크플로 인라인 스크립트가 커지면 `.github/scripts/<영역>/`으로 뽑아냅니다. 숨김 디렉터리라 `.meta`가 필요 없고, `rm -rf .github/`로 릴리즈 배포물에서 빠집니다(선례: `generate-benchmark-report.js`, `generate-perf-report.js`).

`lint.yml`의 `ci-scripts` 잡이 아래를 검사합니다.

- actionlint(버전 고정, `-shellcheck=`)로 워크플로 YAML 전체를 검사합니다. `release.yml`의 `SENTRY_AUTH_TOKEN` workflow_call secrets 오탐(`bulk-release`가 `secrets: inherit`으로 호출해 런타임에는 전달됨)은 이 잡에서 명시적으로 `-ignore`합니다.
- `.github/scripts/**/*.sh`는 `bash -n`과 `shellcheck -S error`(ubuntu-latest 기본 탑재, 별도 설치 없음)를 통과해야 합니다.
- `.github/scripts/**/*.ps1`은 ASCII 전용이어야 합니다. PowerShell 5.1이 BOM 없는 UTF-8을 CP949로 오독하기 때문입니다. 한국어 설명은 스크립트 안이 아니라 그 스크립트를 호출하는 YAML 스텝의 이름·주석에 둡니다.
- `.github/scripts/**/*.{js,cjs}`는 `node --check`를 통과해야 합니다.

actionlint는 내장 shellcheck를 끄고 돌립니다. 그래서 워크플로 YAML 안에 남아 있는 인라인 `run:` 블록은 shellcheck 검사를 받지 않고, `.github/scripts/`로 뽑아낸 스크립트만 검사 대상이 됩니다.

`.ci`는 추출한 스크립트를 부르는 워크플로가 스크립트 전용으로 쓰는 두 번째 체크아웃 경로입니다(`.gitignore`의 `/.ci/`). 지금은 preview.yml의 deploy 잡이 이 체크아웃으로 `.github/scripts/deploy/extract-deploy-url.sh`를 부릅니다. 기본 체크아웃은 `target_ref`나 포크를 가리킬 수 있어 워크플로 YAML과 스크립트가 다른 커밋을 볼 수 있지만, `.ci` 체크아웃은 ref를 지정하지 않아 워크플로 YAML과 항상 같은 커밋의 스크립트를 읽습니다.

셸 옵션은 원래 스텝의 실행 방식과 맞춥니다. 기본 셸 스텝(`shell:` 미지정)은 `bash -e {0}`이라 스크립트 헤더에 `set -e`만 두고, `shell: bash`를 명시한 스텝은 `-eo pipefail`이 붙으므로 `set -eo pipefail`을 씁니다. `.ps1`은 같은 세션에서 `& "<path>"`로 호출해 러너가 앞뒤로 붙이는 `$ErrorActionPreference`/`LASTEXITCODE` 처리를 그대로 상속합니다.

워크플로 변경을 REST dispatch로 검증할 때는 `ref`를 브랜치로 줍니다. `ref: main` + `target_ref`는 main에 있는 워크플로 YAML을 실행하므로 브랜치의 변경 사항을 검증하지 못합니다.

## 알아둘 점

- **PR 번호 사용 권장** — `target_ref`에 PR 번호를 넣으면 결과가 PR 코멘트로 자동 게시됩니다.
- **concurrency 그룹** — 같은 PR에 대해 동시 실행하면 이전 실행이 취소될 수 있습니다.
- **러너 라벨** — self-hosted 러너는 `unity-<version>` 라벨로 1:1 핀되어 있습니다. 라벨이 빠진 머신이 생기면 Unity 라이선스 충돌이 재발합니다.

## 관련 문서

- [테스트 전략](testing.md) — E2E 레벨 구조와 러너 라우팅
- [Sentry 알려진 이슈](sentry-known-issues.md) — CI가 만들어 내는 노이즈 이벤트
- [기여 가이드](../Contributing.md) — 푸시 전 로컬 검증
