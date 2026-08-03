# Sentry 연동

[Sentry Unity SDK](https://docs.sentry.io/platforms/unity/)를 설치하면 SDK가 크래시·에러 이벤트에 Apps in Toss 플랫폼 컨텍스트를 자동으로 붙여 보냅니다. 이 문서는 그 연동을 켜는 방법과, 자동으로 붙는 값이 정확히 무엇인지 설명합니다.

Sentry SDK를 설치하지 않은 프로젝트에서는 연동 코드가 **아예 컴파일되지 않습니다.** 런타임 오버헤드도 컴파일 에러도 없으므로, 쓰지 않는다면 이 문서를 읽지 않아도 됩니다.

## 설치

`AIT > Install Sentry SDK` 메뉴를 누르면 Package Manager가 Sentry Unity SDK를 설치합니다. 이미 설치돼 있으면 메뉴가 비활성화됩니다.

직접 추가하려면 `Packages/manifest.json`에 씁니다.

```json
{
  "dependencies": {
    "io.sentry.unity": "https://github.com/getsentry/unity.git#4.1.0"
  }
}
```

최소 요구 버전은 `io.sentry.unity` **4.0.0**입니다. 메뉴가 설치하는 버전은 **4.1.0**입니다.

설치 후 `Tools > Sentry`를 열고 DSN을 입력하면 끝입니다. AIT 연동 자체에는 설정할 것이 없습니다. DSN은 Sentry 프로젝트의 `Settings > Client Keys (DSN)`에서 확인합니다. 입력한 값은 `Assets/Resources/Sentry/SentryOptions.asset`에 저장됩니다.

## 자동으로 붙는 컨텍스트

### 태그

| Tag | 출처 | 예시 |
|-----|------|------|
| `ait.sdk_version` | `AITVersion.FullVersion` | `2.4.7` |
| `ait.unity_version` | `Application.unityVersion` | `6000.3.3f1` |
| `ait.commit_hash` | `AITVersion.CommitHash` | `9d42c0b` |
| `ait.current_scene` | 현재 활성 씬 | `MainMenu` |
| `ait.device_id` | `AIT.GetDeviceId` | `abc123...` |
| `ait.platform_os` | `AIT.GetPlatformOS` | `iOS`, `Android` |
| `ait.locale` | `AIT.GetLocale` | `ko-KR` |
| `ait.toss_app_version` | `AIT.GetTossAppVersion` | `5.80.0` |
| `ait.environment` | `AIT.GetOperationalEnvironment` | `production`, `staging` |
| `ait.deployment_id` | `AIT.EnvGetDeploymentId` | `deploy-xyz` |

앞의 넷은 동기적으로 즉시 설정되고, `ait.device_id` 이하 여섯은 플랫폼 API를 비동기 호출해 채웁니다.

`ait.commit_hash`는 커밋 해시를 확인할 수 없는 빌드에서는 **아예 설정되지 않습니다.** 나머지 플랫폼 태그도 값을 얻지 못하면 태그를 붙이지 않습니다 — 값이 없다는 것과 `unavailable`이라는 문자열이 값으로 들어간 것은 다르며, 여기서는 전자입니다.

`ait.current_scene`은 씬이 로드될 때마다 갱신되므로 이벤트 발생 시점의 씬을 가리킵니다.

### User

기기 ID를 얻은 경우에 한해 `User.Id`에 그 값을 넣습니다. 기기 ID를 얻지 못하면 `User`를 건드리지 않습니다.

### 컨텍스트 오브젝트

`apps_in_toss` 이름의 커스텀 컨텍스트가 추가됩니다.

```json
{
  "sdk_version": "2.4.7",
  "unity_version": "6000.3.3f1",
  "device_id": "abc123...",
  "platform_os": "iOS",
  "locale": "ko-KR",
  "toss_app_version": "5.80.0",
  "environment": "production",
  "deployment_id": "deploy-xyz"
}
```

태그와 달리 이 오브젝트는 **값을 얻지 못한 항목도 `unavailable` 문자열로 채워 넣습니다.** 어떤 API가 실패했는지 이벤트에서 바로 읽을 수 있게 하기 위해서입니다. 태그에는 없는 커밋 해시가 여기에도 없고, 태그에만 있는 `current_scene`도 여기에는 없습니다.

### Breadcrumb

씬이 로드될 때마다 breadcrumb가 기록됩니다.

| 필드 | 값 |
|------|-----|
| message | `Scene loaded: MainMenu` |
| category | `scene` |
| level | `Info` |
| data | `scene_name`, `scene_build_index`, `load_mode` |

## Analytics 연동

`AITSentryAnalytics`는 Analytics API 호출을 Sentry breadcrumb으로 함께 남기는 래퍼입니다. `AIT.AnalyticsScreen`을 직접 부르는 대신 이쪽을 부르면, 같은 호출이 Sentry 이벤트의 맥락으로도 남습니다.

```csharp
using AppsInToss.Sentry;

// AIT.AnalyticsScreen 호출 + Sentry breadcrumb 기록
await AITSentryAnalytics.TrackScreen(new { screen_name = "MainMenu" });
await AITSentryAnalytics.TrackImpression(new { item_id = "banner_1" });
await AITSentryAnalytics.TrackClick(new { button = "start" });
```

씬 전환마다 자동으로 화면을 기록하게 하려면 플래그 하나를 켭니다.

```csharp
AITSentryAnalytics.AutoScreenTrackingEnabled = true;
```

켜면 `SceneManager.sceneLoaded`에서 `TrackScreen(new { screen_name = 씬이름 })`이 자동 호출됩니다. 이 경우 씬 하나가 로드될 때 breadcrumb가 **두 개** 남습니다 — 위의 `scene` breadcrumb과 여기서 나오는 `analytics` breadcrumb입니다.

호출 누계는 `ait_analytics` 컨텍스트 오브젝트로도 이벤트에 붙습니다.

| 필드 | 설명 |
|------|------|
| `screen_count` / `impression_count` / `click_count` | 종류별 누적 호출 수 |
| `last_screen` | 마지막으로 화면을 기록한 씬 이름 (없으면 `none`) |
| `auto_tracking` | `AutoScreenTrackingEnabled` 현재 값 |

> **참고**: `AIT` 본체 API와 마찬가지로 반환형이 Unity 버전에 따라 갈립니다. Unity 6 이상에서는 `Awaitable`, 그 이하에서는 `Task`입니다. 자세한 내용은 [API 사용 패턴](APIUsagePatterns.md)을 참고하세요.

## CI 환경 변수

### 빌드 시 DSN 주입

WebGL은 브라우저 샌드박스라 런타임에 환경 변수를 읽을 수 없습니다. 그래서 `AITSentryDsnInjector`가 빌드 전처리 단계에서 환경 변수를 읽어 `SentryOptions.asset`으로 구워 넣습니다.

| 변수 | 용도 | 예시 |
|------|------|------|
| `SENTRY_DSN` | DSN. 이 값이 없으면 주입 자체를 건너뜀 | `https://key@sentry.io/123` |
| `SENTRY_ENVIRONMENT` | environment 강제 지정 (선택) | `production`, `staging` |
| `SENTRY_RELEASE` | release 강제 지정 (선택) | `my-app@1.0.0` |

주입은 **WebGL 빌드에서만** 동작하고, `SentryOptions.asset`이 이미 있으면 사용자 설정을 보호하기 위해 건너뜁니다. 즉 이 경로는 asset이 없는 CI 체크아웃에서만 실제로 파일을 만듭니다.

### environment 와 release 자동 파생

`SENTRY_ENVIRONMENT` / `SENTRY_RELEASE`를 주지 않으면 `AITSentryReleaseResolver`가 SDK 버전에서 두 값을 파생합니다. Sentry의 `environment`/`release`는 초기화 시점 전용 옵션이라 런타임 scope로 바꿀 수 없어서, 빌드 시점에 구워 넣는 것 말고는 방법이 없습니다.

| SDK 버전 | environment | release |
|----------|-------------|---------|
| stable | *(미설정 → Sentry 기본값 `production`)* | `apps-in-toss.unity@{버전}` |
| prerelease | `beta` | `apps-in-toss.unity@{버전}` |
| unknown | *(미설정)* | *(미설정)* |

- **목적**: 베타 파일럿 빌드의 에러가 `environment:beta`로 분리되어 stable triage·알림·release-health를 오염시키지 않습니다. stable 빌드는 environment를 설정하지 않으므로 기존 동작이 그대로 유지됩니다.
- **우선순위**: 명시된 환경 변수가 있으면 **항상 자동 파생을 이깁니다.**
- **release 정합**: 파생된 release는 릴리즈 워크플로가 만드는 Sentry release 식별자와 같은 규칙을 쓰므로, release-health와 `Fixes` trailer 기반 auto-resolve 연결이 맞아떨어집니다.

SDK 버전을 알 수 없으면 둘 다 구워 넣지 않고 경고를 남깁니다. 이때 Sentry는 기본값을 쓰므로, prerelease 빌드였다면 이벤트가 stable triage로 흘러갈 수 있습니다.

### sentry-cli

디버그 심볼과 소스맵 업로드에 쓰는 값들입니다. SDK가 읽는 것이 아니라 CLI가 읽습니다.

| 변수 | 용도 | 예시 |
|------|------|------|
| `SENTRY_AUTH_TOKEN` | API 인증 토큰 | `sntrys_...` |
| `SENTRY_ORG` | 조직 slug | `my-org` |
| `SENTRY_PROJECT` | 프로젝트 slug | `unity-game` |
| `SENTRY_URL` | 자체 호스팅 Sentry URL (선택) | `https://sentry.mycompany.com` |
| `SENTRY_LOG_LEVEL` | CLI 로그 수준 (선택) | `info`, `debug` |

```yaml
env:
  SENTRY_AUTH_TOKEN: ${{ secrets.SENTRY_AUTH_TOKEN }}
  SENTRY_ORG: my-org
  SENTRY_PROJECT: unity-game
```

## 동작 원리

### 조건부 컴파일

Sentry 연동 어셈블리는 `AIT_SENTRY_AVAILABLE` define이 있을 때만 컴파일됩니다.

1. `io.sentry.unity` 4.0.0 이상이 설치되면 `versionDefines`가 `AIT_SENTRY_AVAILABLE`을 자동으로 켭니다.
2. `AppsInToss.Sentry`와 `AppsInToss.Sentry.Editor`의 `defineConstraints`가 이 define을 요구합니다.
3. Sentry SDK가 없으면 두 어셈블리가 통째로 컴파일에서 빠집니다.

### 자동 초기화

`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 초기화됩니다. Sentry SDK와 SDK 본체가 모두 `BeforeSceneLoad`에서 초기화되므로, 그 뒤인 `AfterSceneLoad`가 양쪽에 안전하게 접근할 수 있는 첫 시점입니다.

1. `SentrySdk.IsEnabled`로 Sentry 활성 여부 확인 — 꺼져 있으면 여기서 끝
2. 버전·커밋 해시 태그 설정
3. 씬 로드 이벤트 구독
4. 플랫폼 API를 비동기 호출해 나머지 컨텍스트 수집
5. Analytics 연동 초기화

4번은 fire-and-forget이라 각 API가 독립적으로 실패합니다. 하나가 실패해도 나머지 컨텍스트는 정상적으로 붙습니다.

### IL2CPP 스트리핑 보호

WebGL(IL2CPP) 빌드에서 연동 코드가 통째로 사라지지 않도록 세 겹으로 막습니다.

| 보호 수단 | 역할 |
|-----------|------|
| `[assembly: AlwaysLinkAssembly]` | 어셈블리 자체가 링커에서 제거되는 것 방지 |
| `[Preserve]` | 개별 타입·메서드 보존 |
| `link.xml` | 어셈블리 내 모든 타입 보존 선언 |

`AlwaysLinkAssembly`가 핵심입니다. 이 어셈블리를 참조하는 다른 어셈블리가 없어서, 이 어트리뷰트가 없으면 IL2CPP 링커가 "아무도 안 쓰는 어셈블리"로 판단해 통째로 제거합니다.

### Unity 6 이상의 스택트레이스 정밀도

Unity 6 이상에서 WebGL 빌드를 하면 `AITSentryBuildProcessor`가 IL2CPP 스택트레이스에 C# 파일·라인 정보를 켭니다.

```csharp
PlayerSettings.SetIl2CppStacktraceInformation(WebGL, MethodFileLineNumber)
```

덕분에 Sentry에서 크래시 위치를 정확한 소스 라인으로 볼 수 있습니다. Unity 2021.3/2022.3에는 이 API가 없어 자동으로 건너뛰고, 설정에 실패해도 빌드는 계속됩니다.

## 문제 해결

### 이벤트가 전송되지 않음

Console에 다음 로그가 있으면 Sentry SDK 자체가 비활성 상태입니다. AIT 연동 문제가 아니라 DSN 문제입니다.

```text
[AITSentry] Sentry SDK가 비활성 상태입니다. AIT 컨텍스트 연동을 건너뜁니다. (DSN 설정 확인: Tools > Sentry)
```

정상적으로 붙었다면 이 로그가 나옵니다.

```text
[AITSentry] Initialized - AIT 컨텍스트가 Sentry 이벤트에 자동으로 추가됩니다.
```

CI 빌드라면 빌드 로그에서 `SentryOptions.asset을 생성했습니다`로 시작하는 줄을 확인하세요. 바로 아래에 마스킹된 DSN과 자동 파생된 Environment·Release가 함께 찍힙니다. 이 줄이 없다면 `SENTRY_DSN`이 비었거나 asset이 이미 존재해 주입을 건너뛴 것입니다.

### IL2CPP 빌드에서 AIT 태그가 없음

스트리핑으로 연동 코드가 제거된 경우입니다. 보존 선언은 SDK가 `Runtime/Sentry/link.xml`로 이미 함께 배포하므로 **직접 추가할 필요가 없습니다.** 그래도 태그가 없다면 대개 빌드 캐시가 원인입니다.

`Library/Bee/artifacts/WebGL/`를 삭제하고 클린 빌드하세요. 캐시된 결과에는 `link.xml` 변경이 반영되지 않습니다.

프로젝트 쪽에서 스트리핑 설정을 따로 손댔다면 `Assets/link.xml`에 같은 선언을 더해 보강할 수 있습니다.

```xml
<linker>
    <assembly fullname="AppsInToss.Sentry" preserve="all"/>
</linker>
```

### 컨텍스트 일부가 unavailable

플랫폼 API 호출이 실패한 경우입니다. 재시도하지 않고 `unavailable`로 확정합니다.

Mock 브릿지 환경에서는 일부 API가 지원되지 않아 이 값이 나오는 것이 정상입니다. 이 경우 태그에는 해당 항목이 아예 없고, `apps_in_toss` 컨텍스트에만 `unavailable`로 남습니다.

## 관련 문서

- [SDK 이벤트 로깅](Metrics.md) — SDK가 자동 수집하는 런타임 이벤트
- [시작하기](GettingStarted.md) — SDK 설치와 기본 설정
- [문제 해결](Troubleshooting.md) — 일반 문제 해결
- [Sentry Unity SDK 문서](https://docs.sentry.io/platforms/unity/) — Sentry 공식 문서
