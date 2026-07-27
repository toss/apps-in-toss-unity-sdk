# 기여 가이드

SDK 자체를 고칠 때 필요한 개발 환경 설정과 규칙입니다.

> **대상**: SDK 기여자. SDK를 사용하기만 한다면 [시작하기](GettingStarted.md)로 가세요.

## 개발 환경 설정

### Git hooks

저장소를 클론한 뒤, 그리고 **새 worktree를 만들 때마다** 한 번 실행하세요.

```bash
./.githooks/setup.sh
```

`core.hooksPath`를 상대 경로 `.githooks`로 지정합니다. 상대 경로라서 worktree마다 자기 자신의 훅을 쓰게 됩니다.

| Hook | 하는 일 |
|------|---------|
| `pre-commit` | `.meta` 파일 누락 검사 |
| `pre-push` | 내부 호스트명·자격증명 URL·사설 프로젝트 식별자가 공개 원격으로 나가기 전에 차단 |

`pre-push`는 파일 내용뿐 아니라 브랜치·태그 이름, 커밋 메시지, 태그 메시지, 파일 경로명까지 검사합니다. 이 표면들은 파일 내용 스캔만으로는 잡히지 않는 유출 경로입니다.

조직별 denylist는 저장소에 넣지 않습니다. 로컬에서 아래처럼 채우세요.

```bash
cp .githooks/patterns.local.example .githooks/patterns.local
```

`patterns.local`은 gitignore되며 한 줄에 하나씩 ERE 패턴을 씁니다. 이 파일이 없으면 저장소에 내장된 일반 패턴만 검사하므로, 내부 도메인이나 사설 코드네임은 걸러지지 않습니다.

> **중요**: git 훅은 클라이언트 측이라 `setup.sh`를 실행해야 동작하고 `--no-verify`로 우회할 수도 있습니다. 권위 있는 백스톱은 CI의 String Check 워크플로입니다. 훅은 노출 순간을 예방하는 계층입니다.

### 필요한 도구

| 도구 | 용도 | 필수 여부 |
|------|------|----------|
| Unity 2021.3 이상 | SDK 개발과 테스트 | 필수 |
| Node.js 18 이상 | SDK Generator | 선택 (내장 Node.js 사용 가능) |
| pnpm | SDK Generator 패키지 관리 | Generator 작업 시 필요 |
| dotnet | CSharpier 포맷팅 | `pnpm format` 사용 시 |

pnpm 버전은 저장소 안에서 핀되어 있고, `Editor/AITPackageManagerHelper.cs`의 `PNPM_VERSION` 상수와 세 곳의 `package.json` `packageManager` 필드가 항상 같은 값이어야 합니다. 한 곳을 올리면 나머지도 함께 올리세요.

## SDK Generator

`Runtime/SDK/`의 C# 코드는 전부 `sdk-runtime-generator~/`가 만들어 냅니다.

> **중요**: `Runtime/SDK/`의 파일을 직접 수정하지 마세요. 다음 생성 때 덮어써집니다. 모든 변경은 생성기 쪽에서 해야 합니다.

```bash
cd sdk-runtime-generator~
pnpm install
pnpm generate          # TypeScript 타입 정의 → C# + JS 브릿지 생성
pnpm format            # CSharpier 포맷팅 (dotnet 필요)
pnpm test              # 전체 유닛 테스트
pnpm test:invariants   # CI가 거는 것과 동일한 불변식 검사
```

생성기를 고쳤다면 `pnpm generate` → `pnpm test:invariants` 순서로 확인하고, `git status`로 `Runtime/SDK/` 아래에 의도한 변경만 나왔는지 보세요. 타입 매핑 규칙 등 생성기 내부 구조는 `internal/sdk-generator.md`에 있습니다.

## 로컬 검증

저장소 루트의 `run-local-tests.sh`가 진입점입니다.

| 명령 | 무엇을 | 대략 소요 |
|------|--------|-----------|
| `./run-local-tests.sh --validate` | 파일 구조 + Playwright 설정 + SDK 유닛 테스트 | 30초 |
| `./run-local-tests.sh --editmode` | Unity EditMode 테스트 | 10초 |
| `./run-local-tests.sh --e2e` | E2E 테스트만 (빌드 결과물 필요) | |
| `./run-local-tests.sh --all` | Unity 빌드까지 포함한 전체 | 오래 걸림 |
| `./run-local-tests.sh --list-unity` | 설치된 Unity 버전 확인 | |

푸시 전 최소 기준은 `--validate`입니다. 생성기를 건드렸다면 `--editmode`도 함께 돌리세요. `--unity-version`, `--compression`, `--parallel`로 특정 조합을 재현할 수 있습니다.

## Unity .meta 파일 규칙

`Editor/`, `Runtime/`, `WebGLTemplates/` 아래의 모든 파일과 폴더에는 `.meta`가 짝으로 있어야 합니다. `pre-commit` 훅과 CI의 lint 워크플로가 둘 다 검사합니다.

`~`로 끝나는 폴더는 예외입니다. Unity가 임포트하지 않으므로 `.meta`가 필요 없습니다 — `Documentation~/`, `Tests~/`, `sdk-runtime-generator~/`가 여기 해당합니다.

`.meta`가 없다면 Unity Editor에서 프로젝트를 한 번 열면 자동으로 생성됩니다. 손으로 만들어야 한다면 기존 파일을 참고하되 GUID는 반드시 새로 만드세요.

```yaml
fileFormatVersion: 2
guid: a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

## 커밋 메시지

모든 커밋 메시지는 **한국어**로 씁니다. 형식은 `<타입>: <설명>`입니다.

| 타입 | 언제 |
|------|------|
| `기능` | 새로운 기능 추가 |
| `수정` | 버그 수정 |
| `개선` | 기존 기능 개선 |
| `리팩토링` | 동작 변경 없는 구조 개선 |
| `문서` | 문서 변경 |
| `테스트` | 테스트 추가·수정 |
| `빌드` | 빌드 설정 변경 |

```text
기능: 사용자 인증 API 추가
수정: WebGL 빌드 오류 해결
개선: 빌드 성능 최적화
문서: 시작하기 가이드 추가
```

아래는 모두 거부됩니다.

```text
feat: Add user authentication API    영어 사용
사용자 인증 API 추가                   타입 누락
기능 사용자 인증 API 추가              콜론 누락
```

## PR

`main`에는 직접 push할 수 없습니다. feature 브랜치를 만들고 PR로 올린 뒤 **squash merge**로 병합합니다. merge commit과 rebase는 서버 측 ruleset이 막습니다. 커밋 서명도 필수입니다.

체크리스트:

- [ ] 커밋 메시지가 한국어이고 `<타입>: <설명>` 형식인가
- [ ] `.meta` 파일이 빠짐없이 포함됐는가
- [ ] `Runtime/SDK/`를 직접 고치지 않았는가
- [ ] `./run-local-tests.sh --validate`가 통과하는가
- [ ] `TODO.md`에서 이 PR이 해결한 항목을 제거했는가

> **주의**: 이 저장소는 public입니다. push한 브랜치·커밋·PR 제목과 본문은 즉시 공개되고, 한 번 push되면 외부 아카이브에 수집되어 사실상 회수할 수 없습니다. 비공개 자원의 이름·존재·URL을 브랜치명이나 커밋 메시지에 포함하지 마세요.

## 관련 문서

- [시작하기](GettingStarted.md) — SDK 사용자 관점의 설치와 설정
- [빌드 파이프라인](BuildProcess.md) — 빌드가 실제로 하는 일
- [문제 해결](Troubleshooting.md) — 빌드가 막혔을 때
