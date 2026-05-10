# SDK Generate Workflow

SDK 런타임 코드를 재생성하고 검증합니다.

## Steps

1. `cd sdk-runtime-generator~`
2. `pnpm generate` 실행하여 TypeScript → C# + jslib 브릿지 생성 (~1.5초)
3. `pnpm validate` 실행하여 vitest로 생성 코드 속성 검증
4. `pnpm test` 실행하여 유닛 테스트
5. 생성된 파일 변경사항 요약: `git diff --stat Runtime/SDK/`
6. `Runtime/SDK/` 하위 `.cs`/`.jslib` 파일이 rename·add·remove 됐다면 **Library/Bee 캐시 무효화 영향** 사용자에게 안내 (PR #540 정책: SDK 변경 PR은 CI에서 풀 클린 빌드)
7. 결과 보고

## Arguments

- `--format`: 생성 후 `pnpm format`도 실행 (CSharpier, dotnet 필요)
- `$ARGUMENTS`가 있으면 추가 컨텍스트로 사용

## 주의

- `Runtime/SDK/` 의 파일은 절대 직접 수정하지 말 것 (`CLAUDE.md`의 "자동 생성 코드 정책" 참조)
- 생성기 변경 후 push 전: `./run-local-tests.sh --validate`(~30초)로 invariants 검증 권장
- pnpm 버전이 변경됐다면 `Editor/AITPackageManagerHelper.cs`의 `PNPM_VERSION`과 3개 `package.json`의 `packageManager` 필드 동기화 확인 (CLAUDE.md "pnpm 버전 핀 동기화" 참조)
