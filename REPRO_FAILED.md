# 재현 실패 / 원인 미규명: APPS-IN-TOSS-UNITY-SDK-8C

## 이슈
- 제목: `UnityWarning: [AIT] web-framework가 node_modules에 없습니다. node_modules를 정리합니다.`
- 분류 사유: SDK 자체 경고이며 web-framework 누락 시 node_modules 정리 동작은 SDK 로직 이슈
- 재현 단서: web-framework 미설치 감지 후 node_modules 정리 로직 재현 및 의도 검토

## 처리 상태
**Sentry 이슈 status: `ignored (forever)` 처리 완료** (2026-05-06).
신규 이벤트가 들어와도 자동 재오픈되지 않음, 카운트만 증가.

코드 변경 없이 worktree에서 멈춤(절차 5). PR 생성 안 함.

## 핵심 발견: 코드와 Sentry 데이터가 모순

### v2.4.7 기준 코드 분석
이 메시지를 출력하는 유일한 코드 경로는 `Editor/Package/NodeModulesValidator.cs:83`:
```csharp
Debug.Log($"[AIT] web-framework가 node_modules에 없습니다. node_modules를 정리합니다.");
```
- 저장소 전체 grep(`*.cs/*.ts/*.js/*.mjs/*.cjs`) 단일 호출 지점
- v2.4.6/v2.4.7 모두 이미 `Debug.Log` (PR #448 / commit `44f292d` 적용됨)

`Editor/ErrorTracker/AITEditorErrorTracker.cs:307`의 `OnLogMessageReceived`:
```csharp
if (type != LogType.Error && type != LogType.Exception && type != LogType.Warning)
    return;
```
→ `LogType.Log`는 명시적으로 거름. 정상 경로로는 Sentry 도달 불가능.

### Sentry 실제 데이터 (MCP로 조회)
| Release | 건수 | 기간 |
|---|---|---|
| 2.4.6 | 55 | 2026-04-16 ~ 04-27 |
| **2.4.7** | **40** | **2026-04-20 ~ 04-26** |
| 2.0.5 | 7 | 2026-04-22 ~ 05-02 |
| 2.4.0 | 2 | 2026-04-17 ~ 04-18 |

이벤트 태그 (event `9098edb5...`): `level=warning, type=UnityWarning, error_source=sdk, stacktrace=No stacktrace available`.

→ **v2.4.7 사용자(Debug.Log 적용)에서도 95건이 LogType.Warning으로 캡처되어 Sentry에 도달함**. 코드상 도달 경로가 없는데 도달했다는 모순.

## 미규명: LogType 변형 메커니즘
v2.4.7 코드를 정적 분석으로는 `Debug.Log` → Sentry warning 도달 경로 발견 불가.
가능한 가설(검증 안 됨):
1. 다른 ILogHandler/`logMessageReceived` 핸들러가 LogType을 변형
2. Unity Editor가 빌드 컨텍스트에서 Debug.Log를 Warning으로 격상하는 케이스
3. Domain Reload 캐시로 stale assembly의 이전 `LogWarning` 코드가 실행됨
4. Sentry envelope의 `level`을 다른 캡처 경로가 Warning으로 셋팅
5. grep이 놓친 출력 경로 (예: 동적 string 생성, 네이티브 Log API)

이 중 어느 것도 코드 레벨에서 단정 불가. 따라서 추측 기반 fix 금지 규칙 적용.

## 시도한 분석

### 1. 메시지 출처 (재확인)
유일 출처: `Editor/Package/NodeModulesValidator.cs:83` (Debug.Log).
저장소 전체 grep 결과 동일 텍스트 다른 호출 지점 없음.

### 2. 트리거 조건과 의도
`ValidateIntegrity()`는 best-effort 무결성 판단:
- `node_modules` 없음 → `true` (재설치할 것이므로 OK)
- `package.json` 없음/파싱 실패/`dependencies` 없음/version 빈 문자열 → `true`
- `@apps-in-toss/web-framework`가 dependencies에 없음 → `true`
- `node_modules/.pnpm` 없음 → `false` (오염 상태로 판단, 메시지 출력)
- `.pnpm/`에 `@apps-in-toss+web-framework@{version}*` 매칭 → `true`
- **`.pnpm/`에 web-framework 자체가 없음 → `false` + 본 경고 출력** ← 이 케이스
- `.pnpm/`에 web-framework는 있지만 버전 불일치 → `false` + 별도 경고 4줄 출력

본 경고가 출력되는 정확한 사용자 시나리오: `node_modules/.pnpm/`은 존재하지만
`@apps-in-toss+web-framework@*` 디렉토리가 없는 상태. 사용자 환경 이슈
(부분 install 중단/디스크 공간/캐시 외부 변조 등)이며, SDK는 자동
`CleanNodeModules` 후 재설치로 self-heal한다.

### 3. 로직 의도 검토
의도한 동작에 결함 없음:
- 검증은 best-effort 계약(판별 불가는 모두 `true` 반환)을 명확히 지킨다.
- specifier `^`/`~` prefix trim 처리됨(line 54).
- `WebGLTemplates/AITTemplate/BuildConfig~/package.json`는 고정 버전
  `"@apps-in-toss/web-framework": "2.4.7"` — range/alias/git URL specifier 사용 안 함.
- 호출자 `Editor/AITPackageBuilder.cs:128, 186`에서 `false` 반환 시
  `CleanNodeModules`만 호출하고 빌드는 계속됨 — destructive 아님.

### 4. Sentry 캡처 경로 검증 (코드 grep)
- `Application.logMessageReceived` 핸들러: 2개 (`AITEditorErrorTracker.OnLogMessageReceived`,
  `AITErrorReporter.CaptureLog`). 후자는 단순 버퍼링이고 Sentry 전송 없음.
- 전자는 line 307에서 LogType.Log 거름.
- 다른 ILogHandler 교체/`unityLogger.logHandler` 변경 코드 없음.

### 5. Sentry 데이터 검증 (MCP)
- v2.4.7에서 40건, v2.4.6에서 55건이 `level=warning`으로 캡처됨.
- 이벤트 태그 `error_source=sdk` — `DetermineErrorSource`가 SDK로 분류함.
- 스택트레이스는 모든 이벤트에 없음 (`No stacktrace available`).

### 6. 재현 시도
EditMode 단위 테스트(`Tests~/E2E/SharedScripts/Editor/EditModeTests/Package/NodeModulesValidatorTests.cs`)에
이미 시나리오 커버 완료:
- `ValidateIntegrity_ReturnsFalse_WhenPnpmDirHasOtherPackagesOnly` (line 126)
- `ValidateIntegrity_ReturnsFalse_WhenPnpmDirMissing` (line 56)

Debug.Log → Sentry Warning 변형은 EditMode/로컬 E2E에서 재현 불가
(`logMessageReceived`는 발생한 LogType을 그대로 전달하는 게 정상 동작).

## 후속 조사를 위해 필요한 정보
1. **사용자 측 raw Unity Console 로그** — 텍스트 그대로 + LogType 색상(노란/빨강/회색)
   - 노란색(Warning)으로 보인다면 Debug.LogWarning이 어딘가에서 호출되고 있음 → 누가?
   - 회색(Log)으로 보이는데 Sentry에는 Warning으로 들어온다면 → ErrorTracker 외부 캡처 경로 존재
2. **사용자 환경의 다른 Sentry SDK/로깅 플러그인 설치 여부**
   - Unity Sentry SDK가 별도로 설치돼 있으면 Debug.Log를 자체적으로 캡처 가능
3. **같은 빌드 세션의 다른 Sentry 이벤트** — breadcrumb으로 LogType 변형 시점 추적
4. **Domain Reload 직전에 출력된 메시지인지** — stale assembly로 v2.4.6 이전 `LogWarning`
   코드가 실행됐을 가능성 검증 (버전 태그가 v2.4.7이어도 실제 실행 코드는 다를 수 있음)

## 권장 후속 조치
1. ✅ Sentry 이슈 ignored (forever) 처리 완료
2. 본 메시지의 Sentry 재유입을 monitoring하다가 새 이벤트가 들어오면 위 §"필요한 정보" 항목들을
   확보해서 별도 PR에서 근본 원인 추적
3. (선택) 단기 노이즈 차단을 위해 `Editor/ErrorTracker/AITEditorErrorTracker.cs`의
   `NonSdkMessagePatterns` 또는 별도 "SDK 정상 흐름" 패턴 배열에 본 메시지 핵심 문구를 등록 —
   단, 이는 근본 원인을 가리는 패치이므로 §1 monitoring과 함께 진행해야 함.
   현재는 Sentry 이슈가 ignored 상태이므로 코드 변경 불필요.
