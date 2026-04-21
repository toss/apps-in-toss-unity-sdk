# TODO: Repository 개선 항목

> 2026-04-14 전체 리뷰 기반으로 작성. 우선순위: P0(즉시) ~ P3(장기).

---

## 문서 정합성 이슈

### P1 — SDK Generator 테스트 README: 삭제된 파일 참조
- **파일**: `sdk-runtime-generator~/tests/unit/README.md` (8, 83, 126행)
- **현상**: `compilation.test.ts`를 Tier 1 테스트로 참조하지만, 이 파일은 `cc95357` 커밋에서 삭제됨
- **조치**: README에서 Tier 1 참조 제거 또는 현재 테스트 구조에 맞게 갱신

### P1 — SDK Generator tests/unit/package.json: 존재하지 않는 스크립트
- **파일**: `sdk-runtime-generator~/tests/unit/package.json` (12행)
- **현상**: `"test:tier1": "vitest run compilation.test.ts"` — 파일 없음
- **조치**: 삭제된 tier1 스크립트 제거, 부모 package.json의 tier 스크립트와 정합성 확보

### P2 — Contributing.md: 삭제된 Mono mcs 검증 참조
- **파일**: `Docs~/Contributing.md` (44행)
- **현상**: `pnpm validate # Mono mcs로 컴파일 검사` — Mono mcs 의존성은 이미 제거됨
- **조치**: `pnpm validate` 설명을 현재 동작("vitest run")에 맞게 수정

### P2 — SentryIntegration.md: 예시 버전 번호 오래됨
- **파일**: `Docs~/SentryIntegration.md` (69, 89, 95행)
- **현상**: 예시에서 SDK 버전 `1.11.2` 사용 — 현재 `2.4.6`
- **조치**: 현재 버전으로 예시 갱신

---

## 코드 구조 개선 (리팩토링)

### P1 — AIT.Types.cs (2,086행): 자동 생성 타입 파일 분할
- **파일**: `Runtime/SDK/AIT.Types.cs`
- **현상**: 100+ enum/class 정의가 단일 파일에 집중. Permission 관련 enum 3개 중복 (`GetPermissionPermissionName`, `OpenPermissionDialogPermissionName`, `RequestPermissionPermissionName` — 동일 값)
- **조치**: sdk-runtime-generator에서 카테고리별 타입 파일 분할 생성 검토, Permission enum 통합

### P1 — AITPackageBuilder.cs (1,965행): God class 분할
- **파일**: `Editor/AITPackageBuilder.cs`
- **현상**: pnpm 패키징, WebGL 빌드 복사, HTML 템플릿 생성, early fetch 스크립트, node_modules 검증 등 다수 책임
- **조치**: `PnpmPackageManager`, `WebGLBuildCopier`, `TemplateProcessor` 등으로 분리

### P1 — AppsInTossMenu.cs (1,915행): 모놀리식 메뉴 컨트롤러
- **파일**: `Editor/AppsInTossMenu.cs`
- **현상**: Dev/Prod 서버 관리, 포트 해석, 브라우저 실행, 배포, 플러그인 설치가 하나의 파일에 혼재
- **조치**: `ServerManager`, `DeploymentController`, `PortResolver` 등으로 분리

### P2 — AITConvertCore.cs (895행): 빌드 단계 분리
- **파일**: `Editor/AITConvertCore.cs`
- **현상**: 빌드 초기화, 에셋 내보내기, WebGL 생성, 에러 처리가 한 클래스에 집중
- **조치**: 빌드 단계별 Strategy 또는 Pipeline 패턴 도입

### P2 — run-local-tests.sh (1,255행): 모듈화
- **파일**: `run-local-tests.sh`
- **현상**: 테스트 함수는 잘 분리되어 있으나 단일 파일이 비대
- **조치**: `scripts~/test-editmode.sh`, `scripts~/test-e2e.sh` 등으로 분리

---

## 비동기/스레딩 개선

### P2 — AITNodeJSDownloader.cs: Thread.Sleep 블로킹
- **파일**: `Editor/AITNodeJSDownloader.cs` (143행)
- **현상**: `Thread.Sleep(1000 * retry)` — 최대 3초까지 블로킹 (호출 컨텍스트의 스레딩 모델 확인 필요)
- **조치**: 메인 스레드 호출 확인 후, 필요 시 `Task.Delay()` + async/await 패턴으로 전환

### P2 — .Result 동기 블로킹 호출
- **파일/행**:
  - `Editor/AITAutoUpdater.cs:485` — `client.GetStringAsync(apiUrl).Result`
  - `Editor/AITGitGuard.cs:392` — `stdoutTask.Result`, `stderrTask.Result`
  - `Editor/AITPlatformHelper.cs:396-397` — `outputTask.Result`, `errorTask.Result`
- **현상**: `.Result` 호출은 데드락 가능성
- **조치**: async/await 체인으로 리팩토링

### P2 — 기타 Thread.Sleep 호출 (6곳)
- `AITProcessTreeManager.cs:283` (300ms)
- `AITNpmRunner.cs:296` (200ms)
- `AITSentryTransport.cs:185` (10ms 반복)
- `AITPackageInitializer.cs:343`
- `AITAsyncCommandRunner.cs:261` (100ms)
- `AITPackageBuilder.cs:334` (200ms)
- **조치**: 가능한 곳부터 비동기 패턴 전환

---

## 에러 처리 개선

### P2 — 빈 catch 블록 (20+ 곳)
- **주요 파일**:
  - `Editor/AITFileUtils.cs` — 5개 빈 catch 블록 (176, 186, 194, 216, 219행)
  - `Editor/AITFileUtils.cs` 내 `AITUnixPermission` 클래스 — 2개 빈 catch 블록 (145, 148행)
  - `Editor/AITNodeJSDownloader.cs` — 5개 빈 catch 블록 (168, 274, 289, 334, 341행)
  - `Editor/AITGitGuard.cs:386-387`
  - `Editor/AITPackageBuilder.cs:93-94` (ObjectDisposedException 한정 catch — 의도적 패턴이나 로깅 부재)
  - `Editor/AITDeprecationChecker.cs:215`
- **현상**: `catch (Exception) { }` — 오류가 완전히 숨겨짐
- **조치**: 최소한 `Debug.LogWarning`으로 로깅 추가. `FileSystemHelper.SafeDelete()` 같은 공통 유틸리티 추출

---

## 의존성 관리

### P2 — BuildConfig~/package.json의 floating versions
- **파일**: `WebGLTemplates/AITTemplate/BuildConfig~/package.json`
- **현상**: `vite: "^8.0.8"`, `typescript: "^6.0.2"` — caret(^)은 마이너/패치 업데이트를 허용하므로 예기치 않은 마이너 버전 변경으로 빌드가 깨질 수 있음
- **조치**: 정확한 버전 고정 또는 `~` (틸드, 패치만 허용) 사용

### P2 — sdk-runtime-generator~/package.json의 floating versions
- **현상**: `commander: "^14.0.2"`, `ts-morph: "^27.0.2"` 등 다수 caret 버전
- **조치**: 핵심 의존성은 정확한 버전 고정

---

## 보안

### P3 — Sentry DSN 하드코딩
- **파일**: `Editor/ErrorTracker/AITEditorErrorTracker.cs` (20행)
- **현상**: DSN이 소스코드에 직접 내장 (Sentry public key이므로 기술적으로는 안전 — 보안 위험 낮음)
- **조치**: 환경변수 또는 설정 파일 주입 방식으로 전환 검토 (낮은 우선순위)

---

## 코드 중복 제거

### P2 — 프로세스 실행 + 타임아웃 패턴 중복
- **파일**: `AITGitGuard.cs`, `AITPlatformHelper.cs`, `AITAsyncCommandRunner.cs`
- **현상**: "프로세스 생성 → 타임아웃 대기 → 출력 캡처" 패턴이 3곳 이상에서 반복
- **조치**: `ProcessExecutor` 유틸리티 클래스 추출

### P2 — 파일 삭제 + 예외 무시 패턴 중복 (15+ 곳)
- **파일**: `AITNodeJSDownloader.cs`, `AITPackageBuilder.cs`, `AITFileUtils.cs`
- **현상**: `try { File.Delete(x); } catch { }` 반복
- **조치**: `FileSystemHelper.SafeDelete()` 공통 메서드 추출

### P1 — Permission enum 3중 중복
- **파일**: `Runtime/SDK/AIT.Types.cs` (33, 67, 93행)
- **현상**: `GetPermissionPermissionName`, `OpenPermissionDialogPermissionName`, `RequestPermissionPermissionName` — 동일한 enum 값이 3번 정의
- **조치**: sdk-runtime-generator에서 공유 `PermissionName` enum 하나로 통합 생성

---

## 정적 상태/스레드 안전성

### P2 — 동기화 없는 static 필드
- **파일/행**:
  - `Editor/AppsInTossMenu.cs:22-29` — `devServerState`, `prodServerState` 등
  - `Editor/AITConvertCore.cs:33-35` — `isCancelled`, `currentAsyncTask`
  - `Editor/AITPackageBuilder.cs:73,81` — volatile 사용 중이나 문서화 부족
- **조치**: `Interlocked` 또는 `lock` 사용, 스레드 안전성 보장 명시

---

## 테스트 커버리지

### P2 — Editor 코드 유닛 테스트 부족
- **현상**: 31개 Editor 소스 파일 중 유닛 테스트는 7개 테스트 파일 (62개 테스트 메서드)뿐
- **미테스트 핵심 클래스**: `AITPackageBuilder`, `AITConvertCore`, `AITPackageManagerHelper`, `AITPlatformHelper`
- **조치**: 빌드 검증 로직, pnpm install 폴백 시나리오, 권한 오류 등에 대한 테스트 추가

---

## Sentry / ErrorTracker 개선 (2026-04-18 Sentry 이슈 전수 조사 기반)

### P2 — 사용자 SDK API 변경으로 인한 컴파일 에러 모니터링
- **이슈**: SDK-80, 81 (AppsInTossMenu.Build/Package 정의 없음), SDK-7Z (namespace AppsInToss not found)
- **현상**: SDK 업데이트 후 API 변경으로 사용자 프로젝트에서 컴파일 에러 발생. SDK breaking change의 영향을 파악할 수 있는 귀중한 데이터
- **조치**: 이 이슈들은 삭제/무시하지 말고 모니터링. `error_source`를 `sdk_breaking_change`로 분류하는 것 검토

### P3 — Sentry 이슈 자동 정리 자동화
- **현상**: resolve/ignore 처리해도 이전 SDK 버전 사용자에서 같은 패턴이 새 이슈 ID로 재생성됨. enterprise audit 제한으로 auto-resolve 불가
- **조치**: 주기적으로 `!has:error_source` 이슈를 자동 삭제하는 스케줄 워크플로우 검토, 또는 Sentry의 Inbound Filter 기능 활용
