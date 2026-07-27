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
| Pages | push(main), PR, 수동 | `Documentation~/`를 GitHub Pages로 발행 |

Validate와 Lint, String Check, Pages는 자동 트리거가 주 경로입니다. Pages만 수동 트리거를 함께 받습니다.

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

## 알아둘 점

- **PR 번호 사용 권장** — `target_ref`에 PR 번호를 넣으면 결과가 PR 코멘트로 자동 게시됩니다.
- **concurrency 그룹** — 같은 PR에 대해 동시 실행하면 이전 실행이 취소될 수 있습니다.
- **러너 라벨** — self-hosted 러너는 `unity-<version>` 라벨로 1:1 핀되어 있습니다. 라벨이 빠진 머신이 생기면 Unity 라이선스 충돌이 재발합니다.

## 관련 문서

- [테스트 전략](testing.md) — E2E 레벨 구조와 러너 라우팅
- [Sentry 알려진 이슈](sentry-known-issues.md) — CI가 만들어 내는 노이즈 이벤트
- [기여 가이드](../Contributing.md) — 푸시 전 로컬 검증
