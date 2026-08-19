# PlayerPrefs 영속화 실기기 검증

[PlayerPrefs 영속화](../PlayerPrefs.md)의 동작 중 CI(Chromium)만으로는 확정할 수 없는 항목을 실기기(토스 앱 WebView)에서 확인하기 위한 런북입니다. `TODO.md`의 "후속 검증" 항목 2건과, 백그라운드 전환 시 PlayerPrefs 자동 flush 여부와 JS 실행 시간 확보 여부 실측이 대상입니다.

> **대상**: SDK 기여자. 아래 버튼들은 E2E 샘플 프로젝트의 Interactive 테스터(PlayerPrefs 서브패널)에 포함되어 있다 — 이 문서 자체는 실측 절차만 정의합니다.

## 준비

Preview 워크플로를 workflow_dispatch로 트리거해 QR 빌드를 만듭니다. REST 호출 예시와 workflow ID는 [GitHub Actions 워크플로](github-actions.md)를 참고하세요. `targets`에는 최소 다음 두 조합을 포함합니다.

```text
macos-6000.2   ← 현재 Unity 6 LTS, 정상 경로 대조군
macos-2021.3   ← 최소 지원 버전, 순정 IDBFS 세션 노화 결함 재현 대상
```

빌드가 끝나면 Job Summary 또는 PR 코멘트에 게시된 QR을 토스 앱에서 스캔해 실기기에 엽니다.

## 절차 A: Storage 크기 상한

목적: 앱인토스 Storage `setItem`의 실제 값 크기 상한 확정. 확정되면 `ait-playerprefs.js`의 `MAX_MANIFEST_CHARS`(현재 512 * 1024, 보수적 추정값) 조정 여부를 판단합니다.

1. 테스터 UI에서 **"Storage 크기 프로브"** 버튼을 실행합니다.
2. 크기 구간별(16KB, 64KB, 128KB, 256KB, 512KB, 1MB) `set`/`get` 왕복의 소요 시간(ms)과 성공 여부를 기록합니다. 각 단계는 30초 타임아웃이며, 타임아웃도 실패로 기록합니다.
3. 실패가 시작되는 크기를 확정합니다.

### 결과 반영 기준

- 실패 시작 크기가 512KB보다 뚜렷이 작다면 `MAX_MANIFEST_CHARS`를 그 아래로 낮춥니다.
- 512KB보다 충분히 크다면(예: 1MB 이상 안정) 현재 값을 유지하거나 상향을 검토합니다. 상향은 보수적으로 — 실기기 기종·OS 편차를 감안해 여유를 둡니다.

## 절차 B: 백그라운드 flush

목적: `PlayerPrefs.Save()` 없이도 백그라운드 전환 시 미러링이 보장되는지 확인(TODO.md P2 항목의 부속 실측).

1. 테스터 UI에서 **"Set (Save 없음)"**을 실행합니다 — `PlayerPrefs.SetString` 등만 호출하고 `Save()`는 호출하지 않습니다.
2. 홈 버튼으로 앱을 백그라운드로 보냈다가 다시 앱으로 복귀합니다.
3. **"백그라운드 로그"**에서 `hidden`/`visible`/`pagehide` 이벤트 발화 여부와 `persistCount` 증가를 확인합니다.
4. 앱을 완전히 종료(태스크 킬)했다가 재실행합니다.
5. `Get`으로 값이 생존했는지 확인합니다.

### 결과 반영 기준

- 4~5단계에서 값이 생존하지 않으면(미보장 판정) C# 측 강제 `PlayerPrefs.Save()` 헬퍼 추가를 후속 과제로 등록합니다(예: `AITVisibilityHelper`의 백그라운드 전환 콜백에 연결).
- 값이 생존하면 이 경로는 별도 조치 없이 종결합니다.

## 절차 C: 2021.3 세션 노화 재현

목적: CI에서 관찰된 "Unity 2021.3 순정 IDBFS가 세션 시작 ~60초 후부터 저장을 통째로 실패한다"는 결함이 실기기에서도 재현되는지, 그리고 이 레이어가 켜져 있으면 회피되는지 확인(TODO.md P2 항목).

**대조군 (레이어 활성 — 정상 케이스 확인용)**

1. 2021.3 빌드를 열고 초기 상태를 확인합니다.
2. 세션 경과 60초 이상 대기합니다(상태줄의 **"세션 경과"** 표시로 확인).
3. Set + Save 후 reload합니다.
4. Get으로 값이 정상 생존하는지 확인합니다.

**본 시나리오 (레이어 비활성 — 결함 재현용)**

1. **"다음 reload부터 레이어 끄기(L3)"**를 실행합니다.
2. reload합니다.
3. **"영속화 status"**에서 `mode=vanilla`(순정 동작)임을 확인합니다.
4. 세션 경과 60초 이상 대기합니다.
5. Set + Save 후 reload합니다.
6. Get으로 값 유실 여부를 확인합니다.
7. 확인이 끝나면 **"L3 해제"**로 되돌립니다.

### 결과 반영 기준

- 본 시나리오에서 유실이 재현되면: (1) CI 관찰과 일치함을 기록, (2) 2021.3 사용자에게 이 기능 opt-out을 비권장한다는 안내를 [PlayerPrefs.md](../PlayerPrefs.md)의 알려진 이슈 절에 반영(사용자 허락 후), (3) Unity 상류 리포트 여부를 판단.
- 재현되지 않으면: CI 전용 환경 요인(예: 특정 Chromium 버전)일 가능성을 기록하고 실기기 영향 없음으로 결론.

## 결과 기록 템플릿

| 절차 | 기기/OS | Unity 버전 | 결과 | 비고 |
|------|---------|-----------|------|------|
| A | | | | 실패 시작 크기: |
| B | | | | 생존 여부: |
| C (대조군) | | | | |
| C (본 시나리오) | | | | |

## 결과 반영 방법

- **A**: `WebGLTemplates/AITTemplate/Runtime/ait-playerprefs.js`의 `MAX_MANIFEST_CHARS` 상수.
- **B**: 필요 시 `Runtime/Helpers/AIT.VisibilityHelper.cs` 부근에 강제 Save 헬퍼 추가.
- **C**: `Documentation~/PlayerPrefs.md`의 알려진 이슈 절, 필요 시 `AITConfigurationWindow.cs`의 툴팁 보강.
- 실측이 완료되어 해당 TODO 항목의 불확실성이 해소되면, `TODO.md`에서 해당 항목을 코드 근거와 함께 통째로 제거합니다(취소선 처리 금지 — 저장소 정책).

## 관련 문서

- [PlayerPrefs 영속화](../PlayerPrefs.md) — 동작 원리와 진단 필드
- [GitHub Actions 워크플로](github-actions.md) — Preview workflow_dispatch 호출 예시
- [테스트 전략](testing.md) — CI가 커버하는 9-x 케이스 요약
