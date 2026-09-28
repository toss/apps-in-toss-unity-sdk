# SDK Generate Workflow

SDK 런타임 코드를 재생성하고 검증합니다.

## Steps

아래 순서가 생성기 검증의 정본이다. package.json의 검증용 별칭 스크립트는 `test`와 동일한 `vitest run`이라 같은 스위트를 두 번 도므로 쓰지 않는다.

1. `cd sdk-runtime-generator~`
2. `pnpm generate` 실행하여 TypeScript → C# + jslib 브릿지 생성 (~1.5초)
3. 저장소 루트에서 `git status --short Runtime/SDK/`와 `git diff --stat Runtime/SDK/`로 의도한 변경만 있는지 확인
4. 저장소 루트에서 `./run-local-tests.sh --validate` 실행. CI Validate 워크플로와 같은 `pnpm run test:invariants`를 포함해 파일 구조·Playwright 설정·SDK 유닛 테스트까지 확인한다
5. Unity가 로컬에 있으면 `./run-local-tests.sh --editmode`로 Runtime/SDK C# 컴파일과 EditMode를 확인 (권장)
6. 생성기 파서·타입 매핑을 바꿨다면 `cd sdk-runtime-generator~ && pnpm test`(vitest 전체, invariants의 상위집합) 추가 실행 (권장)
7. `Runtime/SDK/` 하위 `.cs`/`.jslib` 파일이 rename·add·remove 됐다면 **Library/Bee 캐시 무효화 영향**을 사용자에게 안내한다. SDK 변경 PR은 CI에서 풀 클린 빌드다(상세: `Documentation~/internal/github-actions.md`의 "Library/Bee 캐시 무효화 정책")
8. 결과 보고

## Arguments

- `--format`: 생성 후 `pnpm format`도 실행 (CSharpier, dotnet 필요)
- `$ARGUMENTS`가 있으면 추가 컨텍스트로 사용

## 주의

- `Runtime/SDK/` 의 파일은 절대 직접 수정하지 말 것 (`CLAUDE.md`의 "자동 생성 코드 정책" 참조)
- pnpm 버전이 변경됐다면 `Editor/AITPackageManagerHelper.cs`의 `PNPM_VERSION`과 3개 `package.json`의 `packageManager` 필드 동기화 확인 (CLAUDE.md "pnpm 버전 핀 동기화" 참조)

## push 전 점검

1. 생성물 상태 확인: `sdk-runtime-generator~/` 또는 `Runtime/SDK/` 근처를 수정했다면 위 Steps 2-3을 실행한다. `Runtime/SDK/` 하위에 예상치 못한 변경이 보이면 생성기 수정 의도와 맞는지 확인하고, 의도하지 않은 산출물은 커밋 전 조치한다.
2. .gitignore 적용 확인: `webgl/`, `ait-build/dist/`, `ait-build/node_modules/`, `Library/`, `Temp/`, `*.log` 등이 `git status`에 나타나면 `.gitignore`에 빠진 항목이 있는지 검토한다.
3. 로컬 검증: 위 Steps 4-6을 실행한다.
4. 스테이지 최종 리뷰: `git diff --cached`로 정확히 어떤 파일이 커밋되는지 확인한다. 의도하지 않은 `Runtime/SDK/` 재생성 산출물, `.meta` 파일 누락·추가(Lint 워크플로우에서 검출), 대용량 바이너리·빌드 산출물 혼입 여부를 특히 체크한다.
