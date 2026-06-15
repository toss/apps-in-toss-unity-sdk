# API 사용 패턴

이 문서는 Apps in Toss Unity SDK의 API 사용 패턴을 설명합니다.

## 목차

- [기본 패턴: async/await](#기본-패턴-asyncawait)
- [에러 처리](#에러-처리)
- [WebGL vs Unity Editor](#webgl-vs-unity-editor)
- [Mock 브릿지](#mock-브릿지)

---

## 기본 패턴: async/await

SDK의 모든 API는 C#의 `async/await` 패턴을 사용합니다. 이는 Unity의 메인 스레드를 차단하지 않고 비동기 작업을 수행할 수 있게 해줍니다.

### 기본 사용법

```csharp
using AppsInToss;
using UnityEngine;

public class Example : MonoBehaviour
{
    async void Start()
    {
        // await 키워드로 비동기 결과를 대기
        string deviceId = await AIT.GetDeviceId();
        Debug.Log($"Device ID: {deviceId}");
    }
}
```

### Task 반환 메서드

외부에서 호출해야 하는 경우 `Task`를 반환하세요:

```csharp
using System.Threading.Tasks;

public class PaymentController : MonoBehaviour
{
    // Task를 반환하면 호출자가 await 가능
    public async Task<bool> ProcessPayment(string orderId)
    {
        try
        {
            var options = new CheckoutPaymentOptions {
                // 옵션 설정
            };

            var result = await AIT.CheckoutPayment(options);
            return result != null;
        }
        catch (AITException)
        {
            return false;
        }
    }
}
```

### 여러 API 연속 호출

여러 API를 순차적으로 호출할 때도 async/await를 사용합니다:

```csharp
async void InitializeGame()
{
    // 순차 호출
    string deviceId = await AIT.GetDeviceId();
    string platform = await AIT.GetPlatformOS();
    string locale = await AIT.GetLocale();

    Debug.Log($"기기: {deviceId}, 플랫폼: {platform}, 언어: {locale}");
}
```

### 병렬 호출

독립적인 API들은 `Task.WhenAll`로 병렬 호출할 수 있습니다:

```csharp
async void InitializeGameParallel()
{
    // 병렬 호출 - 더 빠름
    var deviceIdTask = AIT.GetDeviceId();
    var platformTask = AIT.GetPlatformOS();
    var localeTask = AIT.GetLocale();

    await Task.WhenAll(deviceIdTask, platformTask, localeTask);

    Debug.Log($"기기: {deviceIdTask.Result}, 플랫폼: {platformTask.Result}, 언어: {localeTask.Result}");
}
```

---

## 에러 처리

### AITException

SDK API 호출 실패 시 `AITException`이 throw됩니다:

```csharp
using AppsInToss;
using UnityEngine;

public class ErrorHandling : MonoBehaviour
{
    async void CallAPI()
    {
        try
        {
            var result = await AIT.GetDeviceId();
            Debug.Log($"성공: {result}");
        }
        catch (AITException ex)
        {
            // API 호출 실패
            Debug.LogError($"API 오류: {ex.Message}");
            Debug.LogError($"오류 코드: {ex.ErrorCode}");
        }
        catch (System.Exception ex)
        {
            // 기타 예외
            Debug.LogError($"예상치 못한 오류: {ex.Message}");
        }
    }
}
```

### AITException 속성

| 속성 | 타입 | 설명 |
|------|------|------|
| `Message` | `string` | 사람이 읽을 수 있는 오류 메시지 |
| `ErrorCode` | `string` | 오류 코드 (프로그래밍 방식 처리용) |
| `APIName` | `string` | 실패한 API 이름 |
| `IsPlatformUnavailable` | `bool` | 플랫폼 미지원으로 인한 오류 여부 (브라우저 환경 등) |

### 오류 코드별 처리

```csharp
async void HandlePayment()
{
    try
    {
        var result = await AIT.CheckoutPayment(options);
    }
    catch (AITException ex)
    {
        switch (ex.ErrorCode)
        {
            case "PAYMENT_CANCELLED":
                Debug.Log("사용자가 결제를 취소했습니다.");
                break;
            case "PAYMENT_FAILED":
                Debug.LogError("결제 처리 중 오류가 발생했습니다.");
                break;
            case "NETWORK_ERROR":
                Debug.LogError("네트워크 연결을 확인해주세요.");
                break;
            default:
                Debug.LogError($"알 수 없는 오류: {ex.Message}");
                break;
        }
    }
}
```

---

## WebGL vs Unity Editor

SDK API의 동작은 실행 환경에 따라 다릅니다.

### 실행 환경별 동작

| 환경 | 동작 |
|------|------|
| **WebGL 빌드** (Apps in Toss 앱 내) | 실제 네이티브 API 호출 |
| **WebGL 빌드** (일반 브라우저) | Mock 구현 호출 (Dev Server 모드) |
| **Unity Editor** | Mock 구현 호출 |
| **기타 플랫폼** (Windows, macOS 등) | Mock 구현 호출 |

### 환경별 코드 분기

필요한 경우 환경에 따라 다른 로직을 실행할 수 있습니다:

```csharp
using UnityEngine;

public class PlatformAware : MonoBehaviour
{
    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("WebGL 빌드에서 실행 중");
        // WebGL 전용 로직
#else
        Debug.Log("에디터 또는 다른 플랫폼에서 실행 중");
        // 개발/테스트용 로직
#endif
    }
}
```

### 주의사항

- Unity Editor에서는 항상 Mock 구현이 호출됩니다.
- 실제 네이티브 기능을 테스트하려면 WebGL로 빌드 후 Apps in Toss 앱에서 실행해야 합니다.
- Dev Server 모드는 일반 브라우저에서도 Mock으로 동작하여 개발을 용이하게 합니다.

---

## Mock 브릿지

### Mock 브릿지란?

Mock 브릿지는 네이티브 API가 없는 환경(Unity Editor, 일반 브라우저)에서 SDK API를 테스트할 수 있도록 하는 시뮬레이션 레이어입니다.

### Mock 동작

| API | Mock 반환값 |
|-----|------------|
| `GetDeviceId()` | 빈 문자열 `""` |
| `GetPlatformOS()` | 빈 문자열 `""` |
| `GetNetworkStatus()` | `default(NetworkStatus)` |
| `CheckoutPayment()` | `default(CheckoutPaymentResult)` |
| `GenerateHapticFeedback()` | 로그 출력만 (실제 진동 없음) |

### Mock 로그

Mock 브릿지가 호출되면 Console에 로그가 출력됩니다:

```
[AIT Mock] GetDeviceId called
[AIT Mock] GetPlatformOS called
```

### 빌드 프로필별 Mock 설정

| 빌드 프로필 | Mock 브릿지 |
|------------|------------|
| Dev Server | ✅ 활성화 |
| Production Server | ❌ 비활성화 |
| Build & Package | ❌ 비활성화 |
| Publish | ❌ 비활성화 |

> 자세한 빌드 프로필 설정은 [빌드 프로필 문서](BuildProfiles.md)를 참조하세요.

---

## 다음 단계

- [시작하기](GettingStarted.md) - 설치 및 기본 설정
- [빌드 프로필](BuildProfiles.md) - 빌드 설정 상세 안내
- [문제 해결](Troubleshooting.md) - FAQ 및 트러블슈팅
