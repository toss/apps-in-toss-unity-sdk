# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기준 작성 · 2026-06-16 코드 대조로 완료 항목 정리.
> 우선순위 P1(높음) ~ P3(낮음).

## 리팩토링 (대형 파일 분리)

- **P2 — `Editor/AITConvertCore.cs` (1,298행)**: 빌드 초기화·에셋 내보내기·WebGL 생성·에러 처리가 한 클래스에 집중. 단계별 Strategy/Pipeline 분리.

## 비동기 / 스레드 안전성

- **P2 — `Thread.Sleep` 블로킹**: 백그라운드 ThreadPool 폴링 루프 2곳을 `await Task.Delay`로 전환 — `AITAsyncCommandRunner.cs`(프로세스 종료 폴링), `Editor/Package/PnpmRunner.cs`(pnpm install 폴링). 두 루프 모두 내부에서 메인 스레드 API를 쓰지 않아 대기 중 풀 스레드 반납이 안전. 나머지는 동기 유지가 불가피(전환 시 데드락/계약 위반):
  - 같은 루프에서 메인 스레드 진행률 UI 호출: `AITNodeJSDownloader.cs:143`, `AITNpmRunner.cs:307`, `AITPackageInitializer.cs:337` (`EditorUtility.DisplayProgressBar`).
  - 동기 종료 계약: `AITProcessTreeManager.cs:283`(`IDisposable.Dispose`의 SIGTERM→SIGKILL 유예), `AITSentryTransport.cs:268`(`EditorApplication.quitting` 동기 핸들러의 마지막 flush).
- **P2 — 동기화 없는 static 필드**: `AITConvertCore.cs:33-34`의 `isCancelled`/`currentAsyncTask` → `Interlocked`/`volatile`로 스레드 안전성 보장. (AppsInTossMenu 서버 상태는 `AITServerStateManager`로 이전 완료)

## 의존성

- **P2 — `sdk-runtime-generator~/pnpm-lock.yaml` gitignored** (`.gitignore:181`): package.json은 핵심 의존성을 pin하지만 transitive dependency는 drift 가능. 재현성 목표라면 lockfile 커밋 여부 결정.

## 보안

- **P3 — Sentry DSN 하드코딩** (`Editor/ErrorTracker/AITEditorErrorTracker.cs:21`): public key라 위험은 낮음. 환경변수/설정 주입 방식 검토.

## Sentry / 빌드 진단

- **P2 — SDK breaking change 모니터링**: SDK API 변경으로 사용자 프로젝트 컴파일 에러(SDK-80/81/7Z) 유입 — 삭제·무시하지 말고 유지(breaking change 영향 파악용 데이터). `error_source` 분류 프레임워크는 구축됨 → `sdk_breaking_change` 세분류 추가 검토.
- **P2 — WebGL 빌드 실패 진단 잔여 갭**: 외부 명령(pnpm/granite) exit code + stderr 첨부는 `AITBuildDiagnostics`로 해결됨. 남은 갭:
  - Unity 내부 Bee/IL2CPP `.o` 컴파일 실패가 `BuildReport.steps`에 Error로 안 잡히는 경우 빌드 로그 tail 첨부 (실제 Bee 실패 빌드 재현으로 `result.steps` 검증 선행).
  - `maxMessages`가 Error+Warning 합산이라, Error가 10개를 채우면 Warning이 누락되고 `외 N개 생략`의 N이 모호 (`AITConvertCore`).
