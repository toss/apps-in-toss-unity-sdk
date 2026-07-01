# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).

## 리팩토링 (대형 파일 분리)

- **P2 — `Editor/AITConvertCore.cs` (1,298행)**: 빌드 초기화·에셋 내보내기·WebGL 생성·에러 처리가 한 클래스에 집중. 단계별 Strategy/Pipeline 분리.

## 비동기 / 스레드 안전성

- **P2 — `Thread.Sleep` 블로킹**: 백그라운드 ThreadPool 폴링 루프 2곳을 `await Task.Delay`로 전환 — `AITAsyncCommandRunner.cs`(프로세스 종료 폴링), `Editor/Package/PnpmRunner.cs`(pnpm install 폴링). 두 루프 모두 내부에서 메인 스레드 API를 쓰지 않아 대기 중 풀 스레드 반납이 안전. 나머지는 동기 유지가 불가피(전환 시 데드락/계약 위반):
  - 같은 루프에서 메인 스레드 진행률 UI 호출: `AITNodeJSDownloader.cs:143`, `AITNpmRunner.cs:307`, `AITPackageInitializer.cs:337` (`EditorUtility.DisplayProgressBar`).
  - 동기 종료 계약: `AITProcessTreeManager.cs:283`(`IDisposable.Dispose`의 SIGTERM→SIGKILL 유예), `AITSentryTransport.cs:268`(`EditorApplication.quitting` 동기 핸들러의 마지막 flush).
