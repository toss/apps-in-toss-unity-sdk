# Sentry 통합

AIT SDK는 [Sentry Unity SDK](https://docs.sentry.io/platforms/unity/)와 자동 통합을 지원합니다. Sentry가 설치되어 있으면 크래시 및 에러 이벤트에 AIT 플랫폼 컨텍스트(기기 ID, 환경, 배포 ID 등)를 자동으로 주입합니다.

**제로코스트 opt-in**: Sentry SDK가 설치되지 않은 프로젝트에서는 관련 코드가 아예 컴파일되지 않습니다. 런타임 오버헤드도, 컴파일 에러도 없습니다.

## 목차

- [설치](#설치)
- [설정](#설정)
- [자동 주입되는 AIT 컨텍스트](#자동-주입되는-ait-컨텍스트)
- [CI/CD 환경변수](#cicd-환경변수)
- [동작 원리](#동작-원리)
- [트러블슈팅](#트러블슈팅)

---

## 설치

### 방법 1: Unity 메뉴 (권장)

1. Unity Editor에서 `AIT` > `Install Sentry SDK` 클릭
2. Package Manager가 자동으로 Sentry Unity SDK 4.1.0을 설치합니다

> 이미 Sentry SDK가 설치된 경우 메뉴가 비활성화됩니다.

### 방법 2: manifest.json 직접 추가

`Packages/manifest.json`에 직접 추가:

```json
{
  "dependencies": {
    "io.sentry.unity": "https://github.com/getsentry/unity.git#4.1.0"
  }
}
```

### 지원 버전

- **최소**: `io.sentry.unity` 4.0.0
- **권장**: 4.1.0 이상

---

## 설정

Sentry SDK 자체 설정만 필요합니다. AIT 통합은 별도 설정 없이 자동으로 동작합니다.

### DSN 설정

Unity Editor에서 `Tools` > `Sentry`를 열고 **DSN**을 입력합니다.

> DSN은 Sentry 프로젝트의 `Settings > Client Keys (DSN)`에서 확인할 수 있습니다.

설정값은 `Assets/Resources/Sentry/SentryOptions.asset`에 저장됩니다.

---

## 자동 주입되는 AIT 컨텍스트

Sentry SDK가 활성 상태이면, AIT SDK가 다음 컨텍스트를 자동으로 주입합니다.

### Tags

| Tag | 설명 | 예시 |
|-----|------|------|
| `ait.sdk_version` | AIT SDK 버전 | `1.11.2` |
| `ait.unity_version` | Unity 엔진 버전 | `6000.3.0f1` |
| `ait.device_id` | 기기 고유 ID | `abc123...` |
| `ait.platform_os` | 플랫폼 OS | `iOS`, `Android` |
| `ait.locale` | 기기 로캘 | `ko-KR` |
| `ait.toss_app_version` | 토스 앱 버전 | `5.80.0` |
| `ait.environment` | 운영 환경 | `production`, `staging` |
| `ait.deployment_id` | 배포 ID | `deploy-xyz` |
| `ait.current_scene` | 현재 Unity 씬 | `MainMenu` |

### User

| 필드 | 값 |
|------|-----|
| `User.Id` | AIT 기기 ID (`AIT.GetDeviceId()`) |

### Context Object

`apps_in_toss` 이름의 커스텀 컨텍스트 오브젝트가 추가됩니다:

```json
{
  "sdk_version": "1.11.2",
  "unity_version": "6000.3.0f1",
  "device_id": "abc123...",
  "platform_os": "iOS",
  "locale": "ko-KR",
  "toss_app_version": "5.80.0",
  "environment": "production",
  "deployment_id": "deploy-xyz"
}
```

### Breadcrumbs

Unity 씬이 로드될 때마다 breadcrumb가 자동 기록됩니다:

| 필드 | 값 |
|------|-----|
| message | `Scene loaded: MainMenu` |
| category | `scene` |
| level | `Info` |
| data | `scene_name`, `scene_build_index`, `load_mode` |

---

## CI/CD 환경변수

### Sentry SDK 핵심 (빌드타임 자동 주입)

AIT SDK는 WebGL 빌드 시 `SENTRY_DSN` 환경변수에서 `SentryOptions.asset`을 자동 생성합니다. 브라우저 샌드박스에서는 런타임에 환경변수를 읽을 수 없으므로, 빌드 시점에 asset으로 bake하는 방식입니다.

| 변수 | 용도 | 예시 |
|------|------|------|
| `SENTRY_DSN` | DSN → `SentryOptions.asset` 자동 생성 | `https://key@sentry.io/123` |
| `SENTRY_ENVIRONMENT` | 환경 식별자 (자동 주입) | `production`, `staging` |
| `SENTRY_RELEASE` | 릴리즈 버전 (자동 주입) | `my-app@1.0.0` |

> **자동 주입 동작**: WebGL 빌드 시 `AITSentryDsnInjector`(`IPreprocessBuildWithReport`, callbackOrder=0)가 `SENTRY_DSN` 환경변수를 감지하면 `SentryOptions.asset`을 Unity API로 생성합니다. 이미 asset이 존재하면 사용자 설정을 보호하기 위해 건너뜁니다.

### sentry-cli (빌드 타임)

디버그 심볼 및 소스맵 업로드 시 사용합니다. CI/CD 파이프라인에서 설정하세요.

| 변수 | 용도 | 예시 |
|------|------|------|
| `SENTRY_AUTH_TOKEN` | API 인증 토큰 | `sntrys_...` |
| `SENTRY_ORG` | 조직 slug | `my-org` |
| `SENTRY_PROJECT` | 프로젝트 slug | `unity-game` |
| `SENTRY_URL` | 자체 호스팅 Sentry URL (선택) | `https://sentry.mycompany.com` |
| `SENTRY_LOG_LEVEL` | CLI 로그 수준 (선택) | `info`, `debug` |

### CI/CD 파이프라인 예시

```yaml
# GitHub Actions
env:
  SENTRY_AUTH_TOKEN: ${{ secrets.SENTRY_AUTH_TOKEN }}
  SENTRY_ORG: my-org
  SENTRY_PROJECT: unity-game
```

---

## 동작 원리

### 조건부 컴파일

Sentry 통합은 Unity의 `versionDefines`를 활용한 조건부 컴파일로 구현됩니다:

1. `io.sentry.unity` 4.0.0 이상이 설치되면 `AIT_SENTRY_AVAILABLE` define이 자동 활성화
2. `AppsInToss.Sentry` 어셈블리의 `defineConstraints`에 `AIT_SENTRY_AVAILABLE`이 설정됨
3. Sentry SDK 미설치 시 어셈블리 전체가 컴파일에서 제외됨

### 자동 초기화

`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 어트리뷰트로 게임 시작 시 자동 초기화됩니다:

1. Sentry 활성 상태 확인 (`SentrySdk.IsEnabled`)
2. SDK/Unity 버전 태그 설정
3. 씬 로드 이벤트 구독 (breadcrumb 기록용)
4. AIT 플랫폼 API를 비동기로 호출하여 컨텍스트 수집

### IL2CPP 스트리핑 보호

WebGL(IL2CPP) 빌드에서 코드가 제거되지 않도록 3중 보호가 적용됩니다:

| 보호 수단 | 역할 |
|-----------|------|
| `[assembly: AlwaysLinkAssembly]` | 어셈블리 자체가 링커에서 제거되는 것 방지 |
| `[Preserve]` | 개별 타입/메서드 보존 |
| `link.xml` | 어셈블리 내 모든 타입 보존 선언 |

> `AlwaysLinkAssembly`가 핵심입니다. 이 어셈블리를 직접 참조하는 다른 어셈블리가 없기 때문에, 이 어트리뷰트 없이는 IL2CPP 링커가 어셈블리를 통째로 제거합니다.

### Unity 6+ IL2CPP 스택트레이스

Unity 6 이상에서는 WebGL 빌드 시 `AITSentryBuildProcessor`가 IL2CPP 스택트레이스에 C# 파일/라인 정보를 자동으로 활성화합니다:

```
PlayerSettings.SetIl2CppStacktraceInformation(WebGL, MethodFileLineNumber)
```

이를 통해 Sentry에서 크래시 위치를 정확한 소스 코드 라인으로 확인할 수 있습니다. Unity 2021.3/2022.3에서는 이 API가 없으므로 자동으로 건너뜁니다.

---

## 트러블슈팅

### Sentry 이벤트가 전송되지 않음

1. **DSN 확인**: `Tools > Sentry`에서 DSN이 올바르게 설정되어 있는지 확인
2. **로그 확인**: 콘솔에 `[AIT:Sentry] Sentry is not enabled` 메시지가 있으면 Sentry SDK가 비활성 상태
3. **WebGL CI/CD**: `SENTRY_DSN` 환경변수를 설정하면 빌드 시 `SentryOptions.asset`이 자동 생성됩니다. 빌드 로그에서 `[AITSentry] SentryOptions.asset을 환경변수에서 생성했습니다`를 확인하세요.

### IL2CPP 빌드에서 AIT 태그가 없음

IL2CPP 스트리핑으로 통합 코드가 제거되었을 수 있습니다:

1. `Assets/link.xml`에 다음 내용이 포함되어 있는지 확인:

```xml
<linker>
    <assembly fullname="AppsInToss.Sentry" preserve="all"/>
</linker>
```

2. `Library/Bee/artifacts/WebGL/` 폴더를 삭제하고 클린 빌드 실행 (캐시된 결과에는 link.xml 변경이 반영되지 않음)

### AIT 컨텍스트 일부 값이 `unavailable`

AIT 플랫폼 API 호출이 실패한 경우입니다. 각 API는 독립적으로 실패하며, 나머지 컨텍스트는 정상 주입됩니다.

- Mock 브릿지 환경에서는 일부 API가 지원되지 않을 수 있음
- 네트워크 타임아웃인 경우 재시도 없이 `unavailable`로 설정됨

---

## 다음 단계

- [시작하기](GettingStarted.md) - SDK 설치 및 기본 설정
- [문제 해결](Troubleshooting.md) - 일반 트러블슈팅
- [Sentry Unity SDK 문서](https://docs.sentry.io/platforms/unity/) - Sentry 공식 문서
