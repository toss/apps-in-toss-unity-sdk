# 시작하기

SDK를 설치하고 첫 빌드를 띄우기까지 필요한 것만 순서대로 담았습니다.

Apps in Toss Unity SDK를 사용하면 별도의 Vite 프로젝트 구성이나 JS Bridge 구현 없이 Unity 프로젝트를 미니앱으로 포팅할 수 있습니다. 로딩 화면이 SDK에 기본 포함되어 있고, 첫 상호작용까지 걸린 시간, 프레임 스톨, 에러·예외, 메모리 경고 같은 런타임 이벤트가 사용자 코드 없이 자동으로 수집됩니다(자세한 내용은 [SDK 이벤트 로깅](Metrics.md) 참고).

## SDK 설치

### Package Manager로 설치

1. Unity Editor에서 `Window` > `Package Manager` 열기
2. 왼쪽 상단 `+` 버튼 클릭
3. `Add package from git URL...` 선택
4. Git URL 입력:

```
https://github.com/toss/apps-in-toss-unity-sdk.git#release/v3.0.1
```

### manifest.json 직접 수정

프로젝트의 `Packages/manifest.json`에 의존성을 추가합니다.

```json
{
  "dependencies": {
    "im.toss.apps-in-toss-unity-sdk": "https://github.com/toss/apps-in-toss-unity-sdk.git#release/v3.0.1"
  }
}
```

### 지원 Unity 버전

최소 Unity 2021.3이 필요하고, Unity 6 이상을 권장합니다. 2021.3 이후의 모든 버전을 지원합니다.

## SDK 구성

Apps in Toss Unity SDK는 WebGL 환경에서 플랫폼 API를 쓸 수 있도록 두 계층을 함께 제공합니다.

- **C# API Layer** (`Runtime/SDK/`) — `AIT.*` 형태의 C# 메서드로 플랫폼 API를 감쌉니다. 내부적으로 `DllImport("__Internal")`을 사용해 WebGL 빌드 시 JS 함수와 연결됩니다.
- **JS Bridge** (`.jslib`) — C#에서 호출하는 JS 함수가 정의되어 있고, 실제 Apps in Toss WebView SDK와 통신하는 로직이 여기에 있습니다.

두 계층 모두 SDK에 이미 포함되어 있어 직접 작성할 코드는 없습니다.

## 설치 ref 관리

URL 끝의 `#...` 부분이 **설치 ref**입니다. UPM은 이 ref가 가리키는 커밋을 그대로 가져오므로, 여기에 무엇을 적느냐가 곧 "언제 어떻게 업데이트되는가"를 결정합니다.

### ref 고르기

| ref 형태 | 예 | 동작 |
|------|------|------|
| 불변 릴리즈 태그 | `#release/vX.Y.Z` | 특정 커밋에 영구 고정. 재현 가능한 빌드를 보장하고 의도치 않은 업데이트로부터 격리됨 |
| 브랜치 | `#main` | HEAD가 이동할 때마다 자동 업데이터가 변경을 감지해 업데이트 프롬프트를 표시 |
| prerelease 채널 | `#beta`, `#beta-perf` | 이동 브랜치. 자동 업데이트 프롬프트가 뜨지 않아 직접 관리해야 함 |

> **권장**: 서비스 배포에는 불변 릴리즈 태그를 쓰세요. 사용 가능한 태그는 [GitHub Releases](https://github.com/toss/apps-in-toss-unity-sdk/releases)에서 확인할 수 있습니다.

prerelease 채널은 사전 협의된 파일럿 대상에게만 안내됩니다. [베타 채널](BetaChannel.md)과 [perf 베타 채널](PerfBetaChannel.md)을 참고하세요.

### 이동하는 ref를 최신으로 다시 당겨오기

UPM은 git 의존성을 `Packages/packages-lock.json`에 **커밋 해시로 잠급니다.** 그래서 `#main`처럼 이동하는 ref로 핀했더라도 Unity를 다시 여는 것만으로는 갱신되지 않습니다. 둘 중 하나로 잠금을 풀어야 합니다.

- **Package Manager에서 제거 후 재추가** — 패키지를 remove하고 같은 URL로 다시 add하면 ref가 재해석됩니다. 가장 간단합니다.
- **lock 해제** — `Packages/packages-lock.json`에서 `im.toss.apps-in-toss-unity-sdk` 항목의 `"hash"` 값을 지우고 저장하면 Unity가 ref를 다시 해석합니다.

### 다른 ref로 옮기기

`Packages/manifest.json`에서 URL의 fragment만 바꾸고 저장합니다. 의존성 문자열이 달라지면 UPM이 패키지를 처음부터 다시 resolve하므로, 이 경우에는 위의 잠금 해제가 필요하지 않습니다.

- 파일럿 참여: `#release/vX.Y.Z` → `#beta` 또는 `#beta-perf`
- stable로 복귀: `#beta` → `#release/vX.Y.Z`

불변 릴리즈 태그로 되돌리면 자동 업데이터가 다시 해당 stable ref를 추적합니다.

## 설정

SDK 설치 후 Unity Editor 메뉴에서 `AIT` > `Configuration`을 클릭해 설정 창을 엽니다.

| 설정 | 설명 |
|------|------|
| **앱 ID** | Apps in Toss 플랫폼에서 발급받은 앱 ID. 영문·숫자·하이픈만 사용할 수 있으며, 설정 창에서 `*`로 표시되는 유일한 필수 항목입니다 |
| **표시 이름** | 로딩 화면에 표시될 앱 이름 |
| **버전** | `x.y.z` 형식 |
| **기본 색상** | 브랜드 색상. 진행률 바 등에 사용됩니다 |
| **아이콘 URL** | 미니앱 아이콘으로 표시될 이미지 URL. 입력할 경우 `http://` 또는 `https://`로 시작해야 합니다 |

## AIT 메뉴

SDK 설치가 끝나면 Unity Editor 상단에 `AIT` 메뉴가 추가됩니다.

| 메뉴 | 설명 |
|------|------|
| **Dev Server** | 하위에 Start / Stop / Restart Server / Restart Server (server-only)가 있습니다. `server-only`는 재빌드 없이 서버만 재시작합니다 |
| **Production Server** | 하위에 Start / Stop / Restart Server / Restart Server (server-only)가 있습니다. `server-only`는 재빌드 없이 서버만 재시작합니다 |
| **Build & Package** | WebGL 빌드와 `.ait` 패키징을 한 번에 실행합니다 |
| **Publish** | 생성된 `.ait` 파일을 Apps in Toss 플랫폼으로 업로드합니다. `Configuration`에 배포 키가 설정되어 있어야 합니다 |
| **Clean** | `webgl/`, `ait-build/` 빌드 산출물 폴더를 삭제합니다 |
| **Open Build Output** | 빌드 산출물이 저장된 폴더를 엽니다 |
| **Reset Loading Screen** | 로딩 화면을 SDK 기본 템플릿으로 되돌립니다. 자세한 내용은 [로딩 화면 커스터마이징](LoadingScreenCustomization.md) 참고 |
| **Configuration** | 앱 ID, 표시 이름 등 미니앱 연동 설정 창을 엽니다 |
| **Install Sentry SDK** | Sentry Unity SDK를 설치합니다. 자세한 내용은 [Sentry 연동](SentryIntegration.md) 참고 |
| **이슈 제보하기** | 문제 상황을 제보하는 창을 엽니다 |
| **Check for Updates...** | SDK 신규 릴리즈가 있는지 수동으로 확인합니다 |
| **Debug** | SDK 상태 초기화, WebGL 템플릿 강제 갱신 등 디버그용 하위 메뉴가 모여 있습니다 |

Dev Server와 Production Server, 각 빌드 프로필의 devtools·압축 설정 차이는 [빌드 프로필](BuildProfiles.md)에 정리되어 있습니다.

## 첫 번째 빌드

빌드 진입점은 모두 `AIT` 메뉴에 있습니다. 각 진입점이 무엇을 어떻게 다르게 빌드하는지는 [빌드 프로필](BuildProfiles.md)에 정리되어 있습니다.

### 개발 서버로 확인하기

개발 단계에서는 Dev Server를 사용합니다. `@apps-in-toss/devtools`의 Mock SDK와 패널이 함께 실행되어, toss 앱 없이 브라우저에서 플랫폼 API 호출을 mock으로 확인하고 패널로 mock 상태를 직접 제어할 수 있습니다.

1. `AIT` > `Dev Server` > `Start Server` 클릭
2. Unity WebGL 빌드가 자동으로 실행됩니다
3. 빌드가 끝나면 로컬 개발 서버가 시작됩니다
4. 브라우저가 자동으로 열리거나, 콘솔에 표시된 URL로 접속합니다

### 배포용 패키지 만들기

1. `AIT` > `Build & Package` 클릭
2. 빌드가 끝나면 `ait-build/dist/`에서 결과물을 확인합니다

### 실기기로 확인하기 (Deploy Test)

브라우저 Mock으로는 확인할 수 없는 실제 Toss 앱 환경(카메라, 결제, 광고 등)을 실기기에서 확인하려면 Deploy (Test)를 사용합니다.

1. `AIT` > `Deploy (Test)` 클릭
2. 배포 키가 설정되어 있어야 합니다. `AIT` > `Configuration`에서 입력합니다
3. 증분 빌드 후 `ait deploy`로 콘솔 QR 테스트 환경에 배포됩니다 (memo에 `[Test]` 접두사가 자동으로 붙습니다)
4. 배포가 끝나면 뜨는 창의 QR을 Toss 앱으로 스캔하거나 URL로 접속해 실기기에서 확인합니다

### 플랫폼에 출시하기 (Deploy Production)

실제 사용자에게 노출하려면 클린 빌드로 다시 배포한 뒤 콘솔에서 심사를 신청해야 합니다.

1. `AIT` > `Deploy (Production)` 클릭 (Deploy (Test)와 동일하지만 클린 빌드 + memo `[Production]` 접두사)
2. 배포가 끝나면 뜨는 창에서 "콘솔 열기" 버튼으로 Apps in Toss 콘솔로 이동합니다
3. 콘솔에서 방금 배포한 빌드를 심사/출시 신청합니다 — `ait deploy` 자체는 항상 콘솔 QR 테스트 환경에 배포할 뿐이며, 실제 출시는 이 콘솔 절차로만 이뤄집니다

## SDK 사용 예제

SDK API는 async/await 패턴을 사용합니다. `Awaitable`과 `Task` 중 무엇이 반환되는지, 타임아웃과 에러 코드를 어떻게 다루는지는 [API 사용 패턴](APIUsagePatterns.md)에 정리되어 있습니다.

### 기기 정보 조회

```csharp
using AppsInToss;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    async void Start()
    {
        try
        {
            // 기기 ID 조회
            string deviceId = await AIT.GetDeviceId();
            Debug.Log($"Device ID: {deviceId}");

            // 플랫폼 OS 조회
            string os = await AIT.GetPlatformOS();
            Debug.Log($"Platform: {os}");

            // 네트워크 상태 확인
            NetworkStatus status = await AIT.GetNetworkStatus();
            Debug.Log($"Network: {status}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"API 호출 실패: {ex.Message} (code: {ex.ErrorCode})");
        }
    }
}
```

### 결제 요청

```csharp
using AppsInToss;
using UnityEngine;
using System.Threading.Tasks;

public class PaymentManager : MonoBehaviour
{
    public async Task RequestPayment()
    {
        try
        {
            var options = new CheckoutPaymentOptions {
                PayToken = "your-pay-token"
            };

            CheckoutPaymentResult result = await AIT.CheckoutPayment(options);
            Debug.Log($"Payment success: {result.Success}");
        }
        catch (AITException ex)
        {
            Debug.LogError($"결제 실패: {ex.Message}");
        }
    }
}
```

> **중요**: 인앱결제는 지급 승인 콜백을 반드시 지정해야 합니다. 지정하지 않으면 모든 결제가 지급 실패로 처리됩니다. [API 사용 패턴](APIUsagePatterns.md)의 인앱결제 절을 먼저 읽어보세요.

### 햅틱 피드백

```csharp
using AppsInToss;
using UnityEngine;

public class FeedbackManager : MonoBehaviour
{
    public async void VibrateDevice()
    {
        try
        {
            var options = new HapticFeedbackOptions {
                Type = HapticFeedbackType.Tap
            };

            await AIT.GenerateHapticFeedback(options);
            Debug.Log("Haptic feedback generated");
        }
        catch (AITException ex)
        {
            Debug.LogError($"햅틱 피드백 실패: {ex.Message}");
        }
    }
}
```

## 테스트하기

SDK API는 WebGL 빌드에서만 실제로 브릿지를 타고, 그마저도 대부분 Apps in Toss 앱 환경에서만 정상 동작합니다. Unity Editor에서는 Editor mock이 기본값을 돌려줄 뿐입니다. 자세한 내용은 [API 사용 패턴](APIUsagePatterns.md)의 **Mock** 절을 참고하세요.

샌드박스 앱으로 로컬 빌드를 확인하는 절차는 [문제 해결](Troubleshooting.md) 문서의 "Dev Server 에서는 되는데 Production 에서 안 됨" 절에 정리되어 있습니다.

배포 전 최종 검증에는 `.ait` 파일 업로드 테스트를 사용합니다.

1. `AIT` > `Build & Package`로 `.ait` 파일을 생성합니다.
2. [Apps in Toss 콘솔](https://apps-in-toss.toss.im/)에 업로드합니다.
3. QR 코드로 미니앱을 실행해 확인합니다.

막히는 부분이 있으면 [문제 해결](Troubleshooting.md) 문서를 참고하세요.

## 관련 문서

- [API 사용 패턴](APIUsagePatterns.md) — async/await, 에러 처리, Mock
- [빌드 프로필](BuildProfiles.md) — 빌드 진입점별 설정 차이
- [빌드 커스터마이징](BuildCustomization.md) — 웹 진입점 수정, 외부 라이브러리 추가
- [로딩 화면 커스터마이징](LoadingScreenCustomization.md) — 로딩 화면 교체
- [문제 해결](Troubleshooting.md) — 자주 막히는 지점과 해결 방법
