# 테스트 전략

## Unity 버전 요구사항

**우선 지원 버전:**
- Unity 6000.3.3f1 (Unity 6.3)
- Unity 6000.2.15f1 (Unity 6 LTS)
- Unity 6000.0.66f2 (Unity 6)
- Unity 2022.3.62f3 (LTS)
- Unity 2021.3.45f2 (LTS - 최소 지원 버전)

모든 개발, 테스트, QA는 이 5개 버전을 우선으로 진행해야 합니다.

- Tuanjie Engine 지원

**중요**: SDK는 Unity 2021.3 이상 모든 버전을 지원해야 합니다.

## E2E 테스트 구조

SDK는 5개 Unity 버전에 대해 E2E 테스트를 실행합니다:

```
Tests~/E2E/
├── SampleUnityProject-6000.3/    # Unity 6000.3 테스트 프로젝트
├── SampleUnityProject-6000.2/    # Unity 6000.2 테스트 프로젝트
├── SampleUnityProject-6000.0/    # Unity 6000.0 테스트 프로젝트
├── SampleUnityProject-2022.3/    # Unity 2022.3 테스트 프로젝트
├── SampleUnityProject-2021.3/    # Unity 2021.3 테스트 프로젝트
├── SharedScripts/                # 공유 테스트 스크립트 (UPM 패키지)
│   ├── Runtime/
│   │   ├── InteractiveAPITester.cs   # 대화형 API 테스터 (WebGL UI)
│   │   ├── RuntimeAPITester.cs       # 자동 API 테스트 러너
│   │   ├── APIParameterInspector.cs  # API 리플렉션 유틸리티
│   │   └── E2EBootstrapper.cs        # 런타임 컴포넌트 초기화
│   ├── Editor/
│   │   ├── E2EBuildRunner.cs         # CLI 빌드 자동화
│   │   ├── BuildOutputValidator.cs   # 빌드 산출물 검증 (Level 1)
│   │   └── EditModeTests/            # EditMode 테스트 (Level 0)
│   └── Plugins/
│       └── E2ETestBridge.jslib       # WebGL JS↔C# 브릿지
└── tests/
    ├── e2e-full-pipeline.test.js     # Playwright E2E 테스트 (5 tests)
    └── playwright.config.ts
```

## 테스트 모드

- **E2E 모드** (`?e2e=true`): 자동 벤치마크 + API 테스트 실행
- **Interactive 모드** (기본): 대화형 API 테스터 UI 표시

## 테스트 실행

**로컬 테스트 러너:**
```bash
./run-local-tests.sh --help     # 옵션 보기
./run-local-tests.sh --validate # 빠른 검증만
./run-local-tests.sh --all      # 전체 Unity 빌드 + E2E 테스트
./run-local-tests.sh --e2e      # E2E 테스트만 (기존 빌드 필요)
```

**수동 Playwright 실행:**
```bash
cd Tests~/E2E/tests
npm install
npx playwright install chromium
npm test
```

## CI/CD 통합

`.github/workflows/test-e2e.yml` 실행 내용 (3-Level 테스트 구조):
1. EditMode 테스트 (Level 0: C# 순수 로직 검증, 빌드 없이 ~10초) — **self-hosted Unity runner**
2. Unity WebGL 빌드 + C# 빌드 산출물 검증 (Level 1) — **self-hosted Unity runner** (`build-macos` / `build-windows` 잡)
3. Playwright E2E 테스트 (Level 2: 5개 테스트) — **ubuntu-latest** (`e2e-macos` / `e2e-windows` 잡)
4. 벤치마크 결과 아티팩트로 업로드
5. 성능 메트릭이 포함된 Job summary

**runner 라우팅**:
- self-hosted runner는 `unity-<version>` 라벨로 1:1 핀 — `runs-on: [self-hosted, unity-${{ inputs.unity-version }}]`
- 라이선스 충돌 차단을 위해 한 머신은 한 Unity 버전 잡만 받음
- Playwright는 ubuntu에서 artifact 다운로드 → vite preview 서버 → 테스트 실행 (Unity binary 비의존)

`test-level` 파라미터로 실행 범위 제어:
- `0`: EditMode만 (~10초)
- `1`: 빌드+검증, Playwright 스킵 (~8분)
- `2`: 전체 실행 (~14분, 기본값)

`test_level=0/1`이면 `e2e-*` 잡은 skipped 처리되고 결과는 `build-*` 결과로 폴백.
