# 테스트 전략

SDK가 무엇을 어떤 층위에서 검증하는지, 그리고 CI가 그것을 어떻게 실행하는지에 대한 내부 메모입니다.

> **대상**: SDK 기여자. 로컬에서 무엇을 돌려야 하는지는 [기여 가이드](../Contributing.md)의 로컬 검증 절에 있습니다.

## 3-Level 구조

| Level | 무엇을 | 어디서 | Unity 필요 |
|-------|--------|--------|-----------|
| 0 | EditMode 테스트 — C# 순수 로직 | self-hosted Unity runner | 필요 |
| 1 | Unity WebGL 빌드 + 산출물 검증 | self-hosted Unity runner | 필요 |
| 2 | Playwright로 실제 브라우저 실행 | GitHub-hosted `ubuntu-latest` | 불필요 |

Level 2는 Level 1이 업로드한 `ait-build` 아티팩트를 내려받아 vite preview로 띄우고 테스트합니다. Unity 바이너리에 의존하지 않으므로 self-hosted 러너의 리소스 경합과 무관합니다.

## Unity 버전

E2E 매트릭스는 아래 5개 버전을 macOS와 Windows 양쪽에서 돌립니다.

| 버전 | 성격 |
|------|------|
| 6000.3.3f1 | Unity 6.3 |
| 6000.2.15f1 | Unity 6 LTS |
| 6000.0.66f2 | Unity 6 |
| 2022.3.62f3 | LTS |
| 2021.3.45f2 | LTS, 최소 지원 버전 |

각 버전의 정확한 패치는 `Tests~/E2E/SampleUnityProject-<버전>/ProjectSettings/ProjectVersion.txt`가 단일 출처입니다. Tuanjie 엔진도 지원 대상이며, SDK는 2021.3 이상 모든 버전에서 컴파일되어야 합니다.

## 디렉터리

```text
Tests~/E2E/
├── SampleUnityProject-<버전>/  버전별 샘플 프로젝트 5개
├── SharedScripts/              샘플 프로젝트가 공유하는 UPM 패키지
│   ├── Runtime/
│   │   ├── InteractiveAPITester.cs   대화형 API 테스터 (WebGL UI)
│   │   ├── RuntimeAPITester.cs       자동 API 테스트 러너
│   │   ├── APIParameterInspector.cs  리플렉션 유틸리티
│   │   └── E2EBootstrapper.cs        런타임 컴포넌트 초기화
│   ├── Editor/
│   │   ├── E2EBuildRunner.cs         CLI 빌드 자동화
│   │   ├── HeavyBuildRunner.cs       대용량 에셋 빌드
│   │   ├── BuildOutputValidator.cs   Level 1 산출물 검증
│   │   ├── EditModeTests/            Level 0 테스트
│   │   └── Tests/                    PackageManagerTests
│   └── Plugins/
│       └── E2ETestBridge.jslib       WebGL 브릿지
└── tests/
    ├── e2e-full-pipeline.test.js     Level 2 본 테스트
    ├── test-interactive-mode.test.js
    ├── perf-ttff.test.js             콜드 로드 계측 (별도 config)
    ├── playwright.config.ts
    └── playwright.perf.config.ts
```

## 실행 모드

빌드 결과물은 쿼리 파라미터로 두 모드를 갖습니다.

| 모드 | 진입 | 동작 |
|------|------|------|
| E2E | `?e2e=true` | 자동 벤치마크와 API 테스트 실행 |
| Interactive | 기본 | 대화형 API 테스터 UI 표시 |

## 로컬 실행

```bash
./run-local-tests.sh --help       # 옵션 보기
./run-local-tests.sh --validate   # 빠른 검증
./run-local-tests.sh --editmode   # Level 0
./run-local-tests.sh --e2e        # Level 2 (기존 빌드 필요)
./run-local-tests.sh --all        # Unity 빌드 포함 전체
```

Playwright를 직접 돌릴 때도 pnpm을 씁니다.

```bash
cd Tests~/E2E/tests
pnpm install
pnpm test
```

브라우저는 러너 이미지의 시스템 Chrome을 사용합니다(`playwright.config.ts`의 `channel`). Playwright가 관리하는 chromium 바이너리는 내려받지 않으므로 `playwright install`은 CI 경로에 없습니다.

## 로컬 CI 재현

E2E CI는 압축 비활성화(`AIT_COMPRESSION_FORMAT="0"`)로 실행되어 신규 빌드에서 Brotli/Gzip 크래시가 없습니다. 이전 빌드 분석이나 압축별 동작 검증이 필요하면 `--compression`과 `--parallel`을 조합합니다.

```bash
# E2E CI와 동일한 경로(압축 비활성화)
./run-local-tests.sh --unity-build --compression disabled --unity-version 6000.2

# Gzip / Brotli 강제 (압축 단계 flaky 재현용)
./run-local-tests.sh --unity-build --compression gzip --unity-version 6000.2
./run-local-tests.sh --unity-build --compression brotli --unity-version 6000.2

# 동시 빌드로 리소스 경합 재현 (모든 버전 병렬)
./run-local-tests.sh --unity-build --parallel --compression brotli
```

압축 포맷 값은 `auto` | `disabled` | `gzip` | `brotli`입니다(`run-local-tests.sh` 참조).

> **주의**: self-hosted 러너의 리소스 경합 자체(CPU/메모리/디스크)는 로컬 머신 스펙에 따라 재현이 보장되지 않습니다. 로컬에서 통과해도 CI flaky가 재현되지 않을 수 있으며, 이 경우 [GitHub Actions 워크플로](github-actions.md)의 "E2E 알려진 flaky 패턴"을 참조해 실패한 잡만 재실행합니다.

## CI

`.github/workflows/test-e2e.yml`이 Level 0부터 2까지를 순서대로 실행하고, 벤치마크 결과를 아티팩트로 올린 뒤 job summary에 성능 메트릭을 남깁니다.

`test_level` 입력으로 범위를 줄일 수 있습니다.

| 값 | 범위 | 대략 소요 |
|----|------|-----------|
| `0` | EditMode만 | 10초 |
| `1` | 빌드와 검증까지, Playwright 스킵 | 8분 |
| `2` | 전체 (기본값) | 14분 |

`test_level`이 `2`가 아니면 `e2e-*` 잡이 skipped 처리되고 OS별 결과는 `build-*` 결과로 폴백합니다.

### 러너 라우팅

self-hosted 러너는 `unity-<version>` 라벨로 1:1 핀되어 있습니다(`runs-on: [self-hosted, unity-${{ inputs.unity-version }}]`). 한 머신이 한 Unity 버전 잡만 받게 해서 라이선스 충돌을 차단합니다. 라벨이 빠진 머신이 생기면 라이선스 에러가 재발합니다.

## 관련 문서

- [기여 가이드](../Contributing.md) — 푸시 전 최소 검증 기준
- [GitHub Actions 워크플로](github-actions.md) — 워크플로 트리거와 실패 대응
- [빌드 중 도메인 리로드 수동 재현](build-session-recovery.md) — 자동화할 수 없는 검증
- [프로젝트 구조](project-structure.md) — 저장소 전체 지도
