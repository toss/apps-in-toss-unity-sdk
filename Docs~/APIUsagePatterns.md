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

### 먼저: 이 콜백은 선택이 아닙니다

`ProcessProductGrant`는 nullable 필드라 지정하지 않아도 컴파일되지만, **지정하지 않으면 모든 결제가 지급 실패로 처리됩니다.**

```csharp
// ❌ 컴파일도 되고 결제 창도 뜨지만, 상품이 지급되지 않습니다
var options = new IapCreateOneTimePurchaseOrderOptionsOptions { Sku = sku };
```

SDK 내부적으로 JS 브릿지는 이 콜백을 **항상** 플랫폼에 넘깁니다. C# 쪽에서 콜백이 등록되지 않았을 뿐이라, 결제가 완료되면 플랫폼이 콜백을 호출하고 SDK는 등록된 핸들러가 없다는 이유로 자동으로 `false`를 응답합니다. 이때 Console에 다음 경고가 남습니다.

```
[AITCore] Unknown nested callback: {subscriptionId}_processProductGrant
```

결제 흐름을 붙일 때 이 필드부터 채우세요.

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

### 장부가 두 개라는 것부터

"서버 검증 결과로 지급 여부를 결정한다"가 안 되는 진짜 이유는 시간이 부족해서가 아니라, **서로 다른 두 장부를 하나로 취급했기** 때문입니다.

| | 무엇을 기록하나 | 소유 | 마감 |
|---|---|---|---|
| `ProcessProductGrant` 반환값 | **결제가 소비됐는가** | Toss | 30초 (프레임 없음) |
| 내 서버의 지급 기록 | **아이템을 배달했는가** | 개발사 | 마감 없음, 재시도 가능 |

검증은 첫 번째 장부를 막는 게 아니라 **두 번째 장부를 막습니다.** 콜백은 "결제 소비를 접수했다"고 답하는 자리고, 검증과 지급은 그 뒤에 여유롭게 합니다.

그래서 이 콜백에 넣을 코드는 사실상 한 줄로 정해져 있습니다.

### 1단계 — 콜백은 즉시 승인한다

```csharp
var options = new IapCreateOneTimePurchaseOrderOptionsOptions
{
    Sku = sku,
    ProcessProductGrant = _ => Task.FromResult(true)   // await 0회
};
```

이 콜백이 호출됐다는 것 자체가 이미 앱이 결제 성공을 판정했다는 뜻입니다. 콜백이 들고 오는 정보는 `OrderId` 하나뿐이라, 여기서 새로 검증할 수 있는 것도 없습니다.

### 2단계 — 검증과 지급은 `onEvent`에서

`onEvent`는 **`OrderId`와 살아 있는 player loop를 동시에 갖는 첫 순간**입니다. 실측에서 오버레이가 닫히고 45ms 뒤에 도착했습니다. 여기서부터는 `await`를 마음껏 써도 됩니다.

```csharp
_disposer = AIT.IAPCreateOneTimePurchaseOrder(
    onEvent: e =>
    {
        // 결제는 이미 확정됐으므로 UI에 즉시 반영해도 됩니다
        ShowPurchaseSuccess(e.Data.DisplayAmount);

        // 검증·지급은 서버에 맡기고 기다리지 않습니다
        _ = DeliverAsync(e.Data.OrderId);
    },
    options: options,
    onError: err => Debug.LogError(err.Message)
);

async Task DeliverAsync(string orderId)
{
    // 여기서는 프레임이 정상적으로 돌므로 await가 안전합니다
    await MyServer.VerifyAndDeliver(orderId);
}
```

> `SuccessEvent.Data`에는 `Sku`가 없습니다. 어떤 상품인지는 구매를 시작할 때 넘긴 `sku`를 클로저로 잡아두거나, 서버가 `OrderId`로 조회해야 합니다.

#### 서버는 무엇을 검증하나

클라이언트가 보낸 `OrderId`를 그대로 믿으면 안 됩니다. 개발사 서버는 **주문 상태 조회 API**로 Toss에 직접 확인합니다.

```
POST https://apps-in-toss-api.toss.im/api-partner/v1/apps-in-toss/order/get-order-status
{ "orderId": "..." }
```

- **mTLS 인증서가 필수**입니다 (서버 간 통신). 발급 방법은 [연동 절차 문서](https://developers-apps-in-toss.toss.im/development/integration-process.html)를 참고하세요.
- `x-toss-user-key` 헤더에 토스 로그인으로 얻은 userKey를 넣으면 **그 유저의 주문만** 응답합니다. 넣지 않으면 모든 주문이 조회되므로, 다른 유저의 `OrderId`를 가로채 재사용하는 것을 막으려면 이 헤더를 함께 보내야 합니다.
- 응답의 `sku`로 실제 결제된 상품을 확인할 수 있습니다. 클라이언트가 알려준 SKU를 신뢰하지 마세요.

응답 `status`가 이 API의 핵심입니다.

| status | 의미 |
|---|---|
| `PURCHASED` | 결제와 상품 지급이 모두 완료 |
| `PAYMENT_COMPLETED` | 결제는 완료됐으나 **상품 지급 실패** |
| `REFUNDED` | 환불 완료 |
| `FAILED` / `ORDER_IN_PROGRESS` / `NOT_FOUND` | 결제 실패 / 진행 중 / 주문 없음 |

앞의 두 값이 곧 `ProcessProductGrant` 반환값의 결과입니다. `true`를 반환한 주문은 `PURCHASED`, 그렇지 않은 주문은 `PAYMENT_COMPLETED`로 남습니다.

자세한 명세는 [공식 IAP 문서](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%EC%9D%B8%EC%95%B1%20%EA%B2%B0%EC%A0%9C/IAP.html)를 참고하세요.

### 3단계 — 앱 시작 시 미배달 대사

2단계가 항상 실행된다는 보장은 없습니다. 콜백이 `true`를 보낸 직후 앱이 종료되면 `onEvent`를 받지 못하고, 그 주문은 이미 결제 소비가 확정돼 `IAPGetPendingOrders`에도 나타나지 않습니다.

이 경우를 회수하는 것이 `IAPGetCompletedOrRefundedOrders`입니다. 앱 시작이나 포그라운드 복귀 시 한 번 훑어, 내 서버가 배달하지 않은 주문을 찾습니다.

```csharp
var completed = await AIT.IAPGetCompletedOrRefundedOrders();
if (completed.Orders == null) return;   // 플랫폼 미지원 시 error 필드에 사유가 담깁니다

foreach (var order in completed.Orders)
{
    if (order.Status != CompletedOrRefundedOrdersResultOrderStatus.COMPLETED) continue;

    // 배달 여부의 기준은 서버 기록입니다. PlayerPrefs 같은 로컬 기록은
    // 재설치·기기 변경으로 사라지므로 이 대사의 기준이 될 수 없습니다.
    await MyServer.DeliverIfMissing(order.OrderId, order.Sku);
}
```

이 3단계가 없으면 1단계의 즉시 승인이 위험해집니다. **셋은 한 묶음입니다.**

> **환불은 폴링으로만 알 수 있습니다.** 결제나 환불이 발생했을 때 개발사 서버로 알려주는 웹훅은 제공되지 않습니다. 사용자가 환불을 받아도 앱이 다시 실행되어 이 대사가 돌기 전까지는 개발사가 알 수 없습니다. 환불된 주문의 상품을 회수해야 한다면, 지급한 주문의 `OrderId`를 서버에 보관해두고 주문 상태 조회 API로 주기적으로 확인해야 합니다.

### `false`는 언제 반환하나

공식 문서는 `true`가 아닌 응답에 대해 환불 안내 페이지가 *노출될 수 있다*고 안내합니다. (직접 측정한 것은 무응답 경로이며, 명시적 `false`에서도 같은 화면이 나오는지는 확인하지 않았습니다.) 따라서 `false`는 **정말로 이 상품을 줄 수 없을 때만** 씁니다 — 예를 들어 이미 보유한 비소모품을 결제 도중 다른 기기에서 획득한 경우처럼, 지급이 불가능하다고 지금 단정할 수 있을 때입니다.

"확신이 없으니 일단 `false`"는 성립하지 않습니다. 매 결제마다 환불 안내가 뜨는 앱이 되기 때문입니다. 확신은 1~3단계로 확보하는 것이지 `false`로 확보하는 것이 아닙니다.

판정 근거는 반드시 이미 메모리에 있어야 합니다. `false`를 반환하기 위해 무언가를 조회해야 한다면, 그 조회 자체가 이 절이 설명한 교착을 일으킵니다.

> **구버전 토스앱에서는 반환값이 무시됩니다.** `processProductGrant`를 지원하지 않는 버전(Android 5.231.1 미만 / iOS 5.230.0 미만)에서는 브릿지가 구 결제 경로로 폴백하며, 이때 콜백의 반환값은 플랫폼에 전달되지 않고 버려집니다. 반환값에 의존하는 로직을 짤 때 이 구간을 염두에 두세요.

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
