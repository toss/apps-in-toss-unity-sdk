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

### PlayerPrefs 영속화 테스트 그룹 (9-x)

`e2e-full-pipeline.test.js`의 9번대 테스트가 [PlayerPrefs 영속화](https://developers-apps-in-toss.toss.im/documentation/unity/add-features/playerprefs)를 검증합니다. 동작 설명은 그 문서가 정본이고, 여기서는 각 케이스가 무엇을 확인하는지만 요약합니다.

| 케이스 | 확인 내용 |
|--------|-----------|
| 9-1 / 9-2 | 앱인토스 Storage 경로로 저장된 값이 IndexedDB를 CDP로 wipe해도 reload 후 생존 |
| 9-3 | 앱인토스 Storage 실패(mock이 항상 reject)가 부팅을 막지 않음 |
| 9-4 | 앱인토스 Storage 실패 시 IndexedDB 폴백으로 동작(2021.3은 순정 IDBFS 세션 노화 결함으로 값 단언을 skip — 9-6 참고) |
| 9-5 | mock 없는 순정 프로덕션 경로에서 에러·거부·부팅 회귀 없음 |
| 9-6 | [통제군, 2021.3 전용] 레이어를 완전히 끈 순정 Unity 상태에서 같은 세션 노화 시나리오를 재연해, 9-4의 실패가 레이어와 무관함을 증명 |
| 9-7 | 제휴사(게임)가 자체 키로 Storage를 직접 쓰는 상황에서, 레이어가 제휴사 소유 키를 절대 건드리지 않음(레이어의 접근은 매니페스트 키 1개로 한정됨)을 호출 장부(ledger)로 감사 |
| 9-8 | [레거시 origin 마이그레이션] 로컬에 PlayerPrefs가 없는 부팅에서 `__AIT_PP_LEGACY_SOURCE__` 오버라이드 훅이 준 옛 origin IDBFS 덤프(실제 Unity가 쓴 PlayerPrefs 바이트)를 채택해 MEMFS에 심고 AIT Storage로 승격함(경로 리매핑 포함)을 `TriggerPlayerPrefsGet` 왕복과 `status()`로 검증. ⚠️ **단언은 반드시 Get 이후 `status()`에 건다** — 심기가 populate가 아니라 Unity의 첫 `<appDir>/PlayerPrefs` 접근(= `node_ops.lookup` 미스)까지 지연되므로 부팅 직후 값은 `deferred`가 정상이다(`PlayerPrefsTester`는 Awake/Start에서 PlayerPrefs를 건드리지 않는다). seed 추출용 IDB 프로브는 오래 산 페이지에서 무응답이 되는 실측이 있어(2021.3 계열 세션 노화) 실패 시 같은 origin의 갓 만든 빈 페이지에서 1회 재시도하는데, 이때 seed 페이지를 **`browser.newContext()`로 명시 생성**해야 한다 — `browser.newPage()`는 페이지 1개 전용 컨텍스트라 `.newPage()`가 `Please use browser.newContext()`로 거부된다(run 32466990653에서 2021.3/2022.3 4개 leg가 이 경로로 죽었다). 예산 600초 |
| 9-8b | [레거시 origin 마이그레이션 — 빈 매니페스트] "PlayerPrefs가 하나도 없는 매니페스트"가 이미 깔린 설치에서도 9-8과 동일하게 발화함을 확인 — 마이그레이션 창은 매니페스트 부재가 아니라 "스냅샷에 scoped 파일 0건"으로 판정되며, 그렇지 않으면 신 origin에서 한 번이라도 부팅한 기존 설치가 이관 대상에서 통째로 누락된다. 원래 9-8의 3단계였는데 분리했다: `test.setTimeout`이 테스트 단위라 앞 단계가 예산을 다 먹고 마지막 단계가 시간 안에 못 끝났다(run 32462382123). 분리 후 실측 3.3~3.8분으로 예산에 여유가 확인됐다(run 32466990653). 1단계 seed는 `describe` 스코프의 `pp8LegacySeed`로 9-8에서 물려받는다(`describe.serial`이라 순서·skip 전파가 보장됨). 9-8과 같은 이유로 단언은 Get 이후 `status()`에 건다. 예산 420초 |
| 9-9 | [회귀 방지] `__AIT_PP_LEGACY_SOURCE__` 훅을 설치하지 않으면 absent 분기 동작이 어댑터 도입 이전과 정확히 동일함(`legacyImport`/`legacyBackend`가 `none`, `legacyBytes`가 0)을 확인 |
| 9-10 | [실패 매트릭스] 레거시 소스가 reject하거나 hang해도 부팅을 막지 않음 — reject는 `legacyImport: 'error'`, hang은 자체 타임박스로 `legacyImport: 'timeout'`. 두 분기 모두 `mode: 'ait'`로 부팅 완료하고, 빈 매니페스트(`files: {}`)를 남기지 않아 다음 부팅에 재시도 여지가 남는지도 함께 검증. 분기마다 **콜드 부트 1회**만 쓴다 — `readIdbfs` 호출에 앱 디렉터리 선행 조건이 없어졌기 때문이다(앱 디렉터리 관측은 심기 시점으로 밀렸다). 예전의 워밍 부팅 2회는 순수 낭비였고 run 32466990653의 예산 초과(420초 예산 대 426초 실측, 5개 leg가 6초 차로 사망)에 기여했다. 예산 600초 |
| 9-11 | [콜드 부트 이관] 이 origin에서 한 번도 실행된 적 없는 설치가 **같은 세션 안에** 이관을 끝내는지 — 실제 이관 대상 인구의 첫 부팅이 이 모양이라 기능의 본체를 증명하는 테스트다. 부팅 직후 `legacyImport: 'deferred'` + `legacyBytes: 0`(추측해서 심지 않았음)을 확인한 뒤, `TriggerPlayerPrefsGet`이 만드는 첫 접근에서 `imported`로 전이하고 `legacyAppDir`이 실제 앱 디렉터리(`legacy_origin_seed`가 아닌)로 기록되며 같은 Get이 `v8`을 돌려주는지까지 본다 — 심으면서 노드를 돌려주므로 Unity가 그 자리에서 우리 바이트를 읽는다. 원래 9-10의 세 번째 분기로 `skip-unknown-appdir`(심을 곳을 모르니 포기)을 고정하고 있었으나, 어댑터가 추측 대신 관측으로 바뀌면서 기대값이 통째로 뒤집혀 별도 테스트로 분리했다. seed는 `pp8LegacySeed`를 재사용. 예산 300초 |

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
