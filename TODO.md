# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).
> 2026-07-08 P2 잔여 항목 완료로 정리.
> 2026-07-20 perf 채널 적대적 리뷰 후속 과제 등재.

## P3 (낮음)

- **레거시 early-fetch 킥오프 런타임 실행 기반 테스트 보강** — 현재 `AITEarlyFetchScriptTests`는 생성된 JS의 토큰 존재만 `StringAssert`로 검증해, 런타임 동작 회귀(로더 fetch의 pending 합류, `bodyUsed` 응답 재사용 방지 폴백, 저메모리 분기의 실제 fetch 선택, `init.signal` 우회)는 잡지 못한다. Node `vm`/`child_process`로 생성 스크립트를 `fetch`/`caches`/`sessionStorage` mock과 함께 실제 실행해 이 동작들을 assert하는 테스트를 추가하거나, `Tests~/E2E/tests/e2e-ce-serving.test.js`에 `cache: early-kick`/`early-join` 로그 존재 + Build 리소스 단일 다운로드(이중 다운로드 미발생) 검증 케이스를 추가한다. (근거: 2026-07 early-fetch 킥오프 적대적 리뷰 confirmed finding — `Editor/Package/WebGLBuildCopier.cs` `GenerateEarlyFetchScriptLegacyCaching`)
