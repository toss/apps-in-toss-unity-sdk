# 빌드 중 도메인 리로드 수동 재현

B (도메인 리로드 복원력) 구현을 로컬 Unity 에서 검증하는 절차. 자동화 불가
— `[InitializeOnLoadMethod]` 훅은 Unity 라이프사이클에 의존.

## 사전 준비

- Unity Editor 2021.3 / 2022.3 / 6000.0 / 6000.2 / 6000.3 중 하나
- 샘플 프로젝트: `Tests~/E2E/SampleUnityProject-<version>/`
- SDK 는 로컬 file: 패키지로 설치

## 재현 A: `.cs` 저장으로 리로드 트리거

1. Unity 에서 샘플 프로젝트 오픈
2. `AIT/Build & Package` 실행
3. 콘솔에 `[AIT] pnpm install 진행 중` 로그가 찍힐 때까지 대기 (Packaging 단계)
4. 아무 `.cs` 파일(예: `Assets/Script/Test.cs`)에 공백을 한 줄 추가, Ctrl+S
5. Unity 가 "Reloading Domain" 으로 진입 — 수 초 후 복귀
6. 복귀 후 **콘솔 확인 포인트**:
   ```
   [AIT] 이전 빌드가 중단되어 상태를 복구했습니다.
   entrypoint=Build & Package, stage=Packaging,
   PlayerSettings 복원, 자식 프로세스 N개 정리.
   ```
7. Edit → Project Settings → Player → WebGL → Publishing Settings → Compression Format
   이 원래 값(예: `Disabled`) 으로 돌아왔는지 확인 (빌드 중에는 `Brotli` 로 바뀜)
8. Activity Monitor / 작업 관리자에서 `node` / `pnpm` 프로세스가 남아있지 않은지 확인

## 재현 B: Unity 강제 종료

1. `AIT/Build & Package` 실행, Packaging 단계 진입 확인
2. OS 레벨 kill (`kill -9 $(pgrep -f Unity)` / 작업 관리자 강제 종료)
3. Unity 재실행
4. 에디터 시작 시 **재현 A 의 Step 6 로그가 그대로 나와야** 성공
5. PID kill 은 `0개 정리` 로 나올 수 있음 — 정상 (OS 가 Unity 자식을 함께 종료)
6. PlayerSettings 가 원래 값으로 돌아왔는지 확인

## 재현 C: Stale 세션 (복구 skip 확인)

1. Unity 종료 상태에서 `Library/ScriptableSingleton/AITBuildSession.asset` 을 편집:
   - `startedAtUnixSec` 를 25 시간 전으로 수정
   - 또는 `unityVersion` 을 `"9999.9.9"` 로 수정
2. Unity 재실행
3. 콘솔 확인: `[AIT] Stale build session 발견 및 제거` 로그만, 복원 로그 X
4. `Library/ScriptableSingleton/AITBuildSession.asset` 이 비어있음 (sessionId=null)

## 재현 D: Idle gate 타임아웃

1. Unity 에 무한 컴파일 루프를 유도하기 어려움 → 대신 **테스트 probe** 로 검증:
   `AITEditorIdleWaiterTests.WaitAsync_TimesOut_AfterConfiguredSeconds` 단위 테스트
   가 이 경로를 커버 — `./run-local-tests.sh --editmode` 실행으로 확인.

## 공통 실패 증상

- 복원 로그 없이 PlayerSettings 가 build-time 값 그대로 유지 → 세션 파일이
  쓰이지 않은 것. `Library/ScriptableSingleton/AITBuildSession.asset` 존재 확인.
- `node` 프로세스 남음 → PID 기록 누락. 콘솔에 `RecordPid` 관련 경고 로그 있는지.
- Stale 판정 오작동으로 복원 안 됨 → `IsStale` 판정 기준 (24h / Unity/SDK 버전)
  재확인.
