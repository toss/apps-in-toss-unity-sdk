# SDK Generate Workflow

SDK 런타임 코드를 재생성하고 검증합니다.

## Steps

1. `cd sdk-runtime-generator~`
2. `pnpm generate` 실행하여 TypeScript → C# + jslib 브릿지 생성
3. `pnpm validate` 실행하여 Mono mcs 컴파일 검사
4. `pnpm test` 실행하여 유닛 테스트
5. 생성된 파일 변경사항 요약: `git diff --stat Runtime/SDK/`
6. 결과 보고

## Arguments

- `--format`: 생성 후 `pnpm format`도 실행 (CSharpier, dotnet 필요)
- `$ARGUMENTS`가 있으면 추가 컨텍스트로 사용
