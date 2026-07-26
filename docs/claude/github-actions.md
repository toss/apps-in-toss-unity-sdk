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
