# 빌드 중 도메인 리로드 수동 재현

`AITBuildSessionRecovery`가 실제로 동작하는지 로컬 Unity에서 확인하는 절차입니다. `[InitializeOnLoadMethod]` 훅이 Unity 라이프사이클에 의존해서 자동화할 수 없습니다.

> **대상**: SDK 기여자.

## 사전 준비

- Unity Editor 2021.3, 2022.3, 6000.0, 6000.2, 6000.3 중 하나
- 샘플 프로젝트 `Tests~/E2E/SampleUnityProject-<버전>/`
- SDK를 로컬 `file:` 패키지로 설치

## cs 저장으로 리로드 트리거

1. Unity에서 샘플 프로젝트를 엽니다.
2. `AIT` > `Advanced` > `Build & Package`를 실행합니다.
3. 콘솔에 `[AIT] pnpm install 진행 중` 로그가 찍힐 때까지 기다립니다. Packaging 단계입니다.
4. 아무 `.cs` 파일(예: `Assets/Script/Test.cs`)에 공백 줄을 하나 추가하고 저장합니다.
5. Unity가 Reloading Domain에 진입했다가 수 초 뒤 복귀합니다.
6. 복귀 후 콘솔에 복원 로그가 나와야 합니다.

   ```text
   [AIT] 이전 빌드가 중단되어 상태를 복구했습니다.
   entrypoint=Build & Package, stage=Packaging,
   PlayerSettings 복원, 자식 프로세스 N개 정리.
   ```

7. Edit의 Project Settings에서 Player의 WebGL Publishing Settings에 있는 Compression Format이 빌드 전 값으로 돌아왔는지 확인합니다. 빌드 중에는 프로필이 지정한 값으로 바뀌어 있습니다.
8. Activity Monitor나 작업 관리자에서 `node`와 `pnpm` 프로세스가 남아 있지 않은지 확인합니다.

## Unity 강제 종료

1. `AIT` > `Advanced` > `Build & Package`를 실행하고 Packaging 단계 진입을 확인합니다.
2. OS 레벨에서 종료합니다 — `kill -9 $(pgrep -f Unity)` 또는 작업 관리자 강제 종료.
3. Unity를 다시 실행합니다.
4. 에디터 시작 시 위와 같은 복원 로그가 그대로 나와야 성공입니다.
5. 자식 프로세스는 `0개 정리`로 나올 수 있습니다. OS가 Unity 자식을 함께 종료하므로 정상입니다.
6. PlayerSettings가 원래 값으로 돌아왔는지 확인합니다.

## Stale 세션

복구를 건너뛰는 경로를 확인합니다.

1. Unity가 꺼진 상태에서 `Library/ScriptableSingleton/AITBuildSession.asset`을 엽니다.
2. `startedAtUnixSec`를 25시간 전으로 바꾸거나 `unityVersion`을 `"9999.9.9"`로 바꿉니다.
3. Unity를 실행합니다.
4. 콘솔에 `[AIT] Stale build session 발견 및 제거` 로그만 나오고 복원 로그는 나오지 않아야 합니다.
5. `AITBuildSession.asset`이 비어 있어야 합니다(`sessionId=null`).

## Idle gate 타임아웃

Unity에 무한 컴파일 루프를 유도하기 어려우므로 단위 테스트로 대신 확인합니다. `AITEditorIdleWaiterTests.WaitAsync_TimesOut_AfterConfiguredSeconds`가 이 경로를 커버합니다.

```bash
./run-local-tests.sh --editmode
```

## 실패 증상과 원인

| 증상 | 확인할 것 |
|------|-----------|
| 복원 로그 없이 PlayerSettings가 빌드 중 값 그대로 | 세션 파일이 쓰이지 않았습니다. `Library/ScriptableSingleton/AITBuildSession.asset` 존재 여부 확인 |
| `node` 프로세스가 남음 | PID 기록 누락. 콘솔에 `RecordPid` 관련 경고가 있는지 확인 |
| Stale 판정 오작동으로 복원이 안 됨 | `IsStale` 판정 기준(24시간, Unity·SDK 버전) 재확인 |

## 관련 문서

- [테스트 전략](testing.md) — 자동화된 테스트 층위
- [구현 지점 색인](implementation-details.md) — 세션 복원 구현 위치
- [빌드 파이프라인](../BuildProcess.md) — Packaging 단계가 하는 일
