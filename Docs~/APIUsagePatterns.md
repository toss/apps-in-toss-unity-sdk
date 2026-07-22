# API 사용 패턴

이 문서는 Apps in Toss Unity SDK의 API 사용 패턴을 설명합니다.

## 목차

- [기본 패턴: async/await](#기본-패턴-asyncawait)
- [인앱결제: await를 쓰면 안 되는 자리](#인앱결제-await를-쓰면-안-되는-자리)
- [에러 처리](#에러-처리)
- [WebGL vs Unity Editor](#webgl-vs-unity-editor)
- [Mock 브릿지](#mock-브릿지)

---

## 기본 패턴: async/await

SDK의 모든 API는 C#의 `async/await` 패턴을 사용합니다. 이는 Unity의 메인 스레드를 차단하지 않고 비동기 작업을 수행할 수 있게 해줍니다.

> ⚠️ **예외가 하나 있습니다.** 인앱결제의 `ProcessProductGrant` 콜백 안에서는 `await`를 쓰면 안 됩니다. 결제가 완료되지 않습니다. [인앱결제 절](#인앱결제-await를-쓰면-안-되는-자리)을 먼저 읽어주세요.

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

## 인앱결제: await를 쓰면 안 되는 자리

`IAPCreateOneTimePurchaseOrder` / `IAPCreateSubscriptionPurchaseOrder`에 넘기는 `ProcessProductGrant` 콜백은 **동기로 값을 반환해야 합니다.** 타입이 `Task<bool>`이라 `await`를 쓸 수 있게 생겼지만, 쓰면 결제가 완료되지 않습니다.

### 왜 안 되는가

이 콜백은 **네이티브 결제 오버레이가 화면을 덮고 있는 동안** 호출됩니다. 그 구간에는 브라우저가 `visibilityState = hidden` 상태라 `requestAnimationFrame`이 멈추고, 그것이 유일한 구동원인 Unity WebGL의 player loop도 함께 멈춥니다. player loop가 멈추면 `await`의 continuation이 재개되지 않습니다.

그래서 콜백 안에서 무언가를 `await`하면 순환 교착이 생깁니다:

```
오버레이는 이 콜백의 응답을 기다린다
   → 콜백은 await continuation을 기다린다
      → continuation은 프레임을 기다린다
         → 프레임은 오버레이가 닫혀야 온다   ← 처음으로 돌아감
```

실기기 실측에서 이 고리가 **115초간** 유지된 뒤, 앱이 자체 타임아웃으로 끊고 `"{앱 이름}에 문제가 생겼어요. 환불을 신청해주세요"` 페이지를 띄웠습니다. 결제 성공 후 30초 안에 `ProcessProductGrant`가 `true`로 응답하지 않으면 이 페이지가 노출될 수 있습니다.

같은 실측에서 동기로 반환했을 때는 오버레이가 **1.5초 만에** 닫히고 결제가 정상 완료됐습니다.

### 패턴 A — 서버 검증이 필요한 경우

검증을 **결제 시작 전에** 끝내고, 그 결과를 캡처해서 콜백에서 그대로 반환합니다. 실패하면 결제 자체를 시작하지 않는 선택지도 생깁니다.

```csharp
// 1) 오버레이가 뜨기 전 — 여기서는 프레임이 정상적으로 돕니다
bool authorized = await MyServer.ReserveEntitlement(sku);
if (!authorized)
{
    ShowError("지금은 구매할 수 없습니다");
    return;
}

// 2) 콜백은 이미 결정된 값을 동기로 반환합니다 (await 0회)
var options = new IapCreateOneTimePurchaseOrderOptionsOptions
{
    Sku = sku,
    ProcessProductGrant = _ => Task.FromResult(authorized)
};

_disposer = AIT.IAPCreateOneTimePurchaseOrder(
    onEvent: e => GrantItemLocally(e.Data.OrderId),
    options: options,
    onError: err => Debug.LogError(err.Message)
);
```

### 패턴 B — 미리 알 수 없는 경우

지급 가능 여부를 결제 전에 확정할 수 없다면 `false`를 반환하고, 나중에 복구합니다.

```csharp
ProcessProductGrant = _ => Task.FromResult(false)
```

주문이 `PAYMENT_COMPLETED` 상태로 남아 `IAPGetPendingOrders`에 계속 보이므로, 앱 시작 시 밀린 주문을 훑어 검증하고 지급을 완료하면 됩니다.

```csharp
var pending = await AIT.IAPGetPendingOrders();
if (pending.Orders == null) return;   // 플랫폼 미지원 시 error 필드에 사유가 담깁니다

foreach (var order in pending.Orders)
{
    if (!await MyServer.VerifyAndGrant(order.OrderId, order.Sku)) continue;

    await AIT.IAPCompleteProductGrant(new IAPCompleteProductGrantArgs_0
    {
        Params = new IAPCompleteProductGrantArgs_0Params { OrderId = order.OrderId }
    });
}
```

사용자에게 환불 안내 페이지가 한 번 보이는 대신, 돈만 받고 상품을 못 주는 상황은 구조적으로 생기지 않습니다.

### `true`와 `false`는 대칭이 아닙니다

| 반환값 | 주문 상태 | `IAPGetPendingOrders` | 되돌리기 |
|---|---|---|---|
| `true` | `PURCHASED` (지급 확정) | 사라짐 | **불가** — un-grant API 없음 |
| `false` / 무응답 | `PAYMENT_COMPLETED` | 계속 보임 | 가능 — `IAPCompleteProductGrant` |

`true`는 편도입니다. 확신이 없으면 `false`가 안전한 방향입니다. "일단 `true`로 지급하고 나중에 정산하자"는 로컬 기록(예: `PlayerPrefs`)에 의존하게 되는데, 재설치·기기 변경·결제 중 앱 종료에서 그 기록이 사라지면 복구할 방법이 없습니다.

### 쓸 수 없는 것들

| 하려던 것 | 결과 |
|---|---|
| `await UnityWebRequest` 영수증 검증 | 오버레이가 닫힐 때까지 완료 통지가 오지 않음 |
| `await Task.Delay(...)` | WebGL에는 타이머 스레드가 없어 **아예 완료되지 않음** |
| `await` 코루틴 래퍼 (`WaitForSecondsRealtime`) | 코루틴도 player loop가 구동하므로 동일하게 멈춤 |
| 콜백 안에서 UI를 띄워 사용자 입력 대기 | 화면이 오버레이에 덮여 있어 사용자가 볼 수 없음 |

> 이 제약은 Unity 고유가 아닙니다. 웹(React 등) 미니앱에서도 `processProductGrant`가 30초를 넘기면 같은 환불 페이지가 노출됩니다. 다만 Unity WebGL은 *모든* `await`가 프레임을 필요로 해서 더 쉽게 걸립니다.

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
